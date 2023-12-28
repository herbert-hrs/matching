using System;
using System.Collections.Generic;

namespace MatchingTest.Initiator
{
    public class Order
    {
        public string mdReqId { get; set; }
        public string orderId { get; set; }
        public string orderBid { get; set; }
        public string orderOffer { get; set; }
        public string symbol { get; set; }
        public int quantity { get; set; }
        public string sTax { get; set; }
        public string sPU { get; set; }
        public char side { get; set; }

        public Order()
        {
            orderId = Utils.GenerateID();
        }

        public Order Copy()
        {
            Order copy = new Order();

            copy.symbol = this.symbol;
            copy.quantity = this.quantity;
            copy.sTax = this.sTax;
            copy.sPU = this.sPU;
            copy.side = this.side;
            return copy;
        }

    }
}
