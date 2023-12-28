using QuickFix;
using QuickFix.Fields;
using System;
using System.Globalization;


namespace Matching
{
    public class Translator
    {
        public static bool IsRF { get; set; }
        public static bool SendBrokerManagerID { get; set; }
        public static bool SendROP { get; set; }
        private static System.IFormatProvider culture = new CultureInfo("en-US");
        private static Random rnd = new Random();

        public static decimal ExtractPrice(string value)
        {
            if (value == "")
                return 0;

            decimal temp = Convert.ToDecimal(value, culture);

            return Convert.ToDecimal(temp.ToString(IsRF ? "F6" : "F2", culture), culture);
        }

        public static decimal ConvertPrice(decimal value)
        {
            if (value == 0)
                return value;

            return Convert.ToDecimal(value.ToString(IsRF ? "F6" : "F2", culture), culture);
        }

        public static string ConvertString(decimal? value)
        {
            string result = "";

            if (value == 0)
                return (IsRF ? "0.000000" : "0.00");
            
            result = value?.ToString(IsRF ? "F6" : "F2", culture);

            return result;
        }

        public static int ShadowID(int id, int pos)
        {
            return ((id+pos)*(id+pos)+pos)*100 + rnd.Next(1,99);
        }

        public static Message BrokerReport(BookLine line)
        {
            var message = new QuickFix.FIX44.MatchExecutionReport();
            message.Set(new OrderID(line.OrderID));
            
            message.Set(new Symbol(line.Symbol));
            message.Set(new Side(line.Side));
            message.Set(new Quantity(line.Quantity));            
            message.Set(new LeavesQty(line.LeavesQty));
            message.Set(new CumQty(line.CumQty));
            message.Set(new STax(ConvertString(line.Tax)));
            message.Set(new OrderStatus(line.Status.Obj));
            message.Set(new IsAttack(line.IsAttack));

            message.Set(new LastQty(line.LastQty));
            message.Set(new SLastTax(ConvertString(line.LastTax)));

            
                
            message.Set(new SAvgTax(ConvertString(line.AvgTax)));
            message.Set(new SPU(ConvertString(line.PU)));

            if ((line.Status.Obj == OrderStatus.FILLED) ||
                (line.Status.Obj == OrderStatus.PARTIALLY_FILLED))
            {
                message.Set(new TransactTime(line.TradeTime, true));
                message.Set(new TradeDate(line.TradeTime.ToString("yyyyMMdd")));
            }                
            else
            {
                message.Set(new TransactTime(line.ArrivalTime, true));
            }

            if (line.UniqueTradeID != "")
                message.Set(new UniqueTradeID(line.UniqueTradeID));

            if(IsRF)
            {
                if(line.LastPU != null)
                    message.Set(new SLastPU(ConvertString(line.LastPU)));
                    
                if (!string.IsNullOrEmpty(line.SecondaryOrderID))
                    message.Set(new SecondaryOrderID(line.SecondaryOrderID));
            
                
                if (line.LastBrokerID != 0)
                    message.SetField(new BrokerID(line.LastBrokerID));
            

                if (!String.IsNullOrEmpty(line.Text))
                    message.SetField(new Text(line.Text));

                if (!String.IsNullOrEmpty(line.ClOrdID))
                    message.SetField(new ClOrdID(line.ClOrdID));

                if (!String.IsNullOrEmpty(line.OrigClOrdID))
                    message.SetField(new OrigClOrdID(line.OrigClOrdID));
            }

            return message;
        }

        public static QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup BuildNode(BookLine line, MDUpdateAction action)
        {
            var node = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
            
            if (line.Side == Side.BUY)
            {
                node.Set(new MDEntryType(MDEntryType.BID));
                node.Set(new MDEntryBuyer("0"));
            }
            else
            {
                node.Set(new MDEntryType(MDEntryType.OFFER));
                node.Set(new MDEntrySeller("0"));
            }

            node.Set(new MDUpdateAction(action.Obj));
            node.Set(new SecurityIDSource((IsRF) ? "4" : "8"));
            node.Set(new OrderID(line.OrderID));
            if(SendBrokerManagerID)
            {
                node.Set(new BrokerID(ShadowID(line.BrokerID, line.Position)));
                node.Set(new ManagerID(ShadowID(line.ManagerID, line.Position)));
            }
            node.Set(new SecurityID(line.SecurityID));
            node.Set(new Symbol(line.Symbol));
            if(!line._isDark)
            {
                node.Set(new MDEntryPx(ConvertPrice(line.Tax)));
                node.Set(new MDEntrySize(line.LeavesQty));
                node.Set(new PU(line.PU));
            }
            node.Set(new MDEntryDate(line.ArrivalTime));
            node.Set(new MDEntryTime(line.ArrivalTime));
            node.Set(new MDEntryID(line.OrderID));
            node.Set(new MDEntryPositionNo(line.Position));
            return node;
        }

        public static QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup BuildNodeTopBook(BookLine line, MDUpdateAction action)
        {
            var node = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
            
            if (line.Side == Side.BUY)
            {
                node.Set(new MDEntryType(MDEntryType.SESSION_HIGH_BID ));
                node.Set(new MDEntryBuyer("0"));
            }
            else
            {
                node.Set(new MDEntryType(MDEntryType.SESSION_LOW_OFFER));
                node.Set(new MDEntrySeller("0"));
            }
            node.Set(new MDUpdateAction(action.Obj));
            node.Set(new SecurityIDSource((IsRF) ? "4" : "8"));
            node.Set(new OrderID(line.OrderID));

            if(SendBrokerManagerID)
            {
                node.Set(new BrokerID(ShadowID(line.BrokerID, line.Position)));
                node.Set(new ManagerID(ShadowID(line.ManagerID, line.Position)));
            }
            node.Set(new SecurityID(line.SecurityID));
            node.Set(new Symbol(line.Symbol));
            if(!line._isDark)
            {
                node.Set(new MDEntryPx(ConvertPrice(line.Tax)));
                node.Set(new MDEntrySize(line.LeavesQty));
                node.Set(new PU(line.PU));
            }
            node.Set(new MDEntryDate(line.ArrivalTime));
            node.Set(new MDEntryTime(line.ArrivalTime));
            node.Set(new MDEntryID(line.OrderID));
            return node;
        }

        public static QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup BuildNode(Trade trade, MDUpdateAction action)
        {
            var node = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();

            node.Set(new MDEntryType(MDEntryType.TRADE));
            node.Set(new MDUpdateAction(action.Obj));
            node.Set(new SecurityIDSource((IsRF) ? "4" : "8"));
            node.Set(new SecurityID(trade.SecurityID));
            node.Set(new Symbol(trade.Symbol));
            node.Set(new MDEntryPx(ConvertPrice(trade.Tax)));
            node.Set(new MDEntrySize(trade.Quantity));
            node.Set(new MDEntryDate(trade.TradeTime));
            node.Set(new MDEntryTime(trade.TradeTime));
            node.Set(new MDEntryID(trade.UniqueTradeID));
            node.Set(new UniqueTradeID(trade.UniqueTradeID));
            node.Set(new PU(trade.PU));
            node.Set(new MDEntryBuyer(""));
            node.Set(new MDEntrySeller(""));

            if(IsRF && SendROP)
            {
                node.Set(new OrigTrade(trade.OrigTrade));
                node.Set(new TradeStatus(trade.TradeStatus));
            }
            return node;
        }

        public static Message MarketReport(BookLine line, MDUpdateAction action)
        {
            var message = new QuickFix.FIX44.MarketDataIncrementalRefresh();
            var mdEntry = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();
            message.Set(new TradeDate(DateTime.Now.ToString("yyyyMMdd")));

            if (line.Side == Side.BUY)
            {
                mdEntry.Set(new MDEntryType(MDEntryType.BID));
                mdEntry.Set(new MDEntryBuyer("0"));
            }                
            else
            {
                mdEntry.Set(new MDEntryType(MDEntryType.OFFER));
                mdEntry.Set(new MDEntrySeller("0"));
            }

            mdEntry.Set(new MDUpdateAction(action.Obj));
            mdEntry.Set(new SecurityIDSource((IsRF) ? "4" : "8"));
            mdEntry.Set(new OrderID(line.OrderID));

            if(SendBrokerManagerID)
            {
                mdEntry.Set(new BrokerID(ShadowID(line.BrokerID, line.Position)));
                mdEntry.Set(new ManagerID(ShadowID(line.ManagerID, line.Position)));
            }
            mdEntry.Set(new SecurityID(line.SecurityID));
            mdEntry.Set(new Symbol(line.Symbol));
            if(!line._isDark)
            {
                mdEntry.Set(new MDEntryPx(ConvertPrice(line.Tax)));
                mdEntry.Set(new MDEntrySize(line.LeavesQty));
                mdEntry.Set(new PU(line.PU));
            }
            mdEntry.Set(new MDEntryDate(line.ArrivalTime));
            mdEntry.Set(new MDEntryTime(line.ArrivalTime));
            mdEntry.Set(new MDEntryID(line.OrderID));
            mdEntry.Set(new MDEntryPositionNo(line.Position));
            message.AddGroup(mdEntry);

            

            return message;
        }

        public static Message MarketReport(Trade trade, MDUpdateAction action, char type = MDEntryType.TRADE )
        {
            var message = new QuickFix.FIX44.MarketDataIncrementalRefresh();
            var mdEntry = new QuickFix.FIX44.MarketDataIncrementalRefresh.NoMDEntriesGroup();

            mdEntry.Set(new MDEntryType(type));
            mdEntry.Set(new MDUpdateAction(action.Obj));
            mdEntry.Set(new SecurityIDSource((IsRF) ? "4" : "8"));
            mdEntry.Set(new SecurityID(trade.SecurityID));
            mdEntry.Set(new Symbol(trade.Symbol));
            mdEntry.Set(new MDEntryPx(ConvertPrice(trade.Tax)));
            mdEntry.Set(new MDEntrySize(trade.Quantity));
            mdEntry.Set(new MDEntryDate(trade.TradeTime));
            mdEntry.Set(new MDEntryTime(trade.TradeTime));
            mdEntry.Set(new MDEntryID(trade.UniqueTradeID));
            mdEntry.Set(new UniqueTradeID(trade.UniqueTradeID));
            mdEntry.Set(new PU(trade.PU));
            mdEntry.Set(new MDEntryBuyer(""));
            mdEntry.Set(new MDEntrySeller(""));
            if(IsRF && SendROP)
            {
                mdEntry.Set(new OrigTrade(trade.OrigTrade));
                mdEntry.Set(new TradeStatus(trade.TradeStatus));
            }
            message.AddGroup(mdEntry);
            return message;
        }
    }
}
