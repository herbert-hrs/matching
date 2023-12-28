using System;
using QuickFix;

namespace Matching
{
    public class Notifier : IDisposable
    {
        private object _sync;
        private bool _disposed;
        private IBrokerProvider _broker;
        private IMarketProvider _market;
        private ILogManager _log;

        public Notifier(IBrokerProvider broker, IMarketProvider market, ILogManager log)
        {
            _sync = new object();
            _disposed = false;
            _log = log;
            _broker = broker;
            _market = market;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _log = null;
                _broker = null;
                _market = null;
                _sync = null;
            }
        }

        public void NotifyBroker(Message message, string symbol)
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _broker.NotifyBroker(message);

            }            
        }

        public void NotifyMarket(Message message, string symbol)
        {
            if (_disposed)
                return;
            lock (_sync)
            {
                if(symbol.Equals("MD")){
                    _market.NotifyMarket(message);
                }
                else{
                    _market.NotifyAllMarket(message);
                }
            }            
        }

        public void NotifyLog(string msg)
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _log?.OnLog(msg);
            }            
        }
    }
}
