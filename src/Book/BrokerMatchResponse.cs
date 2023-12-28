using System.Collections.Generic;

namespace Matching
{
    public class BrokerMatchResponse
    {
        public BrokerMatchResponse()
        {
            Success = true;
            Message = "Success";
            brokers = new List<int>();
            broker_id = 0;
            
        }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<int> brokers { get; set; }
        public int broker_id { get; set; }
    }
}