using System;
using QuickFix; 
using QuickFix.Transport; 
using MatchingTest.Initiator;
using SLTools.Util.Config;
using System.Threading;
using System.Collections.Generic;

namespace StressTest 
{
    class Program
    { 
        static private SocketInitiator _initiatorBroker;
        static public Controller _controller;
        static public App _applicationBroker;
        private static EventWaitHandle _eventWait;

        static void TestThroughput(int totMsg)
        {
            
            Console.WriteLine($"Enviando {totMsg} msgs");

            DateTime t1 = DateTime.Now;
            Console.WriteLine($"t1 {t1} {t1.Millisecond}");

            Order order = MatchNewOrderSingleOfferToReplace("1.0");


            for(int count = 1; count <= totMsg; count++)
            {
                //Console.WriteLine($"enviando {count}");
                //MatchNewOrderCross();
                //MatchNewOrderSingleOfferBid();
                MatchOrderReplaceRequest(order);
            }
            Console.WriteLine($"Aguardando {totMsg} msgs");

            _eventWait.WaitOne();

            DateTime t2 = DateTime.Now;
            Console.WriteLine($"t2 {t2} {t2.Millisecond}");


            Console.WriteLine($"time diff {(t2-t1).TotalMilliseconds}");

        }

        static void TestLatency(int totMsg)
        {
            int repeatLoop = 1000;
            double totalTime = 0;
            Console.WriteLine($"Enviando {totMsg} msgs");
            Dictionary<double, int> dictTime = new Dictionary<double, int>();
           
            _eventWait.Reset();
            Order order = MatchNewOrderSingleOfferToReplace("1.0");
            _eventWait.WaitOne();

            for(int loop = 1; loop <= repeatLoop; loop++)
            {
                _eventWait.Reset();

                DateTime t1 = DateTime.Now;
                for(int count = 1; count <= totMsg; count++)
                {
                    //Console.WriteLine($"enviando {count}");
                    //MatchNewOrderCross();
                    //MatchNewOrderSingleOfferBid("1.0");
                    MatchOrderReplaceRequest(order);
                }
                _eventWait.WaitOne();

                DateTime t2 = DateTime.Now;


                //Console.WriteLine($"time diff {(t2-t1).TotalMilliseconds}");
                int key = (int)(t2-t1).TotalMilliseconds;
                if(dictTime.ContainsKey(key))
                    dictTime[key] += 1;
                else
                    dictTime.Add(key, 1);

                totalTime += (t2-t1).TotalMilliseconds;
                
            }

            Console.WriteLine($"totalMsg {repeatLoop} totalTime {totalTime} latency {totalTime/repeatLoop}");
            foreach(var item in dictTime)
                Console.WriteLine($"range {item.Key} valeu {item.Value*100/repeatLoop}% latency");



        }

        static void Main()
        { 
            DotEnv.Load();

            int totMsg = 1;
            string waitTypeMsg = "U405";
            String brokerFile = "./config/broker.cfg";

            _controller = new Controller();
             _eventWait = new ManualResetEvent(false);

            SessionSettings brokerSettings = new SessionSettings(brokerFile);
            _applicationBroker = new App(_controller, _eventWait, totMsg, waitTypeMsg);
            IMessageStoreFactory brokerStoreFactory = new MemoryStoreFactory();
            MessageFactory factory = new MessageFactory();
            //ILogFactory logFactory = new FileLogFactory(brokerSettings);
            ILogFactory logFactory = null;

            _initiatorBroker = new SocketInitiator(_applicationBroker, brokerStoreFactory, brokerSettings, logFactory, factory);
            _initiatorBroker.Start();

            while(!_initiatorBroker.IsLoggedOn)
            {
                Console.WriteLine("aguardando login");
                Thread.Sleep(1000);

            } 

           //TestThroughput(totMsg);
            TestLatency(totMsg);

            _initiatorBroker.Stop();

            

        }   

        public void Dispose()
        {
            _initiatorBroker.Stop();
        }
        

        // Testar o casamento de ofertas na sequência doadora e tomadora
        static public void MatchNewOrderSingleOfferBid(string tax)
        {   

            
            Order order = new Order();
            order.symbol = "TESTE1-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = tax;
            order.sPU = "100";

            Message messageOut = _controller.MatchNewOrderSingle(order);
            _applicationBroker.SendMessage(messageOut);

            Message messageIn = new Message();

            order.orderId = Utils.GenerateID();
            order.side = '1';
            
            messageOut = _controller.MatchNewOrderSingle(order);
            _applicationBroker.SendMessage(messageOut);

        }
        
        
        static public void MatchNewOrderCross()
        {   
            Order order = new Order();
            order.orderOffer = Utils.GenerateID();
            order.orderBid = Utils.GenerateID();
            order.symbol = "TESTE1-SL";
            order.quantity = 100;
            order.sTax = "1.06";
            order.sPU = "100";

            Message messageOut = _controller.MatchNewOrderCross(order);
            _applicationBroker.SendMessage(messageOut);
        }

        static public Order MatchNewOrderSingleOfferToReplace(string tax)
        {   

            
            Order order = new Order();
            order.symbol = "TESTE1-SL";
            order.side = '2';
            order.quantity = 100;
            order.sTax = tax;
            order.sPU = "100";

            Message messageOut = _controller.MatchNewOrderSingle(order);
            _applicationBroker.SendMessage(messageOut);
            return order;

        }
        
        static public void MatchOrderReplaceRequest(Order order)
        {   
            order.sTax = "1.07";

            Message messageOut = _controller.MatchOrderReplaceRequest(order);
            _applicationBroker.SendMessage(messageOut);

        }
        
    }
}