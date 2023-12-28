using System;
using Npgsql;
using QuickFix;
using QuickFix.DataDictionary;
using QuickFix.Fields;
using System.Collections.Generic;
using System.Threading;

namespace Matching
{
    public class HistoricManager : IDisposable
    {
        private bool _disposed;
        private bool _running;
        private object _sync;        
        private AutoResetEvent _event;
        private NpgsqlConnection _connHist;
        private NpgsqlConnection _connFirst;
        private ILogManager _log;
        private Thread _thread;
        private Queue<Message> _queue;
        private DumpFile _dump_hist;
        private DumpFile _dump_detail;
        private Params _params;
        DataDictionary _dd;

        public HistoricManager(Params parameters, ILogManager log)
        {   
            try
            {
                _disposed = false;
                _running = true;
                _sync = new object();
                _event = new AutoResetEvent(false);
                _queue = new Queue<Message>();
                _params = parameters;
                _log = log;
                _dd = new DataDictionary(_params.FixDictionary);
                _dump_hist = new DumpFile(parameters.DumpPath, "historic");
                _dump_detail = new DumpFile(parameters.DumpPath, "detail");


                string buildFirst = "Host=" + parameters.DbFirst_Host +
                                    ";Port=" + parameters.DbFirst_Port +
                                    ";Username=" + parameters.DbFirst_User +
                                    ";Password=" + parameters.DbFirst_Pass +
                                    ";Database=" + parameters.DbFirst_Name;
                
                _connFirst = new NpgsqlConnection(buildFirst);


                _connHist = null;
                if(_params.SaveHistoryDetail)
                {
                    string buildHist = "Host=" + parameters.DbHist_Host +
                                        ";Port=" + parameters.DbHist_Port +
                                        ";Username=" + parameters.DbHist_User +
                                        ";Password=" + parameters.DbHist_Pass +
                                        ";Database=" + parameters.DbHist_Name;

                    _connHist = new NpgsqlConnection(buildHist);
                }

                _thread = new Thread(Run);
                _thread.Start();
            }
            catch (Exception e)
            {
                _log?.OnLog("HistoricManager::HistoricManager() >> Error: " + e.Message);
            }
        }
        
        public void Save(Message message)
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
                CheckDump(_dump_hist, SaveHistory);
                CheckDump(_dump_detail, SaveDatailHistory);
                if(_connHist != null)
                    _connHist.Close();
                _dump_hist = null;
                _dump_detail = null;
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
                    _log?.OnLog("HistoricManager::Run() >> Interrupted successfully");
                    return;
                }
                catch (Exception e)
                {
                    _log?.OnLog("HistoricManager::Run() >> Error: " + e.Message);
                }
            }
        }
        private bool SaveHistory(Message message)
        {
            string sqlMessage = "INSERT INTO MATCHING.INCREMENTAL_HISTORY(ID, MESSAGE, TIME) " +
                                "VALUES (DEFAULT, @msg, DEFAULT);";

            try
            {
                if(!OpenIfClosed(_connFirst, 1))
                {
                    _dump_hist.Write(message.ToString());
                    return false;
                }

                using var cmd = new NpgsqlCommand(sqlMessage, _connFirst);
                cmd.Parameters.AddWithValue("msg", message.ToString());
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                _dump_hist.Write(message.ToString());
                _log?.OnLog("HistoricManager::SaveHistory()::ConnHist >> Error: " + e.Message);
                return false;
            }

        }

        private bool SaveDatailHistory(Message message)
        {
            if(_connHist == null)
                return false;
                
            NpgsqlTransaction transaction = null;
            try
            {
                int numGrp = message.GetInt(Tags.NoMDEntries);

                if(!OpenIfClosed(_connHist, 1))
                {
                    _dump_detail.Write(message.ToString());
                    return false;
                }

                transaction = _connHist.BeginTransaction();

                for (int i = 1; i <= numGrp; i++)
                {
                    Group group = message.GetGroup(i, Tags.NoMDEntries);

                    string sqlMessage = " INSERT INTO HISTAPI.TT_DETAIL_HISTORY(ID, SYMBOL_ID, ENTRY_ID, QUANTITY, TAX, PU, UPDATE_ACTION, ENTRY_TYPE, ENTRY_DATE, ENTRY_TIME, POSITION, STATUS, ORIG) " +
                                        " VALUES (DEFAULT, @symbol, @entry_id, @quantity, @tax, @pu, @action, @type, @date, @time, @position, @status, @orig); ";

                        
                    using var cmd = new NpgsqlCommand(sqlMessage, _connHist);

                    cmd.Transaction = transaction;

                    cmd.Parameters.AddWithValue("symbol", group.GetString(Tags.Symbol));
                    if(group.IsSetField(Tags.OrderID)){
                        cmd.Parameters.AddWithValue("entry_id", group.GetString(Tags.OrderID));
                    }
                    else if(group.IsSetField(Tags.UniqueTradeID)){
                        cmd.Parameters.AddWithValue("entry_id", group.GetString(Tags.UniqueTradeID));
                    }
                    else{
                        cmd.Parameters.AddWithValue("entry_id", "");
                    }

                    if(group.IsSetField(Tags.MDEntrySize))
                        cmd.Parameters.AddWithValue("quantity", group.GetInt(Tags.MDEntrySize));
                    else
                        cmd.Parameters.AddWithValue("quantity", DBNull.Value);

                    if(group.IsSetField(Tags.MDEntryPx))
                        cmd.Parameters.AddWithValue("tax", group.GetDecimal(Tags.MDEntryPx));
                    else
                        cmd.Parameters.AddWithValue("tax", DBNull.Value);

                    if(group.IsSetField(Tags.PU))
                        cmd.Parameters.AddWithValue("pu", group.GetDecimal(Tags.PU));
                    else
                        cmd.Parameters.AddWithValue("pu", DBNull.Value);

                    cmd.Parameters.AddWithValue("type", group.GetString(Tags.MDEntryType));
                    cmd.Parameters.AddWithValue("action", group.GetString(Tags.MDUpdateAction));
                    if(group.IsSetField(Tags.MDEntryPositionNo))
                        cmd.Parameters.AddWithValue("position", group.GetInt(Tags.MDEntryPositionNo));
                    else
                        cmd.Parameters.AddWithValue("position", DBNull.Value);
                
                    DateTime dtEntryDate = group.GetDateOnly(Tags.MDEntryDate);
                    DateTime dtEntryTime = group.GetTimeOnly(Tags.MDEntryTime);
                    cmd.Parameters.AddWithValue("date", dtEntryDate);
                    cmd.Parameters.AddWithValue("time",dtEntryTime );

                    if(group.IsSetField(Tags.TradeStatus))
                        cmd.Parameters.AddWithValue("status", group.GetChar(Tags.TradeStatus));
                    else
                        cmd.Parameters.AddWithValue("status", DBNull.Value);

                    if(group.IsSetField(Tags.OrigTrade))
                        cmd.Parameters.AddWithValue("orig", group.GetChar(Tags.OrigTrade));
                    else
                        cmd.Parameters.AddWithValue("orig", DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
                transaction.Commit();
                return true;
            }
            catch (Exception e)
            {
                _log?.OnLog("HistoricManager::SaveDetailHistory():: Error: " + e.Message);
                if (transaction != null) 
                    transaction.Rollback();
                _dump_detail.Write(message.ToString());
                
                return false;
            }
        }

        private void Process(Message message)
        {
            if (_disposed)
                return;

            SaveHistory(message);

            if(_params.SaveHistoryDetail)
            {
                if(!SaveDatailHistory(message))
                    return;
            }
        }

        void CheckDump(DumpFile dump, Func<Message, bool> method)
        {
            try
            {
                if(dump.Exists())
                {
                    Queue<string> list = new Queue<string>();
                    dump.ReadAll(list);

                    while (list.Count > 0)
                    {
                        string m = list.Dequeue();
                        method(new Message(m, _dd, true));
                    }
                    dump.RemoveFile();
                }
            }
            catch (Exception e)
            {
                _log?.OnLog("HistoricManager::CheckDump()::Error: " + e.Message);
            }
        }

        private bool OpenIfClosed(NpgsqlConnection conn, int maxAttempts=3)
        {

            if(conn.State == System.Data.ConnectionState.Open)
                return true;
        
            int attempts = 0;
            while (conn.State != System.Data.ConnectionState.Open && attempts < maxAttempts)
            {
                if(attempts > 0)
                {
                    _log?.OnLog("HistoricManager::OpenIfClosed():: Trying " + attempts + " in 5 secounds: ");
                    Thread.Sleep(5000);
                }

                try
                {
                    conn.Open();
                }
                catch (Exception e)
                {
                    _log?.OnLog("HistoricManager::OpenIfClosed():: Error: " + e.Message);
                }
                attempts += 1;
            }

            if(conn.State != System.Data.ConnectionState.Open)
            {
                _log?.OnLog("HistoricManager::OpenIfClosed():: WARNING: NOT CONNECTED!! ");
                return false;
            }
            else
            {
                _log?.OnLog("HistoricManager::OpenIfClosed():: Connected!!");
                CheckDump(_dump_hist, SaveHistory);
                CheckDump(_dump_detail, SaveDatailHistory);
                return true;
            }
        }
    }
}
