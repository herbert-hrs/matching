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
