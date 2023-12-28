using System;
using Xunit;
using QuickFix; 
using QuickFix.Transport; 
using MatchingTest.Initiator;
using QuickFix.Fields;
using Matching.Secrets;
using SLTools.Util.Config;

namespace MatchingTest 
{
    public class QuickfixFixture : IDisposable
    {   
        private static int NUM_SYMBOLS = 20;
        public QuickfixFixture()
        {   
            DotEnv.Load(); 

            MatchingSecrets matchingSecrets = new();

            _dataManager = new DataManager(matchingSecrets.RdsKeys);
            _dataManager.DeleteSymbols(NUM_SYMBOLS);
            _dataManager.PrepareSymbols(NUM_SYMBOLS);

            String brokerFile = "./config/broker.cfg";

            _controller = new Controller();
            SessionSettings brokerSettings = new SessionSettings(brokerFile);
            _applicationBroker = new App(_controller, true);
            IMessageStoreFactory brokerStoreFactory = new FileStoreFactory(brokerSettings);
            MessageFactory factory = new MessageFactory();
            _initiatorBroker = new SocketInitiator(_applicationBroker, brokerStoreFactory, brokerSettings, null, factory);
            _initiatorBroker.Start();
            Message message = new Message();
             _applicationBroker.WaitUntilFirstMsgType(MsgType.LOGON, "", ref message);
            
            String marketFile = "./config/market.cfg";

            SessionSettings marketSettings = new SessionSettings(marketFile);
            _applicationMarket = new App(_controller, true);
            IMessageStoreFactory marketStoreFactory = new FileStoreFactory(marketSettings);
            MessageFactory factoryMarket = new MessageFactory();
            _initiatorMarket = new SocketInitiator(_applicationMarket, marketStoreFactory, marketSettings, null, factoryMarket);
            _initiatorMarket.Start();

            message = new Message();
            _applicationMarket.WaitUntilFirstMsgType(MsgType.LOGON, "", ref message);

        }   

        public void Dispose()
        {
            _dataManager.DeleteOrders();
            _dataManager.Close();
            _initiatorBroker.Stop();
            _initiatorMarket.Stop();
        }
        private DataManager _dataManager;
        private SocketInitiator _initiatorBroker;
        private SocketInitiator _initiatorMarket;
        public Controller _controller;
        public App _applicationBroker;
        public App _applicationMarket;
    }

    [CollectionDefinition("Quickfix collection")]
    public class QuickfixCollection : ICollectionFixture<QuickfixFixture>
    {
    }

    [Collection("Quickfix collection")]
    public class BasicTests
    {
        QuickfixFixture fixture;
        public BasicTests(QuickfixFixture fixture)
        {
            this.fixture = fixture;
        }

        bool checkNewMsgBroker(String orderId, ref Message messageIn)
        {
            bool checkMsg = fixture._applicationBroker.WaitUntilFirstMsgType(MsgType.MATCH_EXECUTION_REPORT, orderId, ref messageIn);
            Assert.True(checkMsg);

            Assert.Equal(OrderStatus.NEW, messageIn.GetChar(Tags.OrderStatus));

            return true;
        }

        bool checkExecMsgBroker(String orderId, ref Message messageIn) 
        {   
            bool checkMsg = fixture._applicationBroker.WaitUntilFirstMsgType(MsgType.MATCH_EXECUTION_REPORT, orderId, ref messageIn);
            Assert.True(checkMsg);

            Assert.Equal(OrderStatus.FILLED, messageIn.GetChar(Tags.OrderStatus));

            return true;
        }

        bool checkRejectMsgBroker(String orderId, ref Message messageIn) 
        {   
            bool checkMsg = fixture._applicationBroker.WaitUntilFirstMsgType(MsgType.MATCH_EXECUTION_REPORT, orderId, ref messageIn);
            Assert.True(checkMsg);

            Assert.Equal(OrderStatus.REJECTED, messageIn.GetChar(Tags.OrderStatus));

            return true;
        }

        bool checkReplaceMsgBroker(String orderId, ref Message messageIn)
        {   
            bool checkMsg = fixture._applicationBroker.WaitUntilFirstMsgType(MsgType.MATCH_EXECUTION_REPORT, orderId, ref messageIn);
            Assert.True(checkMsg); 

            Assert.Equal(OrderStatus.REPLACED, messageIn.GetChar(Tags.OrderStatus));

            return true;
        }

        bool checkCancelMsgBroker(String orderId, ref Message messageIn)
        {   
            bool checkMsg = fixture._applicationBroker.WaitUntilFirstMsgType(MsgType.MATCH_EXECUTION_REPORT, orderId, ref messageIn);
            Assert.True(checkMsg); 

            Assert.Equal(OrderStatus.CANCELED, messageIn.GetChar(Tags.OrderStatus));

            return true;
        }

        bool checkNewMsgMarket(String orderId, ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_INCREMENTAL_REFRESH, orderId, ref messageIn);
            Assert.True(checkMsg);

            bool orderOk = false, lowHighOk = false;

            int noGroups = messageIn.GetInt(Tags.NoMDEntries);
            for (int i = 1; i <= noGroups; i++)
            {
                var group = messageIn.GetGroup(i, Tags.NoMDEntries);
                char entryType = group.GetChar(Tags.MDEntryType);
                if (MDEntryType.BID == entryType || MDEntryType.OFFER == entryType)
                {
                    Assert.Equal(MDUpdateAction.NEW, group.GetChar(Tags.MDUpdateAction));
                    orderOk = true;
                }
                else if (MDEntryType.SESSION_LOW_OFFER == entryType || MDEntryType.SESSION_HIGH_BID == entryType)
                {
                    Assert.Equal(MDUpdateAction.CHANGE, group.GetChar(Tags.MDUpdateAction));
                    lowHighOk = true;
                }
            }

            Assert.True(orderOk);
            if (noGroups > 1)
            {
                Assert.True(lowHighOk);
            }

            return true;
        }

        bool checkNewQuoteMarket(string symbol, ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.SECURITY_QUOTES, "", ref messageIn);
            Assert.True(checkMsg);

            Assert.Equal(symbol, messageIn.GetString(Tags.Symbol));

            Assert.Equal(MDUpdateAction.NEW, messageIn.GetChar(Tags.MDUpdateAction));

            return true;
        }

        bool checkExecMsgMarket(ref Message messageIn) 
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_INCREMENTAL_REFRESH, "", ref messageIn);
            Assert.True(checkMsg);

            int noGroup = messageIn.GetInt(Tags.NoMDEntries);
            Assert.Equal(5, noGroup);

            bool offerOk = false, bidOk = false, tradeOk = false, lowOk = false, highOk = false;
            for (int i = 1; i <= noGroup; i++)
            {
                var group = messageIn.GetGroup(i, Tags.NoMDEntries);
                char entryType = group.GetChar(Tags.MDEntryType);
                if (MDEntryType.OFFER == entryType)
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    offerOk = true;
                }
                else if (MDEntryType.BID == entryType)
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    bidOk = true;
                }
                else if (MDEntryType.TRADE == entryType)
                {
                    Assert.Equal(MDUpdateAction.NEW, group.GetChar(Tags.MDUpdateAction));
                    tradeOk = true;
                }
                else if (MDEntryType.SESSION_HIGH_BID == entryType)
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    highOk = true;
                }
                else if (MDEntryType.SESSION_LOW_OFFER == entryType)
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    lowOk = true;
                }

            }

            Assert.True(offerOk);
            Assert.True(bidOk);
            Assert.True(tradeOk);
            Assert.True(lowOk);
            Assert.True(highOk);

            return true;
        }

        bool checkCrossExecMsgMarket(ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_INCREMENTAL_REFRESH, "", ref messageIn);
            Assert.True(checkMsg);

            var group = messageIn.GetGroup(1, Tags.NoMDEntries);
            Assert.Equal(MDEntryType.TRADE, group.GetChar(Tags.MDEntryType));

            return true;
        }

        bool checkReplaceMsgMarket(String orderId, ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_INCREMENTAL_REFRESH, "", ref messageIn);
            Assert.True(checkMsg);

            bool orderOk = false, lowHighOk = false;

            int noGroup = messageIn.GetInt(Tags.NoMDEntries);
            for (int i = 1; i <= noGroup; i++)
            {
                var group = messageIn.GetGroup(i, Tags.NoMDEntries);
                char entryType = group.GetChar(Tags.MDEntryType);
                if (MDEntryType.SESSION_LOW_OFFER == entryType || MDEntryType.SESSION_HIGH_BID == entryType) 
                {
                    Assert.Equal(MDUpdateAction.CHANGE, group.GetInt(Tags.MDUpdateAction));
                    lowHighOk = true;
                }
                else
                {
                    Assert.Equal(MDUpdateAction.CHANGE, group.GetChar(Tags.MDUpdateAction));
                    orderOk = true;
                }
            }

            Assert.True(orderOk);
            if (noGroup > 1) 
            {
                Assert.True(lowHighOk);
            }

            return true;
        }

        bool checkCancelMsgMarket(String orderId, ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_INCREMENTAL_REFRESH, "", ref messageIn);
            Assert.True(checkMsg);

            bool orderOk = false, lowHighOk = false;

            int noGroup = messageIn.GetInt(Tags.NoMDEntries);
            for (int i = 1; i <= noGroup; i++)
            {
                var group = messageIn.GetGroup(i, Tags.NoMDEntries);
                char entryType = group.GetChar(Tags.MDEntryType);
                if (MDEntryType.SESSION_LOW_OFFER == entryType || MDEntryType.SESSION_HIGH_BID == entryType) 
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    lowHighOk = true;
                }
                else
                {
                    Assert.Equal(MDUpdateAction.DELETE, group.GetChar(Tags.MDUpdateAction));
                    orderOk = true;
                }
            }

            Assert.True(orderOk);
            if (noGroup > 1) 
            {
                Assert.True(lowHighOk);
            }

            return true;
        }

        bool checkMdReject(ref Message messageIn)
        {
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_REQUEST_REJECT, "", ref messageIn);
            Assert.True(checkMsg);
            return true;
        }

        Message getSnapshot()
        {
            Message message = new Message();
            bool checkMsg = fixture._applicationMarket.WaitUntilFirstMsgType(MsgType.MARKET_DATA_SNAPSHOT_FULL_REFRESH, "", ref message);
            Assert.True(checkMsg);
            return message;
        }

        bool checkSnapshotTrade(ref Order order, ref Message messageIn)
        {
            bool flag = false;
            int noGroups = messageIn.GetInt(Tags.NoMDEntries);
            for (int i = 1; i <= noGroups; i++)
            {
                var group = messageIn.GetGroup(i, Tags.NoMDEntries);
                if (group.IsSetField(Tags.UniqueTradeID))
                {
                    Assert.Equal(MDEntryType.TRADE, group.GetChar(Tags.MDEntryType));
                    Assert.Equal(Utils.ExtractPrice(order.sTax), group.GetDecimal(Tags.MDEntryPx));
                    Assert.Equal(order.quantity, group.GetInt(Tags.MDEntrySize));
                    Assert.Equal(Utils.ExtractPrice(order.sPU), group.GetDecimal(Tags.PU));
                    flag = true;
                }
            }

            Assert.True(flag);

            return true;
        }

        bool checkSnapshotOrder(Order order, string []pu, Message messageIn)
        {
            int noGroups = messageIn.GetInt(Tags.NoMDEntries);
            char entryType = order.side;

            for (int i = 1; i <= noGroups; i++)
            {
                Group group =  messageIn.GetGroup(i, Tags.NoMDEntries);

                char groupEntryType = group.GetChar(Tags.MDEntryType);
                if (entryType - 1 == groupEntryType) // Operação em ASCII
                {

                    Assert.Equal(Utils.ExtractPrice(pu[i - 1]), group.GetDecimal(Tags.PU));

                    Assert.Equal(order.quantity, group.GetInt(Tags.MDEntrySize));

                    Assert.Equal(Utils.ExtractPrice(order.sTax), group.GetDecimal(Tags.MDEntryPx));
                }
            }

            return true;
        }
        
        [Fact]
        // Testar o casamento de ofertas na sequência doadora e tomadora
        public void MatchNewOrderSingleOfferBid()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE1-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker("", ref messageIn));

            order.orderId = Utils.GenerateID();
            order.side = '1';
            
            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkNewMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            
            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE1-SL", ref messageIn));
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkExecMsgMarket(ref messageIn));
        }
        
        [Fact]
        // Testar o casamento de ofertas na sequêcia tomadora e doadora
        public void MatchNewOrderSingleBidOffer()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE2-SL";
            order.side = '1';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker("", ref messageIn));

            order.orderId = Utils.GenerateID();
            order.side = '2';
            
            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkNewMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE2-SL", ref messageIn));
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkExecMsgMarket(ref messageIn));
        }
        
        [Fact]
        // Testar criação de ordem com id repetido
        public void MatchNewOrderSingleRepeatedId()
        {   
            
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            Order order = new Order();
            order.symbol = "TESTE3-SL";
            order.side = '1';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));
        }
        
        [Fact]
        // Testar envio de cross
        public void MatchNewOrderCross()
        {   
            
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            Order order = new Order();
            order.orderOffer = Utils.GenerateID();
            order.orderBid = Utils.GenerateID();
            order.symbol = "TESTE4-SL";
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderCross(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkExecMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            
            //Market
            Assert.True(checkCrossExecMsgMarket(ref messageIn));
        }
        
        [Fact]
        // Testar envio de cross com papel inválido
        public void MatchNewOrderCrossInvalidSymbol()
        {   
            
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            Order order = new Order();
            order.orderOffer = Utils.GenerateID();
            string orderOffer = order.orderOffer;
            order.orderBid = Utils.GenerateID();
            string orderBid = order.orderBid;
            order.symbol = "INVALIDO";
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderCross(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkRejectMsgBroker("", ref messageIn));
            Assert.True(checkRejectMsgBroker("", ref messageIn));
            
        }
        
        [Fact]
        // Testar a deleção de ordem
        public void MatchOrderCancelRequest()
        {   
            fixture._applicationBroker.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE5-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker("", ref messageIn));

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkCancelMsgBroker(order.orderId, ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkCancelMsgMarket("", ref messageIn));
        }
        
        [Fact]
        // Testar a deleção de ordem já deletada
        public void MatchOrderCancelRequestAlreadyCanceledOrder()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE6-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkCancelMsgBroker(order.orderId, ref messageIn));

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkCancelMsgMarket("", ref messageIn));
        }
        
        [Fact]
        // Testar a deleção de ordem inexistente
        public void MatchOrderCancelInexistentOrder()
        {   
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE7-SL";
            order.side = '2';

            Message messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));

        }

        [Fact]
        // Testar a deleção de ordem com papel errado
        public void MatchOrderCancelRequestWrongSymbol()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE8-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            order.symbol = "TESTE2-SL";

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));
        }
        
        [Fact]
        // Testar a deleção de ordem com Side errado
        public void MatchOrderCancelRequestWrongSide()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE9-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            order.side = '1';

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));
        }
        
        [Fact]
        // Testar deleção de ordem casada
        public void MatchOrderCancelRequestAlreadyMatchedOrder()
        {   
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE10-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            string orderSellId = order.orderId;

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(orderSellId, ref messageIn));

            order.orderId = Utils.GenerateID();
            order.side = '1';
            
            string orderBuyId = order.orderId;

            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkNewMsgBroker(orderBuyId, ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(orderBuyId, ref messageIn));

            order.side = '2';
            order.orderId = orderSellId;
            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(orderSellId, ref messageIn));
            
            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE10-SL", ref messageIn));
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkExecMsgMarket(ref messageIn));

        }
        
        [Fact]
        // Testar a atualização de ordens
        public void MatchOrderReplaceRequest()
        {   
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE11-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            order.sTax = "1.07";

            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkReplaceMsgBroker(order.orderId, ref messageIn));
        }
        
        [Fact]
        // Testar a atualização de ordem inexistente
        public void MatchOrderReplaceRequestInexistentOrder()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE12-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";

            Message messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));

        }

        [Fact]
        // Testar atualização de ordem casada
        public void MatchOrderReplaceRequestAlreadyMatchedOrder()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE13-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            string orderSellId = order.orderId;

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker("", ref messageIn));

            order.orderId = Utils.GenerateID();
            order.side = '1';
            
            string orderBuyId = order.orderId;

            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkNewMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));

            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(orderBuyId, ref messageIn));

            order.orderId = orderSellId;
            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(orderSellId, ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE13-SL", ref messageIn));
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkExecMsgMarket(ref messageIn));
        }
        
        [Fact]
        // Testar a atualização de ordem deletada
        public void MatchOrderReplaceRequestAlreadyCanceledOrder()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE14-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            messageOut = fixture._controller.MatchOrderCancelRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkCancelMsgBroker(order.orderId, ref messageIn));

            order.sTax = "1.07";
            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE14-SL", ref messageIn));
            Assert.True(checkCancelMsgMarket("", ref messageIn));

        }
        
        [Fact]
        // Testar a atualização de ordem com Side errado
        public void MatchOrderReplaceRequestWrongSide()
        {   
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE15-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            order.sTax = "1.07";
            order.side = '1';
            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));
        }

        [Fact]
        // Testar a atualização de ordem com papel errado
        public void MatchOrderReplaceRequestWrongSymbol()
        {   

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            
            Order order = new Order();
            order.symbol = "TESTE16-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker(order.orderId, ref messageIn));

            order.sTax = "1.07";
            order.symbol = "TESTE-SL";
            messageOut = fixture._controller.MatchOrderReplaceRequest(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkRejectMsgBroker(order.orderId, ref messageIn));
        }
        
        [Fact]  
        // Testar pedido de papel inexistente
        public void MarketDataRequestInexistentSymbol()
        {

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            ReqMarketData mdRequestData = new ReqMarketData();
            mdRequestData.symbolList.Add("INEXISTENTE");

            Message messageOut = fixture._controller.MarketDataRequest(mdRequestData);
            fixture._applicationMarket.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkMdReject(ref messageIn));
        }

        [Fact]  
        // Testar pedido de dois papéis inexistentes
        public void MarketDataRequestTwoInexistentSymbol()
        {

            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            ReqMarketData mdRequestData = new ReqMarketData();
            mdRequestData.symbolList.Add("INEXISTENTE1");
            mdRequestData.symbolList.Add("INEXISTENTE2");

            Message messageOut = fixture._controller.MarketDataRequest(mdRequestData);
            fixture._applicationMarket.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkMdReject(ref messageIn));
            Assert.True(checkMdReject(ref messageIn));
        }
        
        [Fact]  
        // Testar inserção de um negócio no book
        public void MarketDataRequestTradeInsertion()
        {
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();

            Order order = new Order();
            order.symbol = "TESTE17-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "555";

            Message messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();
            Assert.True(checkNewMsgBroker("", ref messageIn));

            order.side = '1';
            order.orderId = Utils.GenerateID();

            messageOut = fixture._controller.MatchNewOrderSingle(order);
            fixture._applicationBroker.SendMessage(messageOut);

            Assert.True(checkNewMsgBroker("", ref messageIn));
            Assert.True(checkExecMsgBroker("", ref messageIn));

            //Market
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkNewQuoteMarket("TESTE17-SL", ref messageIn));
            Assert.True(checkNewMsgMarket("", ref messageIn));
            Assert.True(checkExecMsgMarket(ref messageIn));

            ReqMarketData mdRequestData = new ReqMarketData();
            mdRequestData.symbolList.Add("TESTE17-SL");

            messageOut = fixture._controller.MarketDataRequest(mdRequestData);
            fixture._applicationMarket.SendMessage(messageOut);

            messageIn = getSnapshot();
            Assert.True(checkSnapshotTrade(ref order, ref messageIn));
        }
        
        [Fact]
        // Testar ordenação de ofertas doadoras 
        public void MarketDataRequestOfferSorting()
        {
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            string[] pu = new string[5] {"39", "19", "29", "59", "49"};
            string[] puOrdered = new string[5] {"19", "29", "39", "49", "59"};

            Order order = new Order();
            order.symbol = "TESTE18-SL";
            order.side = '2';
            order.quantity = 5000;
            order.sTax = "1.00";

            Message messageIn = new Message();
            Message messageOut = new Message();

            for (int i = 0; i < pu.Length; i++)
            {
                order.orderId = Utils.GenerateID();
                order.sPU = pu[i];
                messageOut = fixture._controller.MatchNewOrderSingle(order);
                fixture._applicationBroker.SendMessage(messageOut);
                Assert.True(checkNewMsgBroker("", ref messageIn));
                Assert.True(checkNewMsgMarket("", ref messageIn));
                if (i == 0)
                {   
                    Assert.True(checkNewQuoteMarket("TESTE18-SL", ref messageIn));
                }
            }

            ReqMarketData mdRequestData = new ReqMarketData();
            mdRequestData.symbolList.Add("TESTE18-SL");

            messageOut = fixture._controller.MarketDataRequest(mdRequestData);
            fixture._applicationMarket.SendMessage(messageOut);

            messageIn = getSnapshot();

            Assert.True(checkSnapshotOrder(order, puOrdered, messageIn));
        }
        
        [Fact]
        // Testar ordenação de ofertas tomadoras 
        public void MarketDataRequestBidSorting()
        {
            fixture._applicationBroker.ClearQueue();
            fixture._applicationMarket.ClearQueue();
            string[] pu = new string[5] {"39", "19", "29", "59", "49"};
            string[] puOrdered = new string[5] {"59", "49", "39", "29", "19"};

            Order order = new Order();
            order.symbol = "TESTE19-SL";
            order.side = '1';
            order.quantity = 5000;
            order.sTax = "1.00";

            Message messageIn = new Message();
            Message messageOut = new Message();

            for (int i = 0; i < pu.Length; i++)
            {
                order.orderId = Utils.GenerateID();
                order.sPU = pu[i];
                messageOut = fixture._controller.MatchNewOrderSingle(order);
                fixture._applicationBroker.SendMessage(messageOut);
                Assert.True(checkNewMsgBroker("", ref messageIn));
                Assert.True(checkNewMsgMarket("", ref messageIn));
                if (i == 0)
                {   
                    Assert.True(checkNewQuoteMarket("TESTE19-SL", ref messageIn));
                }
            }

            ReqMarketData mdRequestData = new ReqMarketData();
            mdRequestData.symbolList.Add("TESTE19-SL");

            messageOut = fixture._controller.MarketDataRequest(mdRequestData);
            fixture._applicationMarket.SendMessage(messageOut);

            messageIn = getSnapshot();

            Assert.True(checkSnapshotOrder(order, puOrdered, messageIn));
        }

    }

}