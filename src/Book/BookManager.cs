using QuickFix;
using QuickFix.Fields;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Matching
{
    public class BookManager : IBrokerBook, IMarketBook, IDisposable
    {
        private bool _disposed;
        private object _sync;
        List<Instrument> _instrumentsTest;
        private ConcurrentBag<Instrument> _instruments;
        private readonly Dictionary<string, BookNode> _books;
        private readonly HashSet<string> _instrumentsHasOffer;
        private readonly PersistenceManager _persistence;
        private readonly Notifier _notifier;
        private readonly Params _params;
        private ILogManager _log;

        public BookManager(Params p, ILogManager log, IBrokerProvider broker, IMarketProvider market)
        {
            _disposed = false;
            _sync = new object();
            Translator.IsRF = p.IsRF;
            Translator.SendBrokerManagerID = p.SendBrokerManagerID;
            Translator.SendROP = p.SendROP;
            _log = log;
            
            _books = new Dictionary<string, BookNode>();
            _instrumentsHasOffer = new HashSet<string>();
            _persistence = new PersistenceManager(p, log);
            _notifier = new Notifier(broker, market, log);
            _params = p;
        }

        public BookNode CreateBookNode(Instrument i)
        {
            if(!i.IsLinked)
                return new BookNode(i, _notifier, _params);
            else
                return new BookLinkedNode(i, _notifier, _params);
        }

        public bool CleanupTest(Message message)
        {
            _log?.OnLog("BookManager::CleanupTest >> Cleanup Test Instruments ...");

                
            _persistence.CleanupTest(_instrumentsTest);

            foreach(Instrument i in _instrumentsTest)
            {

                lock (_sync)
                {
                    if (_books.ContainsKey(i.Symbol))
                    {
                        _log?.OnLog("BookManager::CleanupTest >> Clean Book " + i.Symbol);
                        _books[i.Symbol].Clear();
                    }

                    if(_instrumentsHasOffer.Contains(i.Symbol))
                    {
                        _log?.OnLog("BookManager::CleanupTest >> Clean Has Offer " + i.Symbol);
                        _instrumentsHasOffer.Remove(i.Symbol);
                    }
                }
            }
            var response = new QuickFix.FIX44.MDInstrumentTestCleanup();
            response.SetField(new TransactTime(message.GetDateTime(Tags.TransactTime)));
            _notifier.NotifyMarket(response, "ALL");

            var responseBroker = new QuickFix.FIX44.MDInstrumentTestCleanupResponse();
            responseBroker.SetField(new TransactTime(message.GetDateTime(Tags.TransactTime)));
            _notifier.NotifyBroker(responseBroker, "ALL");

            return true;
        }

        public bool ReloadMarket(Message message)
        {
            _log?.OnLog("BookManager::ReloadMarket >> Reloading Instruments ...");

            lock (_sync)
            {
                _instruments.Clear();
                if(!_persistence.LoadInstruments(_instruments))
                    return false;

                _instrumentsTest.Clear();

                foreach (var i in _instruments)
                {
                    
                    if (!_books.ContainsKey(i.Symbol))
                    {
                        var node = CreateBookNode(i);
                        _books.Add(i.Symbol, node);
                    }

                    if(i.IsTest)
                        _instrumentsTest.Add(i);
                }
            }

            var response = new QuickFix.FIX44.MatchInstrumentReload();
            response.SetField(new TransactTime(message.GetDateTime(Tags.TransactTime)));
            _notifier.NotifyMarket(response, "ALL");

            return true;
        }

        public bool LoadMarket()
        {
            _log?.OnLog("BookManager::LoadMarket >> Cleanning up ...");
            if(!_persistence.Cleanup())
                return false;

            _log?.OnLog("BookManager::LoadMarket >> Loading last ID ...");
            if(!_persistence.LoadLastID())
                return false;

            _log?.OnLog("BookManager::LoadMarket >> Loading Instruments ...");
            _instruments = new ConcurrentBag<Instrument>();
            _instrumentsTest = new List<Instrument>();

             if(!_persistence.LoadInstruments(_instruments))
                return false;

            foreach (var i in _instruments)
            {
                if (!_books.ContainsKey(i.Symbol))
                {
                    var node = CreateBookNode(i);
                    _books.Add(i.Symbol, node);
                }

                if(i.IsTest)
                    _instrumentsTest.Add(i);
            }

            _log?.OnLog("BookManager::LoadMarket >> Loading messages  ...");
            Queue<Message> messages = new Queue<Message>();

            if(!_persistence.LoadMessages(messages))
                return false;

            while (messages.Count > 0)
            {
                var m = messages.Dequeue();

                string msgType = m.Header.GetString(Tags.MsgType);

                if (msgType == MsgType.MATCH_NEW_ORDER_SINGLE)
                {
                    this.InsertOrder(m, false);
                }
                else if (msgType == MsgType.MATCH_NEW_ORDER_CROSS)
                {
                    this.CrossTrade(m, false);
                }
                else if (msgType == MsgType.MATCH_ORDER_REPLACE_REQUEST)
                {
                    this.ReplaceOrder(m, false);
                }
                else if (msgType == MsgType.MATCH_ORDER_CANCEL_REQUEST)
                {
                    this.CancelOrder(m, false);
                }
                else if (msgType == MsgType.MATCH_TRADE_CANCEL_REQUEST)
                {
                    this.CancelTrade(m, false);
                }
                else if (msgType == MsgType.MATCH_TRADE_UPDATE_REQUEST)
                {
                    this.UpdateTrade(m, false);
                }
            }

            _log?.OnLog("BookManager::LoadMarket >> Success!");


            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _sync = null;
                _instruments.Clear();
                _books.Clear();
                _instrumentsHasOffer.Clear();
                _persistence.Dispose();
                _notifier.Dispose();
            }       
        }

        public void RejectOrder(Message message)
        {
            if(message.Header.GetString(Tags.MsgType) == MsgType.MATCH_NEW_ORDER_CROSS)
            {
                BookLine rejectLine1 = new BookLine(message, new Instrument(), '1', "", DateTime.Now, _params);
                rejectLine1.Status = new OrderStatus(OrderStatus.REJECTED);
                _notifier.NotifyBroker(Translator.BrokerReport(rejectLine1), rejectLine1.Symbol);
                BookLine rejectLine2 = new BookLine(message, new Instrument(), '2', "", DateTime.Now, _params);
                rejectLine2.Status = new OrderStatus(OrderStatus.REJECTED);
                _notifier.NotifyBroker(Translator.BrokerReport(rejectLine2), rejectLine2.Symbol);
            }
            else
            {
                BookLine rejectLine = new BookLine(message, new Instrument(), _params);
                rejectLine.Status = new OrderStatus(OrderStatus.REJECTED);
                _notifier.NotifyBroker(Translator.BrokerReport(rejectLine), rejectLine.Symbol);
            }
        }

        public void InsertOrder(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);

            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                    
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::InsertOrder() >> Error:Symbol [" + symbol + "] not identified!");
                    RejectOrder(message);
                }
            }

            if (book != null)
            {
                if(book.InsertOrder(message))
                {
                    lock (_sync)
                    {
                        if(_instrumentsHasOffer.Add(symbol))
                            SendSecurityQuotes(symbol, MDUpdateAction.NEW);
                    }
                }
                //book.PrintBook();
            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }

        public void ReplaceOrder(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);

            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::ReplaceOrder() >> Error:Symbol [" + symbol + "] not identified!");
                    RejectOrder(message);
                }
            }

            if (book != null)
            {
                book.ReplaceOrder(message);
                //book.PrintBook();
            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }

        public void CancelOrder(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);
           
            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::CancelOrder() >> Error:Symbol [" + symbol + "] not identified!");
                    RejectOrder(message);
                }
            }

            if (book != null)
            {
                if(book.CancelOrder(message))
                {
                    if(book.IsEmpty())
                    {
                        lock (_sync)
                        {
                            _instrumentsHasOffer.Remove(symbol);
                        }
                        SendSecurityQuotes(symbol, MDUpdateAction.DELETE);
                    }
                }
            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }

        public void CancelTrade(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);
            
            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::CancelTrade() >> Error:Symbol [" + symbol + "] not identified!");
                    RejectOrder(message);
                }
            }

            if (book != null)
            {
                book.CancelTrade(message);
                //book.PrintBook();

            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }

        public void UpdateTrade(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);
            
            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::UpdateTrade() >> Error:Symbol [" + symbol + "] not identified!");
                }
            }

            if (book != null)
            {
                book.UpdateTrade(message);
            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }

        public void CrossTrade(Message message, bool notifyBase)
        {
            var symbol = message.GetString(Tags.Symbol);

            BookNode book = null;

            lock (_sync)
            {
                if (_books.ContainsKey(symbol))
                {
                    book = _books[symbol];
                }
                else
                {
                    _notifier?.NotifyLog("BookManager::CrossTrade() >> Error:Symbol [" + symbol + "] not identified!");
                    RejectOrder(message);
                }
            }

            if (book != null)
            {
                if(book.CrossTrade(message))
                {
                    lock (_sync)
                    {
                        if(_instrumentsHasOffer.Add(symbol))
                            SendSecurityQuotes(symbol, MDUpdateAction.NEW);
                    }
                }
                
            }

            if (notifyBase)
                _persistence.SaveOrder(message);

            message.Clear();
        }
        
        public void MarketDataRequest(Message message, SessionID sessionID)
        {
            string mdReqID = message.GetString(Tags.MDReqID);
            int numGrp = message.GetInt(Tags.NoRelatedSym);

            for (int i = 1; i <= numGrp; i++)
            {
                Group group = message.GetGroup(i, Tags.NoRelatedSym);
                string symbol = group.GetString(Tags.Symbol);
                string securityID = group.GetString(Tags.SecurityID);

                var response = new QuickFix.FIX44.MarketDataSnapshotFullRefresh();
                response.Header.SetField(new SenderCompID(sessionID.TargetCompID));
                response.Header.SetField(new TargetCompID(sessionID.SenderCompID));
                response.Set(new Symbol(symbol));
                response.Set(new SecurityID(securityID));
                response.Set(new MDReqID(mdReqID));

                BookNode node = null;

                lock (_sync)
                {
                    if (_books.ContainsKey(symbol))
                    {
                        node = _books[symbol];
                    }
                    else 
                    {
                        var responseReject = MarketDataRequestReject(mdReqID, symbol, sessionID);
                        _notifier.NotifyMarket(responseReject, "MD");
                    }
                }

                if (node != null)
                {
                    response = (QuickFix.FIX44.MarketDataSnapshotFullRefresh)node.GetSnapShot(response);
                    _notifier.NotifyMarket(response, "MD");
                }
            }

            message.Clear();
        }

        public Message MarketDataRequestReject(string mdReqID, string symbol, SessionID sessionID)
        {
            var response = new QuickFix.FIX44.MarketDataRequestReject();
            response.Header.SetField(new SenderCompID(sessionID.TargetCompID));
            response.Header.SetField(new TargetCompID(sessionID.SenderCompID));

            response.SetField(new MDReqID(mdReqID));
            response.SetField(new Text("O símbolo " + symbol + " não é válido."));
            return response;
        }

        public void SecurityRequest(Message message, SessionID sessionID)
        {
            var response = new QuickFix.FIX44.SecurityList();
            response.Header.SetField(new SenderCompID(sessionID.TargetCompID));
            response.Header.SetField(new TargetCompID(sessionID.SenderCompID));

            var reqID = message.GetString(Tags.SecurityReqID);

            response.Set(new SecurityReqID(reqID));
            response.Set(new SecurityResponseID(reqID));
            response.Set(new SecurityRequestResult(SecurityRequestResult.VALID_REQUEST));
            string SecurityIDSource = (_params.IsRF) ? "4" : "8";
            foreach (var i in _instruments)
            {   
                var noGroups = new QuickFix.FIX44.SecurityList.NoRelatedSymGroup();
                noGroups.Set(new Symbol(i.Symbol));
                noGroups.Set(new SecurityIDSource(SecurityIDSource));
                noGroups.Set(new SecurityID(i.SecurityID));
                noGroups.Set(new RoundLot(1));

                if(_params.IsRF)
                {
                    if(!String.IsNullOrEmpty(i.SecurityGroup))
                        noGroups.Set(new SecurityGroup(i.SecurityGroup));
                    
                    lock (_sync)
                    {
                        if(_instrumentsHasOffer.Contains(i.Symbol))
                            noGroups.Set(new HasOffer('Y'));  
                    }

                    if(i.IsTest)
                        noGroups.Set(new IsTest('Y'));

                    if(_params.SendExpirationDate)
                        noGroups.Set(new SecurityValidityTimestamp(i.ExpirationDate));
                }
                    
                response.AddGroup(noGroups);
            }

            _notifier.NotifyMarket(response, "MD");

            message.Clear();
        }

        public void SendSecurityQuotes(string symbol, char type)
        {
            if(_params.IsRF)
            {
                var response = new QuickFix.FIX44.SecurityQuotes();
                response.Set(new Symbol(symbol));
                response.Set(new MDUpdateAction(type));
                _notifier.NotifyMarket(response, symbol);
            }
        }
    }
}
