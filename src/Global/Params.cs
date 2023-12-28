using System;
using SLTools.Api.SLSecretsApi.Models;

namespace Matching
{
    public class Params
    {
        public Params(RDSKeys rdsKeys)
        {
            IsRF = Convert.ToBoolean(Environment.GetEnvironmentVariable("IS_RF"));
            ScreenLogs = Convert.ToBoolean(Environment.GetEnvironmentVariable("SCREEN_LOGS"));

            DbFirst_Host = Environment.GetEnvironmentVariable("DB_FIRST_HOST");
            DbFirst_Port = Environment.GetEnvironmentVariable("DB_FIRST_PORT");
            DbFirst_Name = Environment.GetEnvironmentVariable("DB_FIRST_NAME");
            DbFirst_User = rdsKeys.username;
            DbFirst_Pass = rdsKeys.password;

            BrokerCfg = Environment.GetEnvironmentVariable("BROKER_CFG");
            MarketCfg = Environment.GetEnvironmentVariable("MARKET_CFG");
            TraceLogs = Environment.GetEnvironmentVariable("TRACE_LOGS");
            DumpPath = Environment.GetEnvironmentVariable("DUMP_PATH");
            FixDictionary = Environment.GetEnvironmentVariable("FIX_DICTIONARY");

            FixStore = "File";
            if(Environment.GetEnvironmentVariable("FIX_STORE") != "")
                FixStore = Environment.GetEnvironmentVariable("FIX_STORE");

            BrokerMatchApi = "";
            if(Environment.GetEnvironmentVariable("BROKERMATCH_API") != "")
                BrokerMatchApi = Environment.GetEnvironmentVariable("BROKERMATCH_API");

            FixBrokerLogs = false;
            if(Environment.GetEnvironmentVariable("FIX_BROKER_LOG") != "")
                FixBrokerLogs = Convert.ToBoolean(Environment.GetEnvironmentVariable("FIX_BROKER_LOG"));

            FixMarketLogs = false;
            if(Environment.GetEnvironmentVariable("FIX_MARKET_LOG") != "")
                FixMarketLogs = Convert.ToBoolean(Environment.GetEnvironmentVariable("FIX_MARKET_LOG"));

            IsBookByPU = false;
            if(Environment.GetEnvironmentVariable("BOOK_BY_PU") != "")
                IsBookByPU = Convert.ToBoolean(Environment.GetEnvironmentVariable("BOOK_BY_PU"));

            SaveHistoryDetail = true;
            if(Environment.GetEnvironmentVariable("SEND_HISTORY_DETAIL") != "")
                SaveHistoryDetail = Convert.ToBoolean(Environment.GetEnvironmentVariable("SEND_HISTORY_DETAIL"));

            if(SaveHistoryDetail)
            {
                DbHist_Host = Environment.GetEnvironmentVariable("DB_HIST_HOST");
                DbHist_Port = Environment.GetEnvironmentVariable("DB_HIST_PORT");
                DbHist_Name = Environment.GetEnvironmentVariable("DB_HIST_NAME");
                DbHist_User = rdsKeys.username;
                DbHist_Pass = rdsKeys.password;
            }
            SendExpirationDate = false;
            if(Environment.GetEnvironmentVariable("SEND_EXPIRATION_DATE") != "")
                SendExpirationDate = Convert.ToBoolean(Environment.GetEnvironmentVariable("SEND_EXPIRATION_DATE"));
            
            SendBrokerManagerID = false;
            if(Environment.GetEnvironmentVariable("SEND_BROKER_MANAGER_ID") != "")
                SendBrokerManagerID = Convert.ToBoolean(Environment.GetEnvironmentVariable("SEND_BROKER_MANAGER_ID"));
            
            SendROP = false;
            if(Environment.GetEnvironmentVariable("SEND_ROP") != "")
                SendROP = Convert.ToBoolean(Environment.GetEnvironmentVariable("SEND_ROP"));
            
        }

        public bool IsRF { get; set; }
        public bool SendExpirationDate { get; set; }
        public bool SendBrokerManagerID { get; set; }
        public bool SendROP { get; set; }
        public bool SaveHistoryDetail { get; set; }
        public bool IsBookByPU { get; set; }
        public bool ScreenLogs { get; set; }
        public bool FixBrokerLogs { get; set; }
        public bool FixMarketLogs { get; set; }
        public string DbFirst_Host { get; set; }
        public string DbFirst_Port { get; set; }
        public string DbFirst_User { get; set; }
        public string DbFirst_Pass { get; set; }
        public string DbFirst_Name { get; set; }
        public string DbSecond_Host { get; set; }
        public string DbSecond_Port { get; set; }
        public string DbSecond_User { get; set; }
        public string DbSecond_Pass { get; set; }
        public string DbSecond_Name { get; set; }
        public string BrokerCfg { get; set; }
        public string MarketCfg { get; set; }
        public string TraceLogs { get; set; }
        public string DbHist_Host { get; set; }
        public string DbHist_Port { get; set; }
        public string DbHist_User { get; set; }
        public string DbHist_Pass { get; set; }
        public string DbHist_Name { get; set; }
        public string FixStore { get; set; }
        public string DumpPath { get; set; }
        public string FixDictionary { get; set; }
        public string BrokerMatchApi { get; set; }
    }
}
