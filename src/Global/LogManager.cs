using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Serilog;

namespace Matching
{
    public class LogManager : ILogManager, IDisposable
    {
        private bool _disposed;
        private bool _running;
        private object _sync;
        private readonly Params _params;
        private readonly StringBuilder _builder;
        private StreamWriter _log;
        private readonly AutoResetEvent _event;
        private Queue<string> _queue;
        private Thread _thread;

        public LogManager(Params parameters)
        {
            _disposed = false;
            _running = true;
            _params = parameters;
            _sync = new object();
            _log = null;
            _builder = new StringBuilder();
            _event = new AutoResetEvent(false);
            _queue = new Queue<string>();
            _thread = new Thread(Run);
            _thread.Start();
            Thread.Sleep(200);
            this.OnLog("Application initialized.");
        }

        public void OnLog(string msg)
        {
            if (_disposed)
                return;

            lock (_sync)
            {
                _queue.Enqueue(msg);
            }
            _event.Set();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _running = false;
                _thread.Interrupt();
                _thread.Join(200);
                _sync = null;
                _event.Dispose();
                _queue.Clear();
                _builder.Clear();
                _thread = null;
                _log = null;
            }
        }

        private void OpenFile()
        {
            if (!Directory.Exists(_params.TraceLogs))
            {
                Directory.CreateDirectory(_params.TraceLogs);
            }

            _builder.Clear();
            _builder.Append(_params.TraceLogs).Append(Path.DirectorySeparatorChar);
            _builder.Append("TraceLogs").Append("_");
            _builder.Append(DateTime.Now.ToString("yyyy-MM-dd")).Append(".log");
            string l_path = _builder.ToString();
            _log = new StreamWriter(l_path, true);
        }

        private void CloseFile()
        {
            if (_log != null)
            {
                _log.Flush();
                _log.Close();
                _log.Dispose();
                _log = null;
            }
        }

        private void Run()
        {
            while (_running)
            {
                try
                {
                    _event.WaitOne();

                    if (!_running)
                        _event.WaitOne();

                    Queue<string> queue = null;

                    lock (_sync)
                    {
                        if (_queue.Count > 0)
                        {
                            queue = _queue;
                            _queue = new Queue<string>();
                        }
                    }

                    if (queue != null)
                    {
                        while (queue.Count > 0)
                        {
                            if (!_running)
                                _event.WaitOne();

                            string msg = queue.Dequeue();
                            

                            if(_params.ScreenLogs)
                            {
                                Log.Information(DateTime.Now.ToString("yyyyMMdd-HH:mm:ss.fff") + ": " + msg);
                            }
                            else
                            {
                                OpenFile();
                                _log.WriteLine(DateTime.Now.ToString("yyyyMMdd-HH:mm:ss.fff") + ": " + msg);
                                CloseFile();
                            }
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {   
                    return;
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }
    }
}