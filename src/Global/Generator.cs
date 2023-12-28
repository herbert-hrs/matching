using System;

namespace Matching
{
    public class Generator
    {
        private static long _tradeID = 0;
        private static long _secOrderID = 0;
        private static readonly object _sync = new object();
        public static string GetTradeID()
        {
            lock(_sync)
            {
                return Convert.ToString(++_tradeID);
            }
        }

        public static string GetSecOrderID()
        {
            lock(_sync)
            {
                return Convert.ToString(++_secOrderID);
            }
        }
    }
}
