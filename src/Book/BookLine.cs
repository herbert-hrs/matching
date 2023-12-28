using System;
using QuickFix;
using QuickFix.Fields;

namespace Matching
{
    public class BookLine: IComparable, IDisposable
    {
        private bool _disposed = false;
        private readonly bool _isBookByPU = false;
        private readonly bool _isRF = false;
        private readonly bool _isLinked = false;
        public readonly bool _isDark = false;
        public string OrderID { get; set; }
        public string SecondaryOrderID { get; set; }
        public int BrokerID { get; set; }
        public int LastBrokerID { get; set; }
        public int ManagerID { get; set; }
        public char OrderProfile { get; set; }
        public string Symbol { get; set; }
        public string SecurityID { get; set; }
        public char Side { get; set; }
        public int Quantity { get; set; }
        public int LastQty { get; set; }
        public int LeavesQty { get; set; }
        public int CumQty { get; set; }
        public decimal Tax { get; set; }
        public decimal LastTax { get; set; }
        public decimal? LastPU { get; set; }
        public decimal AvgTax { get; set; }
        public decimal PU { get; set; }
        public string UniqueTradeID { get; set; }
        public bool IsAttack { get; set; }
        public DateTime ArrivalTime { get; set; }
        public DateTime TradeTime { get; set; }
        public OrderStatus Status { get; set; }
        public MDUpdateAction Action { get; set; }
        public int Position { get; set; }
        public string Text { get; set; }
        public string ClOrdID { get; set; }
        public string OrigClOrdID { get; set; }
        

        public BookLine(BookLine src)
        {
            this._isRF = src._isRF;
            this._isBookByPU = src._isBookByPU;
            this._isLinked = src._isLinked;
            this._isDark = src._isDark;
            this.SecurityID = src.SecurityID;
            this.OrderID = src.OrderID;
            this.Symbol = src.Symbol;
            this.Side = src.Side;            
            this.Quantity = src.Quantity;
            this.LeavesQty = src.LeavesQty;
            this.LastQty = src.LastQty;
            this.CumQty = src.CumQty;
            this.Tax = src.Tax;
            this.LastTax = src.LastTax;
            this.LastPU = src.LastPU;
            this.AvgTax = src.AvgTax;
            this.PU = src.PU;
            this.UniqueTradeID = src.UniqueTradeID;
            this.ArrivalTime = src.ArrivalTime;
            this.TradeTime = src.TradeTime;
            this.IsAttack = src.IsAttack;
            this.Status = new OrderStatus(src.Status.Obj);
            this.Action = new MDUpdateAction(MDUpdateAction.DELETE);
            this.Position = src.Position;
            this.BrokerID = src.BrokerID;
            this.LastBrokerID = src.BrokerID;
            this.ManagerID = src.ManagerID;
            this.OrderProfile = src.OrderProfile;
            this.Text = src.Text;
            this.ClOrdID = src.ClOrdID;
            this.OrigClOrdID = src.OrigClOrdID;
            
        }

        public BookLine(Message message, Instrument instrument, Params param)
        {   
            this.SecurityID = instrument.SecurityID;
            this.OrderID = message.GetString(Tags.OrderID);
            this.Symbol = message.GetString(Tags.Symbol);
            this.Side = message.GetChar(Tags.Side);

            this.Quantity = 0;
            if (message.IsSetField(Tags.Quantity))
                this.Quantity = message.GetInt(Tags.Quantity);

            this.LeavesQty = this.Quantity;
            this.LastQty = 0;
            this.CumQty = 0;

            this.Tax = 0;
            if (message.IsSetField(Tags.STax))
                this.Tax = Translator.ExtractPrice(message.GetString(Tags.STax));

            this.LastTax = 0;
            this.LastPU = null;
            this.AvgTax = 0;
            this.UniqueTradeID = "";
            this.SecondaryOrderID = "";           
            this.ArrivalTime = DateTime.Now;
            this.TradeTime = DateTime.MinValue;
            this.IsAttack = false;
            this.Status = new OrderStatus(OrderStatus.NEW);
            this.Action = new MDUpdateAction(MDUpdateAction.NEW);
            this.Position = 1;
            this._isRF = param.IsRF;
            this._isBookByPU = param.IsBookByPU;
            this._isLinked = instrument.IsLinked;
            this._isDark = instrument.IsDark;
            
            this.PU = 0;
            if (message.IsSetField(Tags.SPU))
                this.PU = Translator.ExtractPrice(message.GetString(Tags.SPU));

            this.BrokerID = 0;
            if (message.IsSetField(Tags.BrokerID))
            {
                this.BrokerID = message.GetInt(Tags.BrokerID);
                this.LastBrokerID = this.BrokerID;
            }

            this.ManagerID = 0;
            if (message.IsSetField(Tags.ManagerID))
                this.ManagerID = message.GetInt(Tags.ManagerID);

            this.OrderProfile = ' ';
            if (message.IsSetField(Tags.OrderProfile))
                this.OrderProfile = message.GetChar(Tags.OrderProfile);

            this.Text = "";
            if (message.IsSetField(Tags.Text))
                this.Text = message.GetString(Tags.Text);

            this.ClOrdID = "";
            if (message.IsSetField(Tags.ClOrdID))
                this.ClOrdID = message.GetString(Tags.ClOrdID);

            this.OrigClOrdID = "";
            if (message.IsSetField(Tags.OrigClOrdID))
                this.OrigClOrdID = message.GetString(Tags.OrigClOrdID);
                
        }

        public BookLine(Message message, Instrument instrument, 
            char side, string uniqueTradeID, DateTime tradeTime, Params param)
        {
            if (side == '1')
                this.OrderID = message.GetString(Tags.OrderBid);
            else
                this.OrderID = message.GetString(Tags.OrderOffer);

            if (message.IsSetField(Tags.SPU))
                this.PU = Translator.ExtractPrice(message.GetString(Tags.SPU));

            this.SecurityID = instrument.SecurityID;            
            this.Symbol = message.GetString(Tags.Symbol);
            this.Side = side;
            this.Quantity = message.GetInt(Tags.Quantity);
            this.LeavesQty = 0;
            this.LastQty = this.Quantity;
            this.CumQty = this.Quantity;
            this.Tax = Translator.ExtractPrice(message.GetString(Tags.STax));
            this.LastTax = Translator.ExtractPrice(message.GetString(Tags.STax));
            if (message.IsSetField(Tags.SPU))
                this.LastPU = Translator.ExtractPrice(message.GetString(Tags.SPU));
            this.AvgTax = Translator.ExtractPrice(message.GetString(Tags.STax));
            this.UniqueTradeID = uniqueTradeID;
            this.ArrivalTime = tradeTime;
            this.TradeTime = tradeTime;
            this.IsAttack = false;
            this.Status = new OrderStatus(OrderStatus.FILLED);
            this.Action = new MDUpdateAction(MDUpdateAction.DELETE);
            this.Position = 1;
            this._isRF = param.IsRF;
            this._isBookByPU = param.IsBookByPU;
            this._isLinked = instrument.IsLinked;
            this._isDark = instrument.IsDark;

            this.BrokerID = 0;
            if (message.IsSetField(Tags.BrokerID))
            {
                this.BrokerID = message.GetInt(Tags.BrokerID);
                this.LastBrokerID = this.BrokerID;
            }

            this.ManagerID = 0;
            if (message.IsSetField(Tags.ManagerID))
                this.ManagerID = message.GetInt(Tags.ManagerID);
            
            this.OrderProfile = ' ';
            if (message.IsSetField(Tags.OrderProfile))
                this.OrderProfile = message.GetChar(Tags.OrderProfile);

            this.Text = "";
            if (message.IsSetField(Tags.Text))
                this.Text = message.GetString(Tags.Text);

            this.ClOrdID = "";
            if (message.IsSetField(Tags.ClOrdID))
                this.ClOrdID = message.GetString(Tags.ClOrdID);

            this.OrigClOrdID = "";
            if (message.IsSetField(Tags.OrigClOrdID))
                this.OrigClOrdID = message.GetString(Tags.OrigClOrdID);
        }

        public bool Replace(Message message)
        {
            if (_disposed || LeavesQty == 0)
                return false;

            if (Status.Obj != OrderStatus.NEW &&
                Status.Obj != OrderStatus.REPLACED &&
                Status.Obj != OrderStatus.PARTIALLY_FILLED)
                return false;

            if (message.IsSetField(Tags.Text))
                this.Text = message.GetString(Tags.Text);

            if (message.IsSetField(Tags.ClOrdID))
                this.ClOrdID = message.GetString(Tags.ClOrdID);

            if (message.IsSetField(Tags.OrigClOrdID))
                this.OrigClOrdID = message.GetString(Tags.OrigClOrdID);

            int newQuantity = message.GetInt(Tags.Quantity);


            
            if (_isRF && _isBookByPU)
            {
                decimal newPU = Translator.ExtractPrice(message.GetString(Tags.SPU));

                if(newPU != this.PU || newQuantity > this.LeavesQty)
                {
                    this.ArrivalTime = DateTime.Now;
                    this.PU = newPU;
                }

                if (message.IsSetField(Tags.STax))
                    this.Tax = Translator.ExtractPrice(message.GetString(Tags.STax));
            }
            else
            {
                decimal newTax = Translator.ExtractPrice(message.GetString(Tags.STax));

                if(newTax != this.Tax || newQuantity > this.LeavesQty)
                {
                    this.ArrivalTime = DateTime.Now;
                    this.Tax = newTax;
                }

                if (message.IsSetField(Tags.SPU))
                    this.PU = Translator.ExtractPrice(message.GetString(Tags.SPU));
            }

            this.Quantity = this.CumQty + newQuantity;

            this.LeavesQty = newQuantity;
            this.TradeTime = DateTime.MinValue;
            this.Status.Obj = OrderStatus.REPLACED;
            this.Action.Obj = MDUpdateAction.NEW;

            return true;
        }

        public bool Cancel(Message message)
        {
            if (_disposed || Status.Obj == OrderStatus.CANCELED)
                return false;

            this.ArrivalTime = DateTime.Now;
            this.TradeTime = DateTime.MinValue;
            this.IsAttack = false;
            this.Status.Obj = OrderStatus.CANCELED;
            this.Action.Obj = MDUpdateAction.DELETE;

            if (message.IsSetField(Tags.Text))
                this.Text = message.GetString(Tags.Text);

            if (message.IsSetField(Tags.ClOrdID))
                this.ClOrdID = message.GetString(Tags.ClOrdID);

            if (message.IsSetField(Tags.OrigClOrdID))
                this.OrigClOrdID = message.GetString(Tags.OrigClOrdID);

            return true;
        }

        public bool Cancel()
        {
            if (_disposed || Status.Obj == OrderStatus.CANCELED)
                return false;

            this.ArrivalTime = DateTime.Now;
            this.TradeTime = DateTime.MinValue;
            this.IsAttack = false;
            this.Status.Obj = OrderStatus.CANCELED;
            this.Action.Obj = MDUpdateAction.DELETE;

            return true;
        }

        public bool PartialCancel()
        {
            if (_disposed  || Status.Obj == OrderStatus.PARTIALLY_FILLED_CANCELED)
                return false;

            this.ArrivalTime = DateTime.Now;
            this.TradeTime = DateTime.MinValue;
            this.IsAttack = false;
            this.Status.Obj = OrderStatus.PARTIALLY_FILLED_CANCELED;
            this.Action.Obj = MDUpdateAction.DELETE;

            return true;
        }

        public void Trade(
            int qty, 
            decimal tax, 
            decimal pu, 
            string tradeID, 
            bool isAttack, DateTime tradeTime, int brokerID)
        {
            if (_disposed)
                return;

            if (qty == this.LeavesQty)
            {   
                this.Status.Obj = OrderStatus.FILLED;
                this.Action.Obj = MDUpdateAction.DELETE;
            }                
            else
            {   
                this.Status.Obj = OrderStatus.PARTIALLY_FILLED;
                this.Action.Obj = MDUpdateAction.CHANGE;
            }                

            LastTax = tax;
            LastPU = pu;
            LastQty = qty;
            LeavesQty -= qty;
            CumQty += qty;
            IsAttack = isAttack;
            UniqueTradeID = tradeID;
            TradeTime = tradeTime;
            LastBrokerID = brokerID;

            if (CumQty > 0)
            {
                int avgQty = CumQty - LastQty;
                AvgTax = ((LastQty * LastTax) + (AvgTax * avgQty)) / CumQty;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                this.Status = null;
                this.Action = null;
            }
        }
        
        public int CompareTo(object obj)
        {
            BookLine other = (BookLine)obj;
            int result;
            decimal tax;
            decimal otherTax;

            if (_isRF)
            {
                if(!_isBookByPU)
                {
                    tax = Translator.ConvertPrice(other.Tax);
                    otherTax = Translator.ConvertPrice(Tax);
                }
                else
                {
                    tax = Translator.ConvertPrice(PU);
                    otherTax = Translator.ConvertPrice(other.PU);
                }
            }
            else
            {
                tax = Translator.ConvertPrice(Tax);
                otherTax = Translator.ConvertPrice(other.Tax);
            }

            if (this.Side == '1')
                result = -tax.CompareTo(otherTax);
            else
                result = tax.CompareTo(otherTax);

            if (result == 0)
                return this.ArrivalTime.CompareTo(other.ArrivalTime);

            return result;
        }
    }
}
