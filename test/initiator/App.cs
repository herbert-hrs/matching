using System;
using System.IO;
using QuickFix;
using QuickFix.Fields;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace MatchingTest.Initiator
{
    public class App : QuickFix.MessageCracker, QuickFix.IApplication
    {
        private SessionID _sessionID;
        private Controller _controller;
        private bool bQueue = false;

        ConcurrentQueue<Message> queueMessage = new ConcurrentQueue<Message>();

        public App(Controller controler, bool queue){
            _controller = controler;
            bQueue = queue;

        }

        public void ClearQueue(){
            queueMessage.Clear();
        }

        public void OnCreate(SessionID sessionID)
        {
        }

        public void OnConnect(SocketReader sr, Message message, SessionID sessionID)
        {
        }

        public void OnLogon(SessionID sessionID) { 
            _sessionID = sessionID;
        }
        public void OnLogout(SessionID sessionID) { 
        }

        public void FromAdmin(Message message, SessionID sessionID) {
            if(bQueue) 
                queueMessage.Enqueue(message);
        }
        public void ToAdmin(Message message, SessionID sessionID) { 

            string msgType = message.Header.GetString(35); 
        }

        public void FromApp(Message message, SessionID sessionID)
        {
            if(bQueue) 
                queueMessage.Enqueue(message);
        }

        public void ToApp(Message message, SessionID sessionID)
        {
        }


        public bool SendMessage(Message message){
            if (message != null && _sessionID != null){
                Session.SendToTarget(message, _sessionID);
                return true;
            }
            return false;
        }

        public bool ReadMessage(ref Message message){
            return queueMessage.TryDequeue(out message);
        }

        public bool WaitUntilFirstMsgType(String waitMsgType, String waitOrderID, ref Message message, int count = 8)
        {
            while(count > 0){
                while(!this.ReadMessage(ref message))
                {
                    Thread.Sleep(100);
                    count--;
                }
                String msgType = message.Header.GetString(Tags.MsgType); 

                if (msgType == waitMsgType){
                    if(waitOrderID != ""){
                        if(message.IsSetField(Tags.OrderID))
                        {
                            String orderID = message.GetString(Tags.OrderID);
                            if(orderID == waitOrderID)
                            {
                                return true;
                            }
                        }
                    }else{
                        return true;
                    }
                }
                count--;
            }
            return false;
        }
    }
}
