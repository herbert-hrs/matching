using System;
using Npgsql;
using QuickFix;
using QuickFix.Fields;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace Matching
{
    public class PersistenceManager : IPersistence, IDisposable
    {
        private bool _disposed;
        private bool _running;
        private object _sync;        
        private AutoResetEvent _event;
        private NpgsqlConnection _connFirst;
        private long _counter;
        private ILogManager _log;
        private Thread _thread;
        private Queue<Message> _queue;
        private Params _params;
        private DumpFile _dump;
        string _connectionString;

        public PersistenceManager(Params parameters, ILogManager log)
        {   
            try
            {
                _disposed = false;
                _running = true;
                _sync = new object();
                _event = new AutoResetEvent(false);
                _queue = new Queue<Message>();
                _params = parameters;
                _counter = 0;
                _log = log;
                _dump = new DumpFile(parameters.DumpPath, "offers");

                _connectionString = "Host=" + parameters.DbFirst_Host +
                                    ";Port=" + parameters.DbFirst_Port +
                                    ";Username=" + parameters.DbFirst_User +
                                    ";Password=" + parameters.DbFirst_Pass +
                                    ";Database=" + parameters.DbFirst_Name;
                _connFirst = new NpgsqlConnection(_connectionString);
                //Console.WriteLine(_connectionString);

                _thread = new Thread(Run);
                _thread.Start();
            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::PersistenceManager() >> Error: " + e.Message);
            }
        }

        public  bool LoadInstruments(ConcurrentBag<Instrument> list)
        {

            if (_disposed)
                return false;

            try
            {
                string sqlInstrument;

                if (_params.IsRF)
                {
                    sqlInstrument = " SELECT UPPER(I.INSTRUMENT_ID) AS SYMBOL_ID, " +
                                    "        UPPER(I.DESCRIPTION) AS DESCRIPTION, " +
                                    "        UPPER(I.ISIN_PAPER) AS SECURITY_ID, " +
                                    "        UPPER(I.INSTRUMENT_TYPE) AS INSTRUMENT_TYPE, " +
                                    "        I.EXPIRE_DATE AS EXPIRE_DATE, " +
                                    "        I.SUB_TYPE AS SUB_TYPE, " +
                                    "        I.SECURITY_GROUP AS SECURITY_GROUP " +
                                    " FROM PUBLIC.INSTRUMENT I; ";
                }
                else
                {
                    sqlInstrument = " SELECT UPPER(I.SYMBOL_ID) AS SYMBOL_ID, " +
                                    "        UPPER(I.DESCRIPTION) AS DESCRIPTION, " +
                                    "        UPPER(I.SECURITY_ID) AS SECURITY_ID " +
                                    " FROM PUBLIC.BTC_INSTRUMENT I; ";
                }

                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    using var cmd = new NpgsqlCommand(sqlInstrument, connection);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var i = new Instrument();
                        i.Symbol = reader.GetString(0);
                        i.Description = reader.GetString(1);
                        i.SecurityID = reader.GetString(2);

                        i.IsTest = false;
                        i.IsLinked = false;
                        i.IsDark = false;

                        if (_params.IsRF)
                        {
                            //0 - titulo publico
                            //2 - casado
                            //3 - teste titulo publico
                            //4 - teste casado
                            if(reader.GetString(3) == "2" || reader.GetString(3) == "4")
                                i.IsLinked = true;
                            if(reader.GetString(3) == "3" || reader.GetString(3) == "4")
                                i.IsTest = true;

                            string SubType = reader.GetString(5);
                            if( SubType == "1" || 
                                SubType == "4" || 
                                SubType == "7" || 
                                SubType == "A" || 
                                SubType == "D" || 
                                SubType == "G" || 
                                SubType == "J")
                            {
                                i.IsDark = true;
                            }

                            i.ExpirationDate = reader.GetDateTime(4);
                            i.SecurityGroup = reader.IsDBNull(6) ? "": reader.GetString(6);
                        }


                        list.Add(i);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::LoadInstruments() >> Error: " + e.Message);
                return false;
            }
        }

        public bool LoadMessages(Queue<Message> list)
        {

            if (_disposed)
                return false;

            try
            {
                string sqlMessage = " SELECT MESSAGE FROM MATCHING.MATCH_OFFER" +
                                    " WHERE time >= current_date" +
                                    " ORDER BY COUNT ASC; ";

                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    using var cmd = new NpgsqlCommand(sqlMessage, connection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        Message m = new Message(reader.GetString(0));
                        list.Enqueue(m);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::LoadMessages() >> Error: " + e.Message);
                return false;
            }

        }

        public bool Cleanup()
        {
            if (_disposed)
                return false;

            _log?.OnLog("PersistenceManager::Cleanup " + DateTime.Now.ToString());

            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sqlMessage = " DELETE FROM MATCHING.MATCH_OFFER WHERE time < current_date - 5; ";

                    using var cmd = new NpgsqlCommand(sqlMessage, connection);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::Cleanup()::ConnFirst >> Error: " + e.Message);
                return false;
            }

            return true;

        }

        public bool CleanupTest(List<Instrument> instruments)
        {
            if (_disposed)
                return false;
            
            using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                foreach(Instrument i in instruments)
                {

                    _log?.OnLog("PersistenceManager::CleanupTest " + i.Symbol);

                    try
                    {
                        string sqlMessage = $"DELETE FROM MATCHING.MATCH_OFFER WHERE symbol = '{i.Symbol}'";
                        using var cmd = new NpgsqlCommand(sqlMessage, connection);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception e)
                    {
                        _log?.OnLog("PersistenceManager::CleanupTest():: Error: " + e.Message);
                        return false;
                    }
                }
            }

            return true;

        }

        public bool LoadLastID()
        {
            if (_disposed)
                return false;

            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();
                    string sqlLastID =  " SELECT COALESCE(MAX(ID), 0)" +
                                        " FROM MATCHING.MATCH_OFFER WHERE time >= current_date;";

                    using var cmd = new NpgsqlCommand(sqlLastID, connection);
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        _counter = reader.GetInt64(0) + 1;
                        break;
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::LoadLastID() >> Error: " + e.Message);
                return false;
            }
        }
        
        public void SaveOrder(Message message)
        {
            lock (_sync)
            {
                _queue.Enqueue(new Message(message));
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
                _log = null;
                _counter = 0;
                CheckDump();
                _connFirst.Close();
                _dump = null;

            }            
        }

        protected void Run()
        {
            while (_running)
            {
                try
                {
                    _event.WaitOne();

                    if (!_running)
                        _event.WaitOne();

                    Queue<Message> queue = null;

                    lock (_sync)
                    {
                        if (_queue.Count > 0)
                        {
                            queue = _queue;
                            _queue = new Queue<Message>();
                        }
                    }

                    if (queue != null)
                    {
                        while (queue.Count > 0)
                        {
                            if (!_running)
                                _event.WaitOne();

                            var msg = queue.Dequeue();
                            this.Process(msg);
                            msg.Clear();
                            Thread.Yield();
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    _log?.OnLog("PersistenceManager::Run() >> Interrupted successfully");
                    return;
                }
                catch (Exception e)
                {
                    _log?.OnLog("PersistenceManager::Run() >> Error: " + e.Message);
                }
            }
        }

        private void Process(Message message)
        {
            if (_disposed)
                return;

            string sqlMessage;

            if (_params.IsRF)
            {
                sqlMessage = " INSERT INTO MATCHING.MATCH_OFFER(ID, MESSAGE, TIME, COUNT, SYMBOL) " +
                                " VALUES (DEFAULT, @Message, DEFAULT, @Count, @symbol); ";
            }
            else
            {
                sqlMessage = " INSERT INTO MATCHING.MATCH_OFFER(ID, MESSAGE, TIME, COUNT) " +
                                " VALUES (DEFAULT, @Message, DEFAULT, @Count); ";
            }

            try
            {
                if(!OpenIfClosed(_connFirst, true, 1))
                {
                    _dump.Write(message.ToString());
                    return ;
                }
                

                using var cmd = new NpgsqlCommand(sqlMessage, _connFirst);
                
                cmd.Parameters.AddWithValue("Message", message.ToString());
                
                cmd.Parameters.AddWithValue("Count", ++_counter);

                if(message.IsSetField(Tags.Symbol))
                {
                    string symbol = message.GetString(Tags.Symbol);
                    cmd.Parameters.AddWithValue("symbol", symbol);
                }
                else{
                    cmd.Parameters.AddWithValue("symbol", "");
                }
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                _dump.Write(message.ToString());
                _log?.OnLog("PersistenceManager::Process()::ConnFirst >> Error: " + e.Message);
                return;
            }

        }

        void CheckDump()
        {
            try
            {
                if(_dump.Exists())
                {
                    Queue<string> list = new Queue<string>();
                    _dump.ReadAll(list);

                    while (list.Count > 0)
                    {
                        string m = list.Dequeue();
                        Process(new Message(m));
                    }
                    _dump.RemoveFile();
                }

            }
            catch (Exception e)
            {
                _log?.OnLog("PersistenceManager::CheckDump()::Error: " + e.Message);
            }
        }

        private bool OpenIfClosed(NpgsqlConnection conn, bool isFirst, int maxAttempts=3)
        {
            if(conn.State == System.Data.ConnectionState.Open)
                return true;
        
            int attempts = 0;
            while (conn.State != System.Data.ConnectionState.Open && attempts < maxAttempts)
            {
                if(attempts > 0)
                {
                    _log?.OnLog("PersistenceManager::OpenIfClosed()::" + (isFirst ? "ConnFirst" : "ConnSecond") + " >> Trying " + attempts + " in 5 secounds: ");
                    Thread.Sleep(5000);
                }

                try
                {
                    conn.Open();
                }
                catch (Exception e)
                {
                    _log?.OnLog("PersistenceManager::OpenIfClosed()::" + (isFirst ? "ConnFirst" : "ConnSecond") + " >> Error: " + e.Message);
                }
                attempts += 1;
            }

            if(conn.State != System.Data.ConnectionState.Open)
            {
                _log?.OnLog("PersistenceManager::OpenIfClosed()::" + (isFirst ? "ConnFirst" : "ConnSecond") + " >> WARNING: NOT CONNECTED!!");
                return false;
            }
            else
            {
                _log?.OnLog("PersistenceManager::OpenIfClosed()::" + (isFirst ? "ConnFirst" : "ConnSecond") + " >> Connected!!");
                CheckDump();
                return true;
            }
            
        }
    }
}
