using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Matching.Secrets;

namespace Matching
{
    public class Service
    {
        private EventWaitHandle _eventWait;
        private MatchingSecrets _matchingSecrets;
        private Params _params;
        private LogManager _logManager;        
        private BookManager _bookManager;
        private FixMarket _marketFix;
        private FixBroker _brokerFix;
        private bool _running;

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logManager.OnLog("Service::OnUnhandledException() >> Error: " + e.ExceptionObject.ToString());
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            _logManager.OnLog("Application finalized");            
            _eventWait.Set();            
        }

        public void Start(EventWaitHandle eventWait)
        {  
            _running = true;

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(this.OnUnhandledException);
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(this.OnProcessExit);

            _eventWait = eventWait;

            _matchingSecrets = new MatchingSecrets();

            /// Cria o objeto de parâmetros             
            _params = new Params(_matchingSecrets.RdsKeys);

            /// Cria o gerenciador de logs
            _logManager = new LogManager(_params);

            /// Cria o servidor FIX de negociação
            _brokerFix = new FixBroker(_params, _logManager);

            /// Cria o servidor FIX de mercado
            _marketFix = new FixMarket(_params, _logManager);

            /// Cria o gerenciador de livros
            _bookManager = new BookManager(_params, _logManager, _brokerFix, _marketFix);

            /// Registra as interfaces
            _brokerFix.SetProvider(_bookManager);
            _marketFix.SetProvider(_bookManager);

            _logManager?.OnLog("Start::LoadMarket...");
            while(_running)
            {
                /// Carrega todo o mercado
                if(_bookManager.LoadMarket())
                    break;

                _logManager?.OnLog("Start:: Something was WRONG . Trying again in 5 sec ...");

                Thread.Sleep(5000);
            }

            _logManager?.OnLog("Start::LoadMarket DONE");

            /// Inicia os servidores
            _marketFix.Start();
            _brokerFix.Start();
        }

        public void Stop()
        {   
            _running = false;
            _marketFix.Stop();
            _brokerFix.Stop();            
            _logManager.Dispose();
            _bookManager.Dispose();
            _marketFix.Dispose();
            _brokerFix.Dispose();
            _params = null;
        }
    }
}
