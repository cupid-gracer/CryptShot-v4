using Binance.Net;
using Binance.Net.Objects.Spot;
using CryptoExchange.Net.Authentication;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AuthGG;
using System.Diagnostics;
using System.Threading;

namespace CryptShot
{
    class Program
    {
        static Logic.Settings f = Logic.Settings.Load();
        static NumberStyles style = NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
        static CultureInfo provider = CultureInfo.InvariantCulture;
        static bool isPressESC = false;
        static void Main(string[] args)
        {
            Logic.printTitle();

            OnProgramStart.Initialize("CryptShot", "500878", "LdsffTlh5Ra6g3R7VaDRQHSRAzuYrtpXuXR", "1.0");

            bool success = false;

            if (!string.IsNullOrEmpty(f.LoginKey))
            {
                if (API.AIO(f.LoginKey))
                {
                    success = true;
                }
                else
                {
                    Logic.colorOutput(ConsoleColor.Red, "Your key does not exist!");
                    Process.GetCurrentProcess().Kill(); // closes the application
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("AIO Key: ");
                Console.ForegroundColor = ConsoleColor.Gray;
                string KEY = Console.ReadLine();
                Console.WriteLine();

                if (API.AIO(KEY))
                {
                    if (f.LoginKey == null || f.LoginKey == "")
                    {
                        f.LoginKey = KEY;
                        f.Save();
                    }

                    success = true;
                }
                else
                {
                    Logic.colorOutput(ConsoleColor.Red, "Your key does not exist!");
                    Process.GetCurrentProcess().Kill(); // closes the application
                }
            }
            //bool success = true;

            if (success)
            {
                Logic.colorOutput(ConsoleColor.Green, "Successfully logged in.");
                handleExchangeInfo();

                var client = new BinanceClient(new BinanceClientOptions
                {
                    ApiCredentials = new ApiCredentials(f.APIKey, f.APISecret),
                    LogWriters = new List<TextWriter> { Console.Out }
                });
                while (true)
                {
                    WorkToDo(client);
                }
            }


        }

        static void handleExchangeInfo()
        {
            Logic.colorOutput(ConsoleColor.Yellow, "Renewing ExchangeInfo...");

            string path = "exchange_info.txt";
            if (!File.Exists(path))
                File.Create(path).Close();
            else
            {
                File.Delete(path);
                File.Create(path).Close();
            }

            using (WebClient wb = new WebClient())
            {
                string response = wb.DownloadString("https://api.binance.com/api/v1/exchangeInfo");
                using (StreamWriter sw = new StreamWriter(path, true))
                {
                    sw.AutoFlush = true;
                    sw.WriteLine(response);
                    sw.Close();
                }
            }

            Logic.colorOutput(ConsoleColor.Green, "Successfully renewed.");
        }

        static void captureESC()
        {
            ConsoleKeyInfo cki;
            while (true)
            {
                cki = Console.ReadKey();
                if (cki.Key == ConsoleKey.Escape) isPressESC = true;
            }
        }
    
        static void WorkToDo(BinanceClient client)
        {
            try
            {
                Logic.colorOutput(ConsoleColor.White, "Input your PAIR: ");
                string pair = Console.ReadLine().ToUpper();

                if (!pair.Contains("BTC"))
                    pair = pair + "BTC";

                int precision = 0;
                decimal minPrice = 0;
                decimal min_Notional = 0;
                decimal lot_size = 0;
                decimal multiplierUp = 0;
                decimal multiplierDown = 0;
                decimal WeightedAveragePrice = 0;

                JObject i = JObject.Parse(File.ReadAllText("exchange_info.txt"));
                JArray o = JArray.Parse(i["symbols"].ToString());

                foreach (JObject jObject in o)
                {
                    if ((string)jObject["symbol"] == pair)
                    {
                        precision = (int)jObject["baseAssetPrecision"];
                        minPrice = decimal.Parse((string)jObject["filters"][0]["minPrice"], style, provider);
                        lot_size = decimal.Parse((string)jObject["filters"][2]["minQty"], style, provider);
                        min_Notional = decimal.Parse((string)jObject["filters"][3]["minNotional"], style, provider);
                        multiplierUp = decimal.Parse((string)jObject["filters"][1]["multiplierUp"], style, provider);
                        multiplierDown = decimal.Parse((string)jObject["filters"][1]["multiplierDown"], style, provider);
                        break;
                    }
                    // WeightedAveragePrice = client.Spot.Market.Get24HPrice(pair).Data.WeightedAveragePrice;
                }

                decimal quantityBTC = Decimal.Parse(f.Quantity, style, provider);

                decimal decimalFormat = getAllZeros(minPrice);
                string format = "{0:" + decimalFormat + "}";
                string newFormat = format.Replace(",", ".");

                int limitBuyQtyPrecision = getQtyPrecision(lot_size);
                //if (true)
                if (quantityBTC > min_Notional)
                {
                    decimal takeProfit = Decimal.Parse(f.TakeProfitRate, style, provider);
                    decimal StopPrice = Decimal.Parse(f.LimitPriceRate, style, provider);
                    decimal StopLossPrice = Decimal.Parse(f.StopLossRate, style, provider);
                    decimal buyLimitPrice = Decimal.Parse(f.BuyLimitPriceRate, style, provider);
                    decimal sellLimitRate = Decimal.Parse(f.LimitSellRate, style, provider);
                    int marketSellDelaySec = int.Parse(f.MarketSellDelaySec);

                    Logic.colorOutput(ConsoleColor.Blue, "Start Get Market Price.");
                    decimal price = client.Spot.Market.GetPrice(pair).Data.Price;
                    Logic.colorOutput(ConsoleColor.Blue, "End Get Market Price.");


                    Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {price} BTC");
                    client.Spot.Order.GetAllOrders(pair);
                    CryptoExchange.Net.Objects.WebCallResult<Binance.Net.Objects.Spot.SpotData.BinancePlacedOrder> order;

                    

                    //return;
                    // limit buy
                    if (f.BuyType == "1")
                    {
                        decimal temp_limitPrice = Math.Round(buyLimitPrice * price, precision);
                        string str_limitPrice = String.Format(newFormat, temp_limitPrice).Replace(",", ".");
                        decimal limitPrice = Decimal.Parse(str_limitPrice, style, provider);
                        decimal quantity = Math.Round(quantityBTC / limitPrice, limitBuyQtyPrecision, MidpointRounding.AwayFromZero);
                        
                        Logic.colorOutput(ConsoleColor.Blue, $"Buy Limit. quantity: {quantity}, limit price: {limitPrice}");
                        order = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Limit, quantity, null, null, limitPrice, Binance.Net.Enums.TimeInForce.GoodTillCancel, null, null, Binance.Net.Enums.OrderResponseType.Full);
                        if (order.Error == null)
                        {
                            int sdf = order.Data.Fills.ToList().Count();
                            var fills = order.Data.Fills.ToList();
                            int fills_len = fills.Count;
                            price = 0;
                            for (int k = 0; k < fills_len; k++)
                            {
                                price += fills[k].Price;
                            }
                            price = price / fills_len;
                            Logic.colorOutput(ConsoleColor.Green, $"AVG Price for {pair} is {price} BTC");

                        }

                    }
                    else
                    {
                        // market buy
                        Logic.colorOutput(ConsoleColor.Blue, "Buy Market.");
                        order = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Market, null, quantityBTC, null, null, null, null, null, Binance.Net.Enums.OrderResponseType.Full);
                        if (order.Error == null)
                        {
                            int sdf = order.Data.Fills.ToList().Count();
                            var fills = order.Data.Fills.ToList();
                            int fills_len = fills.Count;
                            price = 0;
                            for (int k = 0; k < fills_len; k++)
                            {
                                price += fills[k].Price;
                            }
                            price = price / fills_len;
                            Logic.colorOutput(ConsoleColor.Green, $"AVG Price for {pair} is {price} BTC");
                        }
                    }
                    
                    // = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Market, decimal.Parse(f.Quantity, style, provider), null, null, price);
                    // var order = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Market, null, quantityBTC, null, null, null, null, null, Binance.Net.Enums.OrderResponseType.Full);
                    // var order = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Limit, quantity, null, null, limitPrice, Binance.Net.Enums.TimeInForce.GoodTillCancel, null, null, Binance.Net.Enums.OrderResponseType.Full);

                    if (order.Error != null)
                    {
                        Logic.colorOutput(ConsoleColor.Yellow, order.Error.Message);
                        return;
                    }

                    while (client.Spot.Order.GetOrder(pair, order.Data.OrderId, order.Data.ClientOrderId).Data?.QuantityFilled == 0)
                    {
                        Console.WriteLine("Waiting for filling...");
                        Task.Delay(10).Wait();
                    }

                   





                    //var getOrderData = client.Spot.Order.GetOrder(pair, order.Data.OrderId, order.Data.ClientOrderId);
                    //decimal priceOfTheCoins = getOrderData.Data.Price;
                    decimal quantityOfCoins = order.Data.Quantity;

                    decimal btcFilled;
                    if (f.HasBNB)
                        btcFilled = order.Data.QuoteQuantityFilled;
                    else
                        btcFilled = order.Data.QuoteQuantityFilled * decimal.Parse("0.9995", style, provider);

                    Logic.colorOutput(ConsoleColor.Green, $"ORDER SUBMITTED, GOT: {quantityOfCoins} coins from {pair} at {btcFilled} BTC");



                    decimal sellPrice = 0;
                    decimal sellStopPrice = 0;
                    decimal sellStopLimitPrice = 0;

                    switch (f.SellType)
                    {
                        case "1":
                            if (f.BuyType == "1")
                            {
                                Logic.colorOutput(ConsoleColor.Blue, "Start Get WeightedAveragePrice.");
                                WeightedAveragePrice = client.Spot.Market.Get24HPrice(pair).Data.WeightedAveragePrice;
                                Logic.colorOutput(ConsoleColor.Blue, "End Get WeightedAveragePrice.");
                                sellPrice = solveQuantityCoins(Math.Round(takeProfit * WeightedAveragePrice, precision), precision);
                                sellStopPrice = solveQuantityCoins(Math.Round(StopPrice * WeightedAveragePrice, precision), precision);
                                sellStopLimitPrice = solveQuantityCoins(Math.Round(StopLossPrice * WeightedAveragePrice, precision), precision);
                            }
                            else
                            {
                                sellPrice = solveQuantityCoins(Math.Round(takeProfit * price, precision), precision);
                                sellStopPrice = solveQuantityCoins(Math.Round(StopPrice * price, precision), precision);
                                sellStopLimitPrice = solveQuantityCoins(Math.Round(StopLossPrice * price, precision), precision);
                            }
                            break;
                        case "2":
                            sellPrice = solveQuantityCoins(Math.Round(sellLimitRate * price, precision), precision);
                            break;
                        case "3":

                            Logic.colorOutput(ConsoleColor.Blue, $"WAIT {marketSellDelaySec} SECONDS, BEFORE MARKET SELL");
                            Task.Delay(marketSellDelaySec * 1000).Wait();
                            break;

                        default:
                            break;
                    }





                    string one = String.Format(newFormat, sellPrice).Replace(",", ".");
                    string two = String.Format(newFormat, sellStopPrice).Replace(",", ".");
                    string three = String.Format(newFormat, sellStopLimitPrice).Replace(",", ".");

                    decimal onee = Decimal.Parse(one, style, provider);
                    decimal twoo = Decimal.Parse(two, style, provider);
                    decimal threee = Decimal.Parse(three, style, provider);

                    if (twoo == threee)
                        threee = threee - minPrice;





                    if (f.SellType == "1")
                    {
                        // oco
                        quantityOfCoins = solveQuantityCoins(quantityOfCoins, precision);
                        Logic.colorOutput(ConsoleColor.Blue, "Start PlaceOcoOrder.");
                        var sellOrder = client.Spot.Order.PlaceOcoOrder(pair, Binance.Net.Enums.OrderSide.Sell, quantityOfCoins, onee, twoo, threee, null, null, null, null, null, Binance.Net.Enums.TimeInForce.GoodTillCancel);
                        Logic.colorOutput(ConsoleColor.Blue, "End PlaceOcoOrder.");


                        bool success = true;
                        while (sellOrder.Error != null)
                        {
                            if (sellOrder.Error.Message == "Account has insufficient balance for requested action.")
                            {
                                Console.WriteLine("submitting again...");
                                quantityOfCoins = quantityOfCoins - lot_size;
                                sellOrder = client.Spot.Order.PlaceOcoOrder(pair, Binance.Net.Enums.OrderSide.Sell, quantityOfCoins, onee, twoo, threee, null, null, null, null, null, Binance.Net.Enums.TimeInForce.GoodTillCancel);
                            }
                            else if (sellOrder.Error.Message == "Filter failure: PERCENT_PRICE")
                            {
                                Logic.colorOutput(ConsoleColor.Yellow, "Filter failure: PERCENT_PRICE, submitting again...");
                                sellOrder = client.Spot.Order.PlaceOcoOrder(pair, Binance.Net.Enums.OrderSide.Sell, quantityOfCoins, onee, twoo, threee, null, null, null, null, null, Binance.Net.Enums.TimeInForce.GoodTillCancel);


                                Logic.colorOutput(ConsoleColor.Yellow, $"sellPrice: { onee }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");
                                Logic.colorOutput(ConsoleColor.Yellow, $"sellStopPrice: { twoo }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");
                                Logic.colorOutput(ConsoleColor.Yellow, $"sellStopLimitPrice: { threee }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");

                            }
                            else
                            {
                                Console.WriteLine(sellOrder.Error.Message);
                                success = false;
                                break;
                            }
                        }

                        if (success)
                            Logic.colorOutput(ConsoleColor.Green, $"OCO ORDER SUBMITTED, SELL PRICE: {onee} | STOP PRICE: {twoo} | STOP LIMIT PRICE: {threee}");
                        else
                            Logic.colorOutput(ConsoleColor.Red, $"OCO ORDER FAILED, FIND THE ERROR MESSAGE HERE ^^");
                    }
                    else if (f.SellType == "2")
                    {
                        // limit sell
                        var sellOrder = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Sell, Binance.Net.Enums.OrderType.Limit, quantityOfCoins, null, null, onee, Binance.Net.Enums.TimeInForce.GoodTillCancel, null, null, Binance.Net.Enums.OrderResponseType.Full);
                        if (sellOrder.Error != null)
                        {
                            Console.WriteLine(sellOrder.Error.Message);
                            Logic.colorOutput(ConsoleColor.Red, $"LIMIT SELL ORDER FAILED, FIND THE ERROR MESSAGE HERE ^^");
                        }

                        Logic.colorOutput(ConsoleColor.Green, $"LIMIT SELL ORDER SUBMITTED, SELL PRICE: {onee}");

                    }
                    else
                    {
                        // market sell
                        var sellOrder = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Sell, Binance.Net.Enums.OrderType.Market, quantityOfCoins, null, null, null, null, null, null, Binance.Net.Enums.OrderResponseType.Full);
                        if (sellOrder.Error != null)
                        {
                            Console.WriteLine(sellOrder.Error.Message);
                            Logic.colorOutput(ConsoleColor.Red, $"MARKET SELL ORDER FAILED, FIND THE ERROR MESSAGE HERE ^^");
                        }

                        Logic.colorOutput(ConsoleColor.Green, $"MARKET SELL ORDER SUBMITTED, SELL PRICE: {sellOrder.Data.QuoteQuantityFilled / quantityOfCoins} | Total: {sellOrder.Data.QuoteQuantityFilled}");
                    }


                    if (f.isRefreshOrder == "1")
                    {


                        //while (f.isStreaming == "1" && !Console.KeyAvailable)
                        Thread escThread = new Thread(captureESC);
                        escThread.Start();

                        while (f.isStreaming == "1" && !isPressESC)
                        {
                            price = client.Spot.Market.GetPrice(pair).Data.Price;
                            //Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {client.Spot.Market.GetPrice(pair).Data.Price} BTC");
                            Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {price} BTC");
                            Task.Delay(10).Wait();
                        }
                        if (isPressESC)
                        {
                            escThread.Abort();
                        client.Spot.Order.CancelAllOpenOrders(pair);
                        Logic.colorOutput(ConsoleColor.Green, "SELL ORDERS CANCELLED!");
                            var sellOrder = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Sell, Binance.Net.Enums.OrderType.Market, quantityOfCoins, null, null, null, null, null, null, Binance.Net.Enums.OrderResponseType.Full);
                            if (sellOrder.Error != null)
                            {
                                Console.WriteLine(sellOrder.Error.Message);
                                Logic.colorOutput(ConsoleColor.Red, $"MARKET SELL ORDER FAILED, FIND THE ERROR MESSAGE HERE ^^");
                            }
                            Logic.colorOutput(ConsoleColor.Green, $"MARKET SELL ORDER SUBMITTED, SELL PRICE: {sellOrder.Data.QuoteQuantityFilled / quantityOfCoins} | Total: {sellOrder.Data.QuoteQuantityFilled}");

                            isPressESC = false;
                        }
                    }
                    else
                    {
                        while (f.isStreaming == "1" && !Console.KeyAvailable)
                        {
                            price = client.Spot.Market.GetPrice(pair).Data.Price;
                            //Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {client.Spot.Market.GetPrice(pair).Data.Price} BTC");
                            Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {price} BTC");
                            Task.Delay(10).Wait();
                        }
                    }



                }
                else
                {
                    Logic.colorOutput(ConsoleColor.Red, $"Didn't purchase cuz MIN_NOTIONAL {min_Notional} > {quantityBTC} QUANTITY BTC.");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void WorkToDo_V1(BinanceClient client)
        {
            try
            {
                Logic.colorOutput(ConsoleColor.White, "Input your PAIR: ");
                string pair = Console.ReadLine().ToUpper();

                if (!pair.Contains("BTC"))
                    pair = pair + "BTC";

                decimal price = client.Spot.Market.GetPrice(pair).Data.Price;

                Logic.colorOutput(ConsoleColor.Green, $"Price for {pair} is {price} BTC");
                decimal takeProfit = Decimal.Parse(f.TakeProfitRate, style, provider);
                decimal StopPrice = Decimal.Parse(f.LimitPriceRate, style, provider);
                decimal StopLossPrice = Decimal.Parse(f.StopLossRate, style, provider);

                // = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Market, decimal.Parse(f.Quantity, style, provider), null, null, price);
                var order = client.Spot.Order.PlaceOrder(pair, Binance.Net.Enums.OrderSide.Buy, Binance.Net.Enums.OrderType.Market, null, Decimal.Parse(f.Quantity, style, provider), null, null, null, null, null, Binance.Net.Enums.OrderResponseType.Full);

                while (client.Spot.Order.GetOrder(pair, order.Data.OrderId, order.Data.ClientOrderId).Data.QuantityFilled == 0)
                {
                    Console.WriteLine("Waiting for filling...");
                    Task.Delay(100).Wait();
                }

                //var getOrderData = client.Spot.Order.GetOrder(pair, order.Data.OrderId, order.Data.ClientOrderId);
                //decimal priceOfTheCoins = getOrderData.Data.Price;
                decimal quantityOfCoins = order.Data.Quantity;

                decimal btcFilled;
                if (f.HasBNB)
                    btcFilled = order.Data.QuoteQuantityFilled;
                else
                    btcFilled = order.Data.QuoteQuantityFilled * decimal.Parse("0.9995", style, provider);

                Logic.colorOutput(ConsoleColor.Green, $"ORDER SUBMITTED, GOT: {quantityOfCoins} coins from {pair} at {btcFilled} BTC");

                int precision = 0;
                decimal minPrice = 0;
                decimal multiplierUp = 0;
                decimal multiplierDown = 0;
                decimal min_Notional = 0;
                decimal lot_size = 0;

                JObject i = JObject.Parse(File.ReadAllText("exchange_info.txt"));
                JArray o = JArray.Parse(i["symbols"].ToString());

                foreach (JObject jObject in o)
                {
                    if ((string)jObject["symbol"] == pair)
                    {
                        precision = (int)jObject["baseAssetPrecision"];
                        minPrice = decimal.Parse((string)jObject["filters"][0]["minPrice"], style, provider);
                        multiplierUp = decimal.Parse((string)jObject["filters"][1]["multiplierUp"], style, provider);
                        multiplierDown = decimal.Parse((string)jObject["filters"][1]["multiplierDown"], style, provider);
                        min_Notional = decimal.Parse((string)jObject["filters"][3]["minNotional"], style, provider);
                        lot_size = decimal.Parse((string)jObject["filters"][2]["minQty"], style, provider);
                        break;
                    }
                }

                decimal decimalFormat = getAllZeros(minPrice);
                string format = "{0:" + decimalFormat + "}";
                string newFormat = format.Replace(",", ".");

                decimal sellPrice = solveQuantityCoins(Math.Round(takeProfit * price, precision), precision);
                decimal sellStopPrice = solveQuantityCoins(Math.Round(StopPrice * price, precision), precision);
                decimal sellStopLimitPrice = solveQuantityCoins(Math.Round(StopLossPrice * price, precision), precision);

                string one = String.Format(newFormat, sellPrice).Replace(",", ".");
                string two = String.Format(newFormat, sellStopPrice).Replace(",", ".");
                string three = String.Format(newFormat, sellStopLimitPrice).Replace(",", ".");

                decimal onee = Decimal.Parse(one, style, provider);
                decimal twoo = Decimal.Parse(two, style, provider);
                decimal threee = Decimal.Parse(three, style, provider);

                quantityOfCoins = solveQuantityCoins(quantityOfCoins, precision);

                var sellOrder = client.Spot.Order.PlaceOcoOrder(pair, Binance.Net.Enums.OrderSide.Sell, quantityOfCoins, onee, twoo, threee, null, null, null, null, null, Binance.Net.Enums.TimeInForce.GoodTillCancel);
                
                if (sellOrder.Error != null)
                {
                    Console.WriteLine(sellOrder.Error.Message);

                    if (sellOrder.Error.Message == "Filter failure: PERCENT_PRICE")
                    {
                        decimal WeightedAveragePrice = client.Spot.Market.Get24HPrice(pair).Data.WeightedAveragePrice;
                        Logic.colorOutput(ConsoleColor.Red, $"sellPrice: { onee }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");
                        Logic.colorOutput(ConsoleColor.Red, $"sellStopPrice: { twoo }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");
                        Logic.colorOutput(ConsoleColor.Red, $"sellStopLimitPrice: { threee }, UpperLimit: { WeightedAveragePrice * multiplierUp }, LowerLimit: {WeightedAveragePrice * multiplierDown} ");
                    }
                    Logic.colorOutput(ConsoleColor.Red, $"OCO ORDER FAILED, FIND THE ERROR MESSAGE HERE ^^");
                }

                Logic.colorOutput(ConsoleColor.Green, $"OCO ORDER SUBMITTED, SELL PRICE: {onee} | STOP PRICE: {twoo} | STOP LIMIT PRICE: {threee}");

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static decimal solveQuantityCoins(decimal quantityOfCoins, int precision)
        {
            string start = "0.";
            for (int i = 0; i < precision; i++)
            {
                start = start + "#";
            }

            return decimal.Parse(quantityOfCoins.ToString(start, provider), style, provider);
        }

        static decimal getAllZeros(decimal minPrice)
        {

            int count = minPrice.ToString(provider).Length - (((int)minPrice).ToString(provider).Length);

            while ((char)minPrice.ToString()[count] == '0')
                count = count - 1;

            StringBuilder sb = new StringBuilder(minPrice.ToString());
            sb[count] = '0';
            decimal newDecimal = Decimal.Parse(sb.ToString());

            return Math.Round(newDecimal, count - 1);

        }

        static int getQtyPrecision(decimal minQty)
        {

            int count = minQty.ToString(provider).Length - (((int)minQty).ToString(provider).Length);

            int pos = minQty.ToString(provider).IndexOf('1') - minQty.ToString(provider).IndexOf('.');

            if (pos <= 0)
            {
                return 0;
            }
            else
            {
                return pos;
            }

        }
    }
}
