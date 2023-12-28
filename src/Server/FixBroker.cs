using System;
using QuickFix;
using QuickFix.Fields;

namespace Matching
{
    public class FixBroker : IApplication, IBrokerProvider, IDisposable
    {
        #region Atributos de Classe
        private bool _disposed;
        private object _sync;
        private Params _params;
        private LogManager _logManager;
        private IBrokerBook _book;
        #endregion

        #region Atributos FIX
        private ThreadedSocketAcceptor _acceptor;
        private SessionID _sessionID;
        #endregion

        public FixBroker(Params parameters, LogManager logManager)
        {
            _disposed = false;
            _sync = new object();
            _params = parameters;
            _logManager = logManager;
        }

        public void SetProvider(IBrokerBook book)
        {
            _book = book;
        }

        public void Start()
        {
            try
            {
                SessionSettings settings = new SessionSettings(_params.BrokerCfg);
                IMessageStoreFactory storeFactory = null;

                if(_params.FixStore == "File")
                    storeFactory = new FileStoreFactory(settings);
                else
                    storeFactory = new MemoryStoreFactory();

                FileLogFactory logFactory = null;
                if(_params.FixBrokerLogs)
                    logFactory = new FileLogFactory(settings);
                    
                MessageFactory messageFactory = new MessageFactory();
                _acceptor = new ThreadedSocketAcceptor(this, storeFactory, settings, logFactory, messageFactory);
                _acceptor.Start();
            }
            catch (Exception e)
            {
                _logManager?.OnLog("BrokerFIX::Start() >> Error: " + e.Message);
                throw new Exception(e.Message);
            }
        }

        public void Stop()
        {
            _acceptor?.Stop();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _acceptor?.Dispose();
                _sessionID = null;
                _disposed = true;
                _sync = null;
                _params = null;
                _logManager = null;
                _book = null;
            }            
        }

        public void NotifyBroker(Message message)
        {
            lock (_sync)
            {
                if (_sessionID == null)
                    return;
            }
           // _logManager?.OnLog(message.ToString());
            
            Session.SendToTarget(message, _sessionID);
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            // Dump
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            
           // _logManager?.OnLog(message.ToString());

            lock (_sync)
            {
                if ((_sessionID == null) || (_sessionID != sessionID))
                {
                    _logManager?.OnLog("BrokerFIX::FromApp() >> Error: User not connected.");
                    return;
                }
            }

            try
            {
                string msgType = message.Header.GetString(Tags.MsgType);


                if (msgType == MsgType.MATCH_NEW_ORDER_SINGLE)
                {
                    message.SetField(new TransactTime(DateTime.Now));

                    _book.InsertOrder(message, true);
                }
                else if (msgType == MsgType.MATCH_NEW_ORDER_CROSS)
                {
                    message.SetField(new TransactTime(DateTime.Now));

                    _book.CrossTrade(message, true);
                }
                else if (msgType == MsgType.MATCH_ORDER_REPLACE_REQUEST)
                {
                    message.SetField(new TransactTime(DateTime.Now));

                    _book.ReplaceOrder(message, true);
                }
                else if (msgType == MsgType.MATCH_ORDER_CANCEL_REQUEST)
                {
                    _book.CancelOrder(message, true);
                }
                else if (msgType == MsgType.MATCH_TRADE_CANCEL_REQUEST)
                {
                    _book.CancelTrade(message, true);
                }
                else if (msgType == MsgType.MATCH_TRADE_UPDATE_REQUEST)
                {
                    _book.UpdateTrade(message, true);
                }
                else if (msgType == MsgType.MATCH_INSTRUMENT_RELOAD)
                {
                    _book.ReloadMarket(message);
                }
                else if (msgType == MsgType.MARKET_DATA_INSTRUMENT_TEST_CLEANUP)
                {
                    _book.CleanupTest(message);
                }
            }
            catch (Exception e)
            {
                _logManager?.OnLog("BrokerFIX::FromApp() >> Error: " + e.Message);
            }
        }
        
        public void OnCreate(SessionID sessionID)
        {
            // Dump
            _logManager?.OnLog("BrokerFIX::Created() >> " + sessionID.ToString());

        }

        public void OnLogon(SessionID sessionID)
        {
            lock (_sync)
            {
                _sessionID = sessionID;
                _logManager?.OnLog("BrokerFIX::OnLogon() >> Info: [" + sessionID.SenderCompID + " connected on " + sessionID.TargetCompID + "]");
            }
        }

        public void OnLogout(SessionID sessionID)
        {
            lock (_sync)
            {
                _sessionID = null;
                _logManager?.OnLog("BrokerFIX::OnLogout() >> Info: [" + sessionID.SenderCompID + " disconnected from " + sessionID.TargetCompID + "]");
            }
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            // Dump
        }

        public void ToApp(Message message, SessionID sessionID)
        {
          //  _logManager?.OnLog(message.ToString());

            // Dump
        }

        public void OnConnect(SocketReader sr, Message message, SessionID sessionID) 
        {
            // Dump
        }

    }
}
