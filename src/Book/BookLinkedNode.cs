using System;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using QuickFix;
using QuickFix.Fields;
using System.Collections.Generic;
using Serilog;

namespace Matching
{
    public class BookLinkedNode : BookNode
    {
        
        public BookLinkedNode(Instrument instrument, Notifier notifier, Params p)
            :base(instrument, notifier, p)
        {
            
        }

        private int BrokerMatch(BookLine bid, BookLine offer, char sideAgressor)
        {
            try
            {

                char orderProfileAgressor = (sideAgressor == Side.BUY) ? bid.OrderProfile: offer.OrderProfile;
                char orderProfileOfertante = (sideAgressor == Side.SELL) ? bid.OrderProfile: offer.OrderProfile;

                if(bid.OrderProfile == OrderProfile.BROKER && offer.OrderProfile == OrderProfile.BROKER)
                    return 0;

                if(bid.BrokerID == offer.BrokerID)
                {
                    if(orderProfileAgressor == OrderProfile.MANAGER || orderProfileAgressor == OrderProfile.BROKER)
                    {

                        return bid.BrokerID;
                    }
                    //dealer
                    else if((sideAgressor == Side.BUY && bid.ManagerID != 0) || (sideAgressor == Side.SELL && offer.ManagerID != 0))
                    {
                        return bid.BrokerID;
                    }
                }

                string jsonToSend = JsonConvert.SerializeObject(new {
                    order_profile_aggressor = orderProfileAgressor,
                    order_profile_ofertante = orderProfileOfertante,
                    broker_id_aggressor = sideAgressor == Side.BUY ? bid.BrokerID: offer.BrokerID,
                    manager_id_buyer = bid.ManagerID,
                    manager_id_seller = offer.ManagerID,
                    broker_id_buyer = bid.BrokerID,
                    broker_id_seller = offer.BrokerID
                });

                Log.Debug(jsonToSend);


                HttpClient auth = new(new HttpClientHandler{})
                {
                    BaseAddress = new Uri(_params.BrokerMatchApi)
                };

                string url = "/broker/match";

                HttpResponseMessage response = auth.PostAsync(url, new StringContent(jsonToSend, Encoding.UTF8, "application/json")).Result;

                if (response.IsSuccessStatusCode)
                {
                    BrokerMatchResponse brokerResponse = JsonConvert.DeserializeObject<BrokerMatchResponse>(response.Content.ReadAsStringAsync().Result);
                    
                    
                    Log.Debug($"broker id {brokerResponse.broker_id}");
                    
                    return brokerResponse.broker_id;
                }
                else
                {
                    _notifier?.NotifyLog("BookLinkedNode::MatchBroker() >> Error: " + response.Content.ReadAsStringAsync().Result);
                    return 0;
                }
            }
            catch (Exception e)
            {
                _notifier?.NotifyLog("BookLinkedNode::MatchBroker() >> Error: " + e.Message);
                return 0;
            }

        }
         

        private static bool CheckMatch(BookLine bid, BookLine offer)
        {
            if (bid.Tax > offer.Tax)
                return false;

            return true;
        }

        private int GetBrokerID(BookLine bid, BookLine offer, char side)
        {
            
            int brokerID = BrokerMatch(bid, offer, side);
            
            if(brokerID == 0)
            {
                _notifier?.NotifyLog("BookLinkedNode::Match() >> Error: brokerID not found");
                return -1;
            }

            return brokerID;

        }


        private bool Match(Message msg, BookLine bid, BookLine offer, DateTime transactTrade, int iBrokerID)
        {
            if (_disposed)
                return false;

            // Conversão para eliminar casas decimais anômalas
            int qty = bid.LeavesQty < offer.LeavesQty ? bid.LeavesQty : offer.LeavesQty;
            string uniqueTradeID = Generator.GetTradeID();
            bool offerIsAttack = (bid.ArrivalTime < offer.ArrivalTime);
            decimal tax = offerIsAttack ? Translator.ConvertPrice(bid.Tax) : Translator.ConvertPrice(offer.Tax);
            decimal pu = offerIsAttack ? Translator.ConvertPrice(bid.PU) : Translator.ConvertPrice(offer.PU);

            
            // Consome as partes com melhores preços
            bid.Trade(qty, tax, pu, uniqueTradeID, offerIsAttack, transactTrade, iBrokerID);
            offer.Trade(qty, tax, pu, uniqueTradeID, offerIsAttack, transactTrade, iBrokerID);

            // Cria o negócio realizado
            var trade = new Trade(_instrument, qty, tax, pu, uniqueTradeID, transactTrade, TradeStatus.CONFIRMED, OrigTrade.BOOK);

            // Adiciona à lista de negócios realizados
            _idsTrade.Add(uniqueTradeID, trade);
            _listTrade.Add(trade);


            _notifier.NotifyBroker(Translator.BrokerReport(bid), bid.Symbol);
            _notifier.NotifyBroker(Translator.BrokerReport(offer), offer.Symbol);
            
            msg.AddGroup(Translator.BuildNode(bid, bid.Action));
            msg.AddGroup(Translator.BuildNode(offer, offer.Action));

            msg.AddGroup(Translator.BuildNode(trade, new MDUpdateAction(MDUpdateAction.NEW)));
            
            return true;
        }

        private bool CheckMatchSide(BookLine line)
        {
            if (line.Side == Side.BUY)
            {
                if(_listOffer.Count > 0)
                {
                    if(CheckMatch(line,_listOffer[0]))
                        return true;
                    
                    return false;
                }
            }
            else
            {
                if(_listBid.Count > 0)
                {
                    if(CheckMatch(_listBid[0], line))
                        return true;

                    return false;
                }
            }

            return false;
        }

        private int MatchFull(Message msgOut, BookLine line, DateTime transactTrade)
        {
            int posMatch = 0;
            int posOferta = 1;

            if (line.Side == Side.BUY)
            {
                List<BookLine> removeOffer = new List<BookLine>();
                foreach(var offer in _listOffer)
                {
                    if(line.LeavesQty == 0)
                        break;

                    if(!CheckMatch(line, offer))
                        break;
                    
                    int iBrokerID = this.GetBrokerID(line, offer, line.Side);

                    if(iBrokerID < 0)
                    {
                        posOferta += 1;
                        continue;
                    }

                    if(posMatch == 0)
                        _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                    offer.Position = posOferta;
                    this.Match(msgOut, line, offer, transactTrade, iBrokerID );

                    if (offer.Status.Obj == OrderStatus.FILLED)
                        removeOffer.Add(offer);
                    else
                        posOferta += 1;

                    posMatch += 1;
                }

                foreach(var offer in removeOffer)
                    _listOffer.Remove(offer);
            }
            else
            {
                List<BookLine> removeBid = new List<BookLine>();

                foreach(var bid in _listBid)
                {
                    if(line.LeavesQty == 0)
                        break;

                    if(!CheckMatch(bid, line))
                        break;

                    int iBrokerID = this.GetBrokerID(bid, line, line.Side);

                    if(iBrokerID < 0)
                    {
                        posOferta += 1;
                        continue;
                    }

                    if(posMatch == 0)
                        _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                    bid.Position = posOferta;
                    this.Match(msgOut, bid, line, transactTrade, iBrokerID );

                    if (bid.Status.Obj == OrderStatus.FILLED)
                        removeBid.Add(bid);
                    else
                        posOferta += 1;

                    posMatch += 1;
                }

                foreach(var bid in removeBid)
                    _listBid.Remove(bid);
            }

            if(posMatch > 0)
            {
                if(line.LeavesQty > 0)
                {
                    if (line.Side == Side.BUY && _listOffer.Count > 0)
                    {
                        if(CheckMatch(line,_listOffer[0] ))
                        {
                            line.PartialCancel();

                            _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);
                            msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.DELETE)));

                        }

                    }
                    else if (line.Side == Side.SELL && _listBid.Count > 0)
                    {
                        if(CheckMatch(_listBid[0], line))
                        {
                            line.PartialCancel();
                            _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);
                            msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.DELETE)));
                        }

                    }
                    
                }
            }
            else
            {
                line.Cancel();
            }

            if (line.Action.Obj == MDUpdateAction.DELETE)
            {
                if (line.Side == Side.BUY)
                    _listBid.Remove(line);
                else
                    _listOffer.Remove(line);
            }

            return posMatch;

        }

        public override bool InsertOrder(Message message)
        {
            if (_disposed)
                return false;

            var quantity = message.GetString(Tags.Quantity);

            if (Convert.ToInt32(quantity) <= 0)
            {
                _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: " + quantity  + " is not a valid quantity!");
                RejectOrder(message);

                return false;
            }

            if (message.IsSetField(Tags.SPU))
            {
                var pu = message.GetString(Tags.SPU);
                if (Convert.ToDecimal(pu) <= 0)
                {
                    _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: " + pu  + " is not a valid pu!");
                    RejectOrder(message);
                    return false;
                }
            }

            if (!message.IsSetField(Tags.OrderProfile))
            {
                _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: OrderProfile is empty!");
                RejectOrder(message);
                return false;

            }

            string orderID = message.GetString(Tags.OrderID);

            if (!_ids.ContainsKey(orderID))
            {
                BookLine line = new BookLine(message, _instrument, _params);

                lock (_sync)
                {

                    if(line.Side == Side.BUY)
                    {
                        _listBid.Add(line);
                        _listBid.Sort();
                        line.Position = _listBid.IndexOf(line) + 1;
                    }
                    else
                    {
                        _listOffer.Add(line);
                        _listOffer.Sort();
                        line.Position = _listOffer.IndexOf(line) + 1;
                    }
                    line.SecondaryOrderID = Generator.GetSecOrderID();
                    
                    _ids.Add(orderID, line);

                    var msgOut = new QuickFix.FIX44.MarketDataIncrementalRefresh();
                    
                    DateTime transactTrade = DateTime.Now;
                    if(message.IsSetField(Tags.TransactTime))
                        transactTrade = message.GetDateTime(Tags.TransactTime);

                    msgOut.Set(new TradeDate(transactTrade.ToString("yyyyMMdd")));


                    if(!CheckMatchSide(line))
                    {
                        _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                        msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.NEW)));

                        this.SendMarket(msgOut, line.Symbol);
                    }
                    else
                    {
                        msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.NEW)));

                        if(MatchFull(msgOut, line, transactTrade) == 0)
                        {
                            _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: " + orderID  + " not a single match!");
                            RejectOrder(message);
                            return false;
                        }
                        this.SendMarket(msgOut, line.Symbol);

                    }
                }      
                return true;
            }
            else
            {
                _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: OrderID [" + orderID + "] already exists!");
                RejectOrder(message);
                return false;
            }
        }

        public override bool ReplaceOrder(Message message)
        {
            if (_disposed)
                return false;

            var quantity = message.GetString(Tags.Quantity);

            if (Convert.ToInt32(quantity) <= 0)
            {
                _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: " + quantity  + " is not a valid quantity!");
                RejectOrder(message);

                return false;
            }

            if (message.IsSetField(Tags.SPU))
            {
                var pu = message.GetString(Tags.SPU);
                if (Convert.ToDecimal(pu) <= 0)
                {
                    _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: " + pu  + " is not a valid pu!");
                    RejectOrder(message);
                    return false;
                }
            }

            var orderID = message.GetString(Tags.OrderID);

            if (_ids.ContainsKey(orderID))
            {
                lock (_sync)
                {
                    var line = _ids[orderID];
                    var side = message.GetString(Tags.Side);

                    if(side[0] != line.Side)
                    {
                        _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] wrong Side");
                        RejectOrder(message);
                        return false;
                    }

                    int old_position;

                    // Ordena para pegar a posição atual da oferta
                    if (line.Side == Side.BUY)
                        old_position = _listBid.IndexOf(line) + 1;
                    else
                        old_position = _listOffer.IndexOf(line) + 1;

                    if (old_position == 0)
                    {
                        _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] is not in the book!");
                        RejectOrder(message);
                        return false;
                    }

                    line.Position = old_position;

                    if (!line.Replace(message))
                    {
                        _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] invalid Type");
                        RejectOrder(message);
                        return false;
                    }
                    line.SecondaryOrderID = Generator.GetSecOrderID();

                    int new_position;
                    var old = new BookLine(line);

                    // Ordena para pegar a posição depois da edição
                    if (line.Side == Side.BUY)
                    {
                        _listBid.Sort();
                        new_position = _listBid.IndexOf(line) + 1;
                    }
                    else
                    {
                        _listOffer.Sort();
                        new_position = _listOffer.IndexOf(line) + 1;
                    }

                    line.Position = new_position;

                    var msgOut = new QuickFix.FIX44.MarketDataIncrementalRefresh();

                    DateTime transactTrade = DateTime.Now;
                    if(message.IsSetField(Tags.TransactTime))
                        transactTrade = message.GetDateTime(Tags.TransactTime);

                    msgOut.Set(new TradeDate(transactTrade.ToString("yyyyMMdd")));

                    
                    old.Dispose();

                    if (old_position != new_position)
                    {
                        msgOut.AddGroup(Translator.BuildNode(old, new MDUpdateAction(MDUpdateAction.DELETE)));
                        msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.NEW)));
                    }
                    else
                    {
                        msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.CHANGE)));
                    }


                    if(!CheckMatchSide(line))
                    {
                        _notifier.NotifyBroker(Translator.BrokerReport(line), line.Symbol);

                        SendMarket(msgOut, line.Symbol);
                    }
                    else
                    {
                        if(MatchFull(msgOut, line, transactTrade) == 0)
                        {
                            msgOut.AddGroup(Translator.BuildNode(line, new MDUpdateAction(MDUpdateAction.DELETE)));
                            _notifier?.NotifyLog("BookLinkedNode::InsertOrder() >> Error: " + orderID  + " not a single match!");
                            RejectOrder(message);
                            SendMarket(msgOut, line.Symbol);
                        }
                        else
                        {
                            SendMarket(msgOut, line.Symbol);
                        }

                    }


                    return true;
                }
            }
            else
            {
                RejectOrder(message);
                _notifier?.NotifyLog("BookLinkedNode::ReplaceOrder() >> Error: OrderID [" + orderID + "] not identified!");
                return false;
            }
        }
    }
}
