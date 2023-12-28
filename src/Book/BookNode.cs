using QuickFix;
using QuickFix.Fields;
using System;
using System.Collections.Generic;
using Serilog;

namespace Matching
{
    public class BookNode : IDisposable
    {
        protected Instrument _instrument;
        protected readonly Dictionary<string, Trade> _idsTrade;
        protected Trade _previousTrade;
        protected readonly Dictionary<string, BookLine> _ids;
        protected object _sync;
        protected bool _disposed;
        protected readonly Params _params;
        protected Notifier _notifier;
        protected List<BookLine> _listBid;
        protected List<BookLine> _listOffer;
        protected List<Trade> _listTrade;
        BookLine _bestBid;
        BookLine _bestOffer;

        public BookNode(Instrument instrument, Notifier notifier, Params p)
        {
            _sync = new object();
            _disposed = false;
            _listBid = new List<BookLine>();
            _listOffer = new List<BookLine>();
            _listTrade = new List<Trade>();
            _idsTrade = new Dictionary<string, Trade>();
            _instrument = instrument;
            _notifier = notifier;
            _params = p;
            _ids = new Dictionary<string, BookLine>();
            _bestBid = null;
            _bestOffer = null;
        }

        public bool IsEmpty()
        {
            lock (_sync)
            {
                if(_listTrade.Count > 0)
                    return false;

                if(_listOffer.Count > 0)
                    return false;

                if(_listBid.Count > 0)
                    return false;

                return true;
            }
        }

        public void PrintBook()
        {
            Log.Debug($"Instrument {_instrument.Symbol}");
            lock (_sync)
            {
                foreach (var line in _listBid)
                    Log.Debug($"BID pos {line.Position} order id {line.OrderID} tax {line.Tax} qtd {line.LeavesQty} brokerId {line.BrokerID} managerId {line.ManagerID} orderProfile {line.OrderProfile}");
            }
            
            lock (_sync)
            {
                foreach (var line in _listOffer)
                    Log.Debug($"ASK pos {line.Position} order id {line.OrderID} tax {line.Tax} qtd {line.LeavesQty} brokerId {line.BrokerID} managerId {line.ManagerID} orderProfile {line.OrderProfile}");
            }

            lock (_sync)
            {
                foreach (var line in _listTrade)
                    Log.Debug($"TRADE order id {line.UniqueTradeID} tax {line.Tax} pu {line.Quantity} status {line.TradeStatus}");
            }

        }

        protected void SendMarket(Message message, string symbol)
        {
            if(_listBid.Count > 0)
            {
                if(_bestBid == null || _listBid[0].CompareTo(_bestBid) != 0)
                {
                    message.AddGroup(Translator.BuildNodeTopBook(_listBid[0], new MDUpdateAction(MDUpdateAction.CHANGE)));
                    _bestBid = new BookLine(_listBid[0]);
                }
            }
            else
            {
                if(_bestBid != null)
                {
                    message.AddGroup(Translator.BuildNodeTopBook(_bestBid, new MDUpdateAction(MDUpdateAction.DELETE)));
                    _bestBid = null;

                }
            }

            if(_listOffer.Count > 0)
            {
                if(_bestOffer == null || _listOffer[0].CompareTo(_bestOffer) != 0)
                {
                    message.AddGroup(Translator.BuildNodeTopBook(_listOffer[0], new MDUpdateAction(MDUpdateAction.CHANGE)));
                    _bestOffer = new BookLine(_listOffer[0]);
                }
            }
            else
            {
                if(_bestOffer != null)
                {
                    message.AddGroup(Translator.BuildNodeTopBook(_bestOffer, new MDUpdateAction(MDUpdateAction.DELETE)));
                    _bestOffer = null;

                }
            }
            _notifier.NotifyMarket(message, symbol);

        }

        public Message GetSnapShot(Message message)
        {
            if (_disposed)
                return message;

            int counter = 0;
            int position;
            lock (_sync)
            {
                position = 1;
                // Percorre as ofertas tomadoras
                foreach (var line in _listBid)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    group.Set(new MDEntryType(MDEntryType.BID));
                    group.Set(new OrderID(line.OrderID));
                    if(_params.SendBrokerManagerID)
                    {
                        group.Set(new BrokerID(Translator.ShadowID(line.BrokerID, position)));
                        group.Set(new ManagerID(Translator.ShadowID(line.ManagerID, position)));

                    }
                    if(!line._isDark)
                    {
                        group.Set(new MDEntryPx(Translator.ConvertPrice(line.Tax)));
                        group.Set(new MDEntrySize(line.LeavesQty));
                        group.Set(new PU(line.PU));
                    }
                    group.Set(new MDEntryDate(line.ArrivalTime));
                    group.Set(new MDEntryTime(line.ArrivalTime));
                    group.Set(new MDEntryPositionNo(position));
                    message.AddGroup(group);
                    counter++;
                    position++;
                }
            }
            
            lock (_sync)
            {
                position = 1;

                // Percorre as ofertas doadoras
                foreach (var line in _listOffer)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    group.Set(new MDEntryType(MDEntryType.OFFER));
                    group.Set(new OrderID(line.OrderID));
                    
                    if(_params.SendBrokerManagerID)
                    {
                        group.Set(new BrokerID(Translator.ShadowID(line.BrokerID, position)));
                        group.Set(new ManagerID(Translator.ShadowID(line.ManagerID, position)));
                    }

                    if(!line._isDark)
                    {
                        group.Set(new MDEntryPx(Translator.ConvertPrice(line.Tax)));
                        group.Set(new MDEntrySize(line.LeavesQty));
                        group.Set(new PU(line.PU));
                    }
                    group.Set(new MDEntryDate(line.ArrivalTime));
                    group.Set(new MDEntryTime(line.ArrivalTime));
                    group.Set(new MDEntryPositionNo(position));
                    message.AddGroup(group);
                    counter++;
                }
            }

            lock (_sync)
            {
                // Percorre os negócios realizados
                foreach (var line in _listTrade)
                {
                    var group = new QuickFix.FIX44.MarketDataSnapshotFullRefresh.NoMDEntriesGroup();
                    group.Set(new MDEntryType(MDEntryType.TRADE));
                    group.Set(new MDEntryPx(Translator.ConvertPrice(line.Tax)));
                    group.Set(new MDEntrySize(line.Quantity));
                    group.Set(new MDEntryDate(line.TradeTime));
                    group.Set(new MDEntryTime(line.TradeTime));
                    group.Set(new UniqueTradeID(line.UniqueTradeID));
                    if(_params.SendROP)
                    {
                        group.Set(new TradeStatus(line.TradeStatus));
                        group.Set(new OrigTrade(line.OrigTrade));
                    }
                    group.Set(new PU(line.PU));
                    message.AddGroup(group);
                    counter++;
                }
            }

            if (counter == 0)
                message.SetField(new NoMDEntries(0));

            return message;
        }

        private static bool CheckMatch(BookLine bid, BookLine offer, bool isRF, bool isBookByPU)
        {
            if(isRF){
                if(isBookByPU)
                {
                    // Na Renda Fixa, se PU da oferta tomadora for menor que da oferta doadora, termina o fluxo
                    if (bid.PU < offer.PU)
                        return false;
                }
                else
                {
                    if (bid.Tax > offer.Tax)
                        return false;
                }
            }
            else{
                // No Aluguel, se taxa da oferta tomadora for menor que oferta doadora, termina o fluxo
                if (bid.Tax < offer.Tax)
                    return false;
            }

            return true;
        }

        private bool Match(char side, DateTime transactTrade)
        {
            if (_disposed)
                return false;

            // Se não tiver ofertas em ambas as direções, termina o fluxo
            if ((_listBid.Count <= 0) || (_listOffer.Count <= 0))
                return false;

            var bid = _listBid[0];
            var offer = _listOffer[0];

            // Define a posição de cabeça para as ofertas
            bid.Position = 1;
            offer.Position = 1;

            if(!CheckMatch(bid, offer, _params.IsRF, _params.IsBookByPU))
                return false;

            int qty = bid.LeavesQty < offer.LeavesQty ? bid.LeavesQty : offer.LeavesQty;
            string uniqueTradeID = Generator.GetTradeID();
            bool offerIsAttack = (bid.ArrivalTime < offer.ArrivalTime);
            decimal tax = offerIsAttack ? Translator.ConvertPrice(bid.Tax) : Translator.ConvertPrice(offer.Tax);
            decimal pu = offerIsAttack ? Translator.ConvertPrice(bid.PU) : Translator.ConvertPrice(offer.PU);

            // Consome as partes com melhores preços
            bid.Trade(qty, tax, pu, uniqueTradeID, offerIsAttack, transactTrade, 0);
            offer.Trade(qty, tax, pu, uniqueTradeID, offerIsAttack, transactTrade, 0);

            // Cria o negócio realizado
            var trade = new Trade(_instrument, qty, tax, pu, uniqueTradeID, transactTrade, TradeStatus.CONFIRMED, OrigTrade.BOOK);

            // Adiciona à lista de negócios realizados
            _idsTrade.Add(uniqueTradeID, trade);
            _listTrade.Add(trade);

            _notifier.NotifyBroker(Translator.BrokerReport(bid), bid.Symbol);
            _notifier.NotifyBroker(Translator.BrokerReport(offer), offer.Symbol);

            var message = new QuickFix.FIX44.MarketDataIncrementalRefresh();
            message.Set(new TradeDate(transactTrade.ToString("yyyyMMdd")));

            if (side == Side.BUY)
            {
                message.AddGroup(Translator.BuildNode(offer, offer.Action));
                message.AddGroup(Translator.BuildNode(bid, bid.Action));
                message.AddGroup(Translator.BuildNode(trade, new MDUpdateAction(MDUpdateAction.NEW)));
            }
            else
            {
                message.AddGroup(Translator.BuildNode(bid, bid.Action));
                message.AddGroup(Translator.BuildNode(offer, offer.Action));
                message.AddGroup(Translator.BuildNode(trade, new MDUpdateAction(MDUpdateAction.NEW)));
            }


            if (bid.Status.Obj == OrderStatus.FILLED)
            {
                _listBid.Remove(bid);
                bid.Dispose();
            }

            if (offer.Status.Obj == OrderStatus.FILLED)
            {
                _listOffer.Remove(offer);
                offer.Dispose();
            }

            SendMarket(message, trade.Symbol);
            

            return true;
        }

        public virtual bool InsertOrder(Message message)
        {
            if (_disposed)
                return false;

            var quantity = message.GetString(Tags.Quantity);

            if (Convert.ToInt32(quantity) <= 0)
            {
                _notifier?.NotifyLog("BookNode::InsertOrder() >> Error: " + quantity  + " is not a valid quantity!");
                RejectOrder(message);

                return false;
            }

            if (message.IsSetField(Tags.SPU))
            {
                var pu = message.GetString(Tags.SPU);
                if (Convert.ToDecimal(pu) <= 0)
                {
                    _notifier?.NotifyLog("BookNode::InsertOrder() >> Error: " + pu  + " is not a valid pu!");
                    RejectOrder(message);
                    return false;
                }
            }

            var orderID = message.GetString(Tags.OrderID);
            if (!_ids.ContainsKey(orderID))
            {
                DateTime transactTime = DateTime.Now;
                if(message.IsSetField(Tags.TransactTime))
                    transactTime = message.GetDateTime(Tags.TransactTime);

                BookLine line = new BookLine(message, _instrument, _params);


                lock (_sync)
                {
                    if (line.Side == Side.BUY)
                    {
                        _listBid.Add(line);
                        _listBid.Sort();
                        line.Position = _listBid.IndexOf(line) + 1;

                    }
                    else
                    {

                        _listOffer.Add(line);
                        _listOffer.Sort();
                        line.Position = _listOffer.IndexOf(line) + 1;

                    }

                    line.SecondaryOrderID = Generator.GetSecOrderID();
                    
                    _ids.Add(orderID, line);
                    _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                    SendMarket(Translator.MarketReport(line, new MDUpdateAction(MDUpdateAction.NEW)), line.Symbol);
                    
                    while (this.Match(line.Side, transactTime)) ;
                }                
                return true;
            }
            else
            {
                _notifier?.NotifyLog("BookNode::InsertOrder() >> Error: OrderID [" + orderID + "] already exists!");
                RejectOrder(message);
                return false;
            }
        }
        

        public virtual bool ReplaceOrder(Message message)
        {
            if (_disposed)
                return false;

            var quantity = message.GetString(Tags.Quantity);

            if (Convert.ToInt32(quantity) <= 0)
            {
                _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: " + quantity  + " is not a valid quantity!");
                RejectOrder(message);

                return false;
            }

            if (message.IsSetField(Tags.SPU))
            {
                var pu = message.GetString(Tags.SPU);
                if (Convert.ToDecimal(pu) <= 0)
                {
                    _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: " + pu  + " is not a valid pu!");
                    RejectOrder(message);
                    return false;
                }
            }

            var orderID = message.GetString(Tags.OrderID);

            if (_ids.ContainsKey(orderID))
            {
                lock (_sync)
                {
                    DateTime transactTime = DateTime.Now;
                    if(message.IsSetField(Tags.TransactTime))
                        transactTime = message.GetDateTime(Tags.TransactTime);

                    var line = _ids[orderID];
                    var side = message.GetString(Tags.Side);
                    int old_position;
                    int new_position;

                    if(side[0] != line.Side)
                    {
                        _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] wrong Side");
                        RejectOrder(message);
                        return false;
                    }

                    // Ordena para pegar a posição atual da oferta
                    if (line.Side == Side.BUY)
                    {
                        old_position = _listBid.IndexOf(line) + 1;
                    }
                    else
                    {
                        old_position = _listOffer.IndexOf(line) + 1;
                    }

                    if (old_position == 0)
                    {
                        _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] is not in the book!");
                        RejectOrder(message);
                        return false;
                    }


                    line.Position = old_position;
                    var old = new BookLine(line);

                    if (!line.Replace(message))
                    {
                        _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] invalid Type");
                        RejectOrder(message);
                        old.Dispose();
                        return false;
                    }

                    // Ordena para pegar a posição depois da edição
                    if (line.Side ==Side.BUY)
                    {
                        _listBid.Sort();
                        new_position = _listBid.IndexOf(line) + 1;
                    }
                    else
                    {
                        _listOffer.Sort();
                        new_position = _listOffer.IndexOf(line) + 1;
                    }

                    line.Position = new_position;
                    line.SecondaryOrderID = Generator.GetSecOrderID();

                    _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                    Message msgOut = null;

                    if (old_position != new_position)
                    {
                        //envia deleção da oferta (compra/venda)
                        msgOut = Translator.MarketReport(old, new MDUpdateAction(MDUpdateAction.DELETE));

                        //envia nova oferta (compra/venda)
                        msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.NEW)));
                    }
                    else
                    {
                        msgOut = Translator.MarketReport(line, new MDUpdateAction(MDUpdateAction.CHANGE));
                    }
                    SendMarket(msgOut, line.Symbol);


                    old.Dispose();
                    while (this.Match(line.Side, transactTime)) ;

                    return true;
                }
            }
            else
            {
                RejectOrder(message);
                _notifier?.NotifyLog("BookNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] not identified!");
                return false;
            }
        }

        public bool CancelOrder(Message message)
        {
            if (_disposed)
                return false;

            var orderID = message.GetString(Tags.OrderID);

            if (_ids.ContainsKey(orderID))
            {
                lock (_sync)
                {
                    var side = message.GetString(Tags.Side);

                    BookLine line = _ids[orderID];

                    if(side[0] != line.Side)
                    {
                        _notifier?.NotifyLog("BookNode::CancelOrder() >> Error: OrderID [" + orderID + "] wrong Side");
                        RejectOrder(message);
                        return false;
                    }

                    if (!line.Cancel(message))
                    {
                        _notifier?.NotifyLog("BookNode::CancelOrder() >> Error: OrderID [" + orderID + "]  is not in the book");
                        RejectOrder(message);
                        return false;
                    }

                    if (line.Side == Side.BUY)
                    {
                        line.Position = _listBid.IndexOf(line) + 1;
                        _listBid.Remove(line);
                        _listBid.Sort();
                    }
                    else
                    {
                        line.Position = _listOffer.IndexOf(line) + 1;
                        _listOffer.Remove(line);
                        _listOffer.Sort();
                    }

                    _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);
                    
                    SendMarket(Translator.MarketReport(line, new MDUpdateAction(MDUpdateAction.DELETE)), line.Symbol);
                    line.Dispose();
                    return true;
                }                
            }
            else
            {
                RejectOrder(message);
                _notifier?.NotifyLog("BookNode::CancelOrder() >> Error: OrderID [" + orderID + "] not identified!");
                return false;
            }
        }

        public bool CrossTrade(Message message)
        {
            if (_disposed)
                return false;

            var quantity = message.GetString(Tags.Quantity);
            if (Convert.ToInt32(quantity) <= 0)
            {
                _notifier?.NotifyLog("BookNode::CrossTrade() >> Error: " + quantity + " is not a valid quantity!");
                RejectOrder(message);

                return false;
            }

            if (message.IsSetField(Tags.SPU))
            {
                var pu = message.GetString(Tags.SPU);
                if (Convert.ToDecimal(pu) <= 0)
                {
                    _notifier?.NotifyLog("BookNode::CrossTrade() >> Error: " + pu  + " is not a valid pu!");
                    RejectOrder(message);
                    return false;
                }
            }

            lock (_sync)
            {
                var orderBid = message.GetString(Tags.OrderBid);
                var orderOffer = message.GetString(Tags.OrderOffer);
                var qty = message.GetInt(Tags.Quantity);
                var tax = Translator.ExtractPrice(message.GetString(Tags.STax));
                decimal pu = 0;

                if (message.IsSetField(Tags.SPU))
                    pu = Translator.ExtractPrice(message.GetString(Tags.SPU));

                if(orderBid == orderOffer)
                {
                   _notifier?.NotifyLog("BookNode::CrossTrade() >>OrderBid [" + orderBid + "] and OrderOffer are the same!");
                    RejectOrder(message);
                    return false; 
                }


                if (_ids.ContainsKey(orderBid))
                {
                    _notifier?.NotifyLog("BookNode::CrossTrade() >> Error: OrderBid [" + orderBid + "] already exists!");
                    RejectOrder(message);
                    return false;
                }

                if (_ids.ContainsKey(orderOffer))
                {
                    _notifier?.NotifyLog("BookNode::CrossTrade() >> Error: OrderOffer [" + orderOffer + "] already exists!");
                    RejectOrder(message);
                    return false;
                }

                string uniqueTradeID = Generator.GetTradeID();

                DateTime transactTrade = DateTime.Now;
                if(message.IsSetField(Tags.TransactTime))
                    transactTrade = message.GetDateTime(Tags.TransactTime);

                var bid = new BookLine(message, _instrument, '1', uniqueTradeID, transactTrade, _params);

                _ids.Add(orderBid, bid);
                var offer = new BookLine(message, _instrument, '2', uniqueTradeID, transactTrade, _params);
                _ids.Add(orderOffer, offer);

                char tradeStatus = TradeStatus.PENDING;
                if (message.IsSetField(Tags.TradeStatus))
                    tradeStatus = message.GetChar(Tags.TradeStatus);

                var trade = new Trade(_instrument, qty, tax, pu, uniqueTradeID, transactTrade, tradeStatus, OrigTrade.ROP);
                _idsTrade.Add(uniqueTradeID, trade);
                _listTrade.Add(trade);

                _notifier.NotifyMarket(Translator.MarketReport(trade, new MDUpdateAction(MDUpdateAction.NEW)), trade.Symbol);
                _notifier.NotifyBroker(Translator.BrokerReport(bid), bid.Symbol);
                _notifier.NotifyBroker(Translator.BrokerReport(offer), offer.Symbol);

                bid.Dispose();
                offer.Dispose();
            }   
            return true;         
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

        public Trade GetPreviousTrade()
        {
            for(int i = _listTrade.Count - 1; i >=0; i--)
            {
                if(_listTrade[i].TradeStatus == TradeStatus.CONFIRMED || 
                    _listTrade[i].TradeStatus == TradeStatus.PENDING)
                    return _listTrade[i];
            }

            return null;
        }

        public void CancelTrade(Message message)
        {
            if (_disposed)
                return;

            var tradeID = message.GetString(Tags.UniqueTradeID);

            if (!_idsTrade.ContainsKey(tradeID))
            {
                _notifier?.NotifyLog("BookNode::CancelTrade() >> Error: TradeID [" + tradeID + "] not exists");
                RejectOrder(message);
                return;
            }

            lock (_sync)
            {
                Trade trade = _idsTrade[tradeID];
                trade.TradeStatus = TradeStatus.CANCELED;

                _notifier.NotifyMarket(
                    Translator.MarketReport(trade, new MDUpdateAction(MDUpdateAction.CHANGE)), 
                    trade.Symbol
                );

                Trade previousTrade = GetPreviousTrade();
                if(previousTrade != null)
                {
                    _notifier.NotifyMarket(
                        Translator.MarketReport(
                            previousTrade, 
                            new MDUpdateAction(MDUpdateAction.CHANGE), MDEntryType.PREVIOUS_TRADE
                        ), 
                        previousTrade.Symbol
                    );
                }

            }
        }

        public void UpdateTrade(Message message)
        {
            if (_disposed)
                return;

            string tradeID = message.GetString(Tags.UniqueTradeID);

            if (!_idsTrade.ContainsKey(tradeID))
            {
                _notifier?.NotifyLog("BookNode::UpdateTrade() >> Error: TradeID [" + tradeID + "] not exists");
                return;
            }

            char tradeStatus = message.GetChar(Tags.TradeStatus);

            lock (_sync)
            {
                var trade = _idsTrade[tradeID];
                trade.TradeStatus = tradeStatus;
                _notifier.NotifyMarket(Translator.MarketReport(trade, new MDUpdateAction(MDUpdateAction.CHANGE)), trade.Symbol);
            
                if(tradeStatus == TradeStatus.REJECTED)
                {
                    Trade previousTrade = GetPreviousTrade();
                    if(previousTrade != null)
                    {
                        _notifier.NotifyMarket(
                            Translator.MarketReport(
                                previousTrade, 
                                new MDUpdateAction(MDUpdateAction.CHANGE), MDEntryType.PREVIOUS_TRADE
                            ), 
                            previousTrade.Symbol
                        );
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _listBid.Clear();
                _listOffer.Clear();
                _listTrade.Clear();
                _idsTrade.Clear();
                _instrument = null;
                _notifier = null;
                _ids.Clear();
                _sync = null;
            }
        }

        public void Clear()
        {
            _listBid.Clear();
            _listOffer.Clear();
            _listTrade.Clear();
            _idsTrade.Clear();
            _ids.Clear();
        }
    }
}
