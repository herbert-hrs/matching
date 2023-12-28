using System;

namespace Matching
{
    public class Instrument
    {
        public string Symbol { get; set; }
        public string Description { get; set; }
        public string SecurityID { get; set; }        
        public string SecurityGroup { get; set; }        
        public bool IsTest { get; set; }        
        public bool IsLinked { get; set; }
        public bool IsDark { get; set; }
        public DateTime ExpirationDate { get; set; }          
    }
}
