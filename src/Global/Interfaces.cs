using QuickFix;
using System;

namespace Matching
{
    /// <summary>
    /// Interface para o gerenciador de logs
    /// </summary>
    public interface ILogManager
    {
        void OnLog(string msg);
    }

    /// <summary>
    /// Interface para o gerenciador de tarefas
    /// </summary>
    public interface IPool
    {
        void Receive(IWrap wrap);
    }

    /// <summary>
    /// Interface para pacotes de mensagens
    /// </summary>
    public interface IWrap : IDisposable
    {
        string GetFlag();
        void Analyze();
    }

    /// <summary>
    /// Interface para notificação de negociação
    /// </summary>
    public interface IBrokerProvider
    {
        void NotifyBroker(Message message);
    }

    /// <summary>
    /// Interface para notificação de Market Data
    /// </summary>
    public interface IMarketProvider
    {
        void NotifyMarket(Message message);
        void NotifyAllMarket(Message message);
        
    }

    /// <summary>
    /// Interface do livro que recebe requisição
    /// de clientes de Negociação
    /// </summary>
    public interface IBrokerBook
    {   
        bool CleanupTest(Message message);
        bool ReloadMarket(Message message);
        void InsertOrder(Message message, bool notifyBase);
        void ReplaceOrder(Message message, bool notifyBase);
        void CancelOrder(Message message, bool notifyBase);
        void CrossTrade(Message message, bool notifyBase);
        void CancelTrade(Message message, bool notifyBase);
        void UpdateTrade(Message message, bool notifyBase);
    }

    /// <summary>
    /// Interface do livro que recebe requisição
    /// do cliente de Market Data
    /// </summary>
    public interface IMarketBook
    {
        void SecurityRequest(Message message, SessionID sessionID);
        void MarketDataRequest(Message message, SessionID sessionID);
    }

    /// <summary>
    /// Interface do livro que recebe requisição
    /// do cliente de Market Data
    /// </summary>
    public interface IPersistence
    {
        void SaveOrder(Message message);
    }
}
