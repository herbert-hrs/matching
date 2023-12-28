using System;

namespace Matching
{
    public class Trade
    {
        public string UniqueTradeID { get; set; }
        public string Symbol { get; set; }
        public string SecurityID { get; set; }        
        public char TradeStatus { get; set; }        
        public char OrigTrade { get; set; }        
        public int Quantity { get; set; }        
        public decimal Tax { get; set; }        
        public decimal PU { get; set; }
        public DateTime TradeTime { get; set; }

        public Trade(
            Instrument instrument, int qty, 
            decimal tax, decimal pu, string uniqueTradeID, 
            DateTime tradeTime, char tradeStatus, char origTrade)
        {
            UniqueTradeID = uniqueTradeID;
            Symbol = instrument.Symbol;
            SecurityID = instrument.SecurityID;
            Quantity = qty;
            Tax = tax;
            PU = pu;
            TradeTime = tradeTime;
            TradeStatus = tradeStatus;
            OrigTrade = origTrade;
        }
    }
}
