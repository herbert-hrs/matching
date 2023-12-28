using System;
using System.Collections.Generic;

namespace MatchingTest.Initiator
{
    public class ReqMarketData
    {
        public string Symbol { get; set; }
        public List<string> symbolList { get; set; }
        public string mdReqId { get; set; }

        public ReqMarketData()
        {
            mdReqId = Utils.GenerateID();
            symbolList = new List<string>();
        }

    }
}
