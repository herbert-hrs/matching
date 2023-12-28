using QuickFix;
using System;
using System.Threading;

namespace MatchingTest.Initiator
{
    public class App : QuickFix.MessageCracker, QuickFix.IApplication
    {
        private SessionID _sessionID;
        private Controller _controller;
        private EventWaitHandle _eventWait;
        private int _countMsg = 0;
        private int _totMsg;
        private string _waitTypeMsg;

        public App(Controller controler, EventWaitHandle eventWait, int totMsg, string waitTypeMsg)
        {
            _controller = controler;
            _eventWait = eventWait;
            _totMsg = totMsg;
            _waitTypeMsg = waitTypeMsg;

        }

        public int countMsg()
        {
            return _countMsg;
        }

        public void ClearQueue(){
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
        }
        public void ToAdmin(Message message, SessionID sessionID) { 

            //string msgType = message.Header.GetString(35); 
            
        }

        public void FromApp(Message message, SessionID sessionID)
        {
           // string date = DateTime.Now.ToString("yyyyMMdd-HH:mm:ss.fff");
           // Console.WriteLine($"{date} {message.ToString()}");
            
            string msgType = message.Header.GetString(35); 

            if(msgType == _waitTypeMsg)
                _countMsg++;

            if(_countMsg == _totMsg)
            {
                _eventWait.Set();
                _countMsg = 0;

            }
        }

        public void ToApp(Message message, SessionID sessionID)
        {
           // string date = DateTime.Now.ToString("yyyyMMdd-HH:mm:ss.fff");
            //Console.WriteLine($"{date} {message.ToString()}");
        }


        public bool SendMessage(Message message){
            if (message != null && _sessionID != null){
                Session.SendToTarget(message, _sessionID);
                return true;
            }
            return false;
        }
    }
}
