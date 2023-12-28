using System.Security.Authentication;
using System;
using System.Threading;
using Serilog;
using System.Collections.Generic;
using SLTools.Util.HealthCheck;
using SLTools.Util.Config;

namespace Matching
{
    class Program
    {   
        private static System.Timers.Timer aTimer;
        private static EventWaitHandle _eventWait;
        private static Service _service;
        private static DateTime _startedAt;
        private static HealthCheck _serviceHC;

        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            if(DateTime.Now.Day != _startedAt.Day)
            {
                Log.Information("Restart at {0}", e.SignalTime);

                Log.Information("Stop Service");
                _service.Stop();
                Log.Information("Start Service");
                _service.Start(_eventWait);
                Log.Information("Service working ...");
                _startedAt = DateTime.Now;
            }
        }


        static void Main()
        {

            try
            {

                List<string> envs = new List<string> { "HEALTH_CHECK_PREFIX", "IS_RF", "SCREEN_LOGS",
                "DB_FIRST_HOST", "DB_FIRST_PORT", "DB_FIRST_NAME" ,
                "RDS_KEYS", "SECRETS_API", "BROKER_CFG", "MARKET_CFG", 
                "TRACE_LOGS", "DUMP_PATH", "FIX_DICTIONARY", "FIX_STORE"}; 
                
                DotEnv.Load(envs); 

                string logLevel = Environment.GetEnvironmentVariable("Logging__LogLevel__Sltools");
                
                if (logLevel == "Debug")
                {
                    Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
                }
                else if (logLevel == "Warning")
                {
                    Log.Logger = new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Console().CreateLogger();
                }
                else
                {
                    Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.Console().CreateLogger();
                }

                aTimer = new System.Timers.Timer(120000);
                aTimer.Elapsed += OnTimedEvent;
                aTimer.AutoReset = true;
                aTimer.Enabled = true;

                _eventWait = new ManualResetEvent(false);
                _service = new Service();
                
                _startedAt = DateTime.Now;
                Log.Information("Starting Health Server");
                _serviceHC = new HealthCheck("matching");
                _serviceHC.Start();
                
                Log.Information("Starting Fix Service");

                _service.Start(_eventWait);

                _eventWait.WaitOne();

                _service.Stop();
                _serviceHC.Stop();
                _eventWait.Dispose();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

        }
    }
}
