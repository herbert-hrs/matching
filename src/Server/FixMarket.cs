using System;
using QuickFix;
using QuickFix.Fields;
using System.Collections.Generic;

namespace Matching
{
    public class FixMarket : IApplication, IMarketProvider, IDisposable
    {
        #region Atributos de Classe
        private bool _disposed;
        private object _sync;
        private Params _params;
        private LogManager _logManager;
        private IMarketBook _book;
        #endregion

        #region Atributos FIX
        private ThreadedSocketAcceptor _acceptor;
        private HashSet<SessionID> _sessionIDs = new HashSet<SessionID>();
        private HistoricManager _historic;
        private bool _started = false;

        #endregion

        public FixMarket(Params parameters, LogManager logManager)
        {
            _disposed = false;
            _sync = new object();
            _params = parameters;
            _logManager = logManager;

        }

        public void SetProvider(IMarketBook book)
        {
            _book = book;
        }

        public void Start()
        {
            try
            {
                SessionSettings settings = new SessionSettings(_params.MarketCfg);

                _historic = null;
                if(_params.IsRF)
                    _historic = new HistoricManager(_params, _logManager);

                IMessageStoreFactory storeFactory = null;

                if(_params.FixStore == "File")
                    storeFactory = new FileStoreFactory(settings);
                else
                    storeFactory = new MemoryStoreFactory();
                    
                FileLogFactory logFactory = null;
                if(_params.FixMarketLogs)
                    logFactory = new FileLogFactory(settings);

                MessageFactory messageFactory = new MessageFactory();
                _acceptor = new ThreadedSocketAcceptor(this, storeFactory, settings, logFactory, messageFactory);
                _acceptor.Start();
                _started = true;

            }
            catch (Exception e)
            {
                _logManager?.OnLog("MarketFIX::Start() >> Error: " + e.Message);
                throw new Exception(e.Message);
            }
        }

        public void Stop()
        {
            _acceptor?.Stop();
            _started = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _acceptor?.Dispose();
                _disposed = true;
                _sync = null;
                _params = null;
                _logManager = null;
                _book = null;
                _sessionIDs = null;
                if(_historic != null)
                    _historic.Dispose();
            }
        }

        public void NotifyMarket(Message message)
        {
            if(_started)
            {
                //caso a sessão tenha sido removida antes do envio da mensagem
                // o próprio quickfix gera erro dizendo que sessão não existe
                Session.SendToTarget(message);
            }
            
        }

        public void NotifyAllMarket(Message message)
        {
            if(_started)
            {
                if (message.Header.GetString(Tags.MsgType) == MsgType.MARKET_DATA_INCREMENTAL_REFRESH)
                {
                    if(_historic != null)
                        _historic.Save(message);
                }

                SessionID[] arrayIDs;
                lock (_sync)
                {
                    if (_sessionIDs.Count == 0)
                        return;

                    arrayIDs = new SessionID[_sessionIDs.Count];
                    _sessionIDs.CopyTo(arrayIDs);
                }
                
                foreach (SessionID sessionID in arrayIDs)
                {
                    Session.SendToTarget(message, sessionID);
                }

                arrayIDs = null;
            }
            
        }

        public void FromAdmin(Message message, SessionID sessionID)
        {
            // Dump
            
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            lock (_sync)
            {
                if(!_sessionIDs.Contains(sessionID))
                {
                    _logManager?.OnLog("MarketFIX::FromApp() >> Error: User not connected: " + sessionID.ToString());
                    return;
                }
            }

            try
            {

                string msgType = message.Header.GetString(Tags.MsgType);
                SessionID sessionClientID = message.GetSessionID(message);


                if (msgType == MsgType.SECURITY_LIST_REQUEST)
                {
                    _book.SecurityRequest(message, sessionClientID);
                }
                else if (msgType == MsgType.MARKET_DATA_REQUEST)
                {
                    _book.MarketDataRequest(message, sessionClientID);
                }
            }
            catch (Exception e)
            {
                _logManager?.OnLog("MarketFIX::FromApp() >> Error: " + e.Message);
            }
        }

        public void OnCreate(SessionID sessionID)
        {
            _logManager?.OnLog("MarketFIX::Created() >> " + sessionID.ToString());
            // Dump
        }

        public void OnLogon(SessionID sessionID)
        {
            lock (_sync)
            {
                _sessionIDs.Add(sessionID);

                _logManager?.OnLog("MarketFIX::OnLogon() >> Info: [" + sessionID.SenderCompID + " connected on " + sessionID.TargetCompID + "]");
            }
        }

        public void OnLogout(SessionID sessionID)
        {
            lock (_sync)
            {
                 _sessionIDs.Remove(sessionID);
                _logManager?.OnLog("MarketFIX::OnLogout() >> Info: [" + sessionID.SenderCompID + " disconnected from " + sessionID.TargetCompID + "]");
            }
        }

        public void ToAdmin(Message message, SessionID sessionID)
        {
            // Dump
        }

        public void ToApp(Message message, SessionID sessionID)
        {
            // Dump
        }

        public void OnConnect(SocketReader sr, Message message, SessionID sessionID) 
        {
            // Dump
        }
    }
}
