using QuickFix;
using QuickFix.Fields;

namespace MatchingTest.Initiator
{
    public class Controller
    {

        public Controller(){
        }

        public void QueryConnect(Message message) 
        {

        }

        public Message MarketDataRequest(ReqMarketData req)
        {   
            
            Message message = new QuickFix.FIX44.MarketDataRequest();

            message.SetField(new StringField(262, req.mdReqId));
            message.SetField(new CharField(263, '0')); //SubscriptionRequestType
            message.SetField(new IntField(264, 5)); //MarketDepth
            message.Header.SetField(new MsgType("V"));
            message.Header.SetField(new SendingTime());

            var group = new Group(267, 1); //NoMDEntryTypes
            group.SetField(new CharField(269, '0')); //MDEntryType
            message.AddGroup(group);

            group = new Group(267, 1); 
            group.SetField(new CharField(269, '1'));
            message.AddGroup(group);

            group = new Group(267, 1);
            group.SetField(new CharField(269, '2'));
            message.AddGroup(group);

            int[] fieldOrder = {55, 48};
            group = new Group(146, 1, fieldOrder); //NoRelatedSym
            for (int i = 0; i < req.symbolList.Count; i++) 
            {
                group.SetField(new StringField(55, req.symbolList[i]));
                group.SetField(new StringField(48,  req.symbolList[i]));
                message.AddGroup(group);
            }

            return message;
        }

        public Message MatchNewOrderSingle(Order order)
        {
            Message message = new Message();
            message.SetField(new StringField(37, order.orderId)); //ClOrdID
            message.SetField(new StringField(55, order.symbol)); //Symbol
            message.SetField(new CharField(54, order.side)); //Side
            message.SetField(new IntField(53, order.quantity)); //Quantity
            message.SetField(new StringField(1040, order.sTax)); //Tax
            message.SetField(new StringField(1067, order.sPU)); //PercentageTax            
            message.Header.SetField(new MsgType("U400"));
            message.Header.SetField(new SendingTime());
            return message;
        }

        public Message MatchNewOrderCross(Order order)
        {
            Message message = new Message();
            message.SetField(new StringField(1006, order.orderOffer)); //OrderOffer
            message.SetField(new StringField(1007, order.orderBid)); //OrderBid
            message.SetField(new StringField(55, order.symbol)); //Symbol
            message.SetField(new IntField(53, order.quantity)); //Quantity
            message.SetField(new StringField(1040, order.sTax)); //Tax
            message.SetField(new StringField(1067, order.sPU)); //PercentageTax 
            message.Header.SetField(new MsgType("U401"));
            message.Header.SetField(new SendingTime());
            return message;
        }

        public Message MatchOrderReplaceRequest(Order order)
        {
            Message message = new Message();
            message.SetField(new StringField(37, order.orderId)); //ClOrdID
            message.SetField(new StringField(55, order.symbol)); //Symbol
            message.SetField(new CharField(54, order.side)); //Side
            message.SetField(new IntField(53, order.quantity)); //Quantity
            message.SetField(new StringField(1040, order.sTax)); //Tax
            message.Header.SetField(new MsgType("U402"));
            message.Header.SetField(new SendingTime());
            return message;
        }

        public Message MatchOrderCancelRequest(Order order)
        {
            Message message = new Message();
            message.SetField(new StringField(37, order.orderId)); //ClOrdID
            message.SetField(new StringField(55, order.symbol)); //Symbol
            message.SetField(new CharField(54, order.side)); //Side
            message.Header.SetField(new MsgType("U403"));
            message.Header.SetField(new SendingTime());
            return message;
        }

    }
}
