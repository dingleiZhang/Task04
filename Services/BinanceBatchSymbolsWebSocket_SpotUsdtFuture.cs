using System;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Collections.Concurrent; // 引入ConcurrentDictionary所在的命名空间

using Models;

namespace Services;

public class BinanceBatchSymbolsWebSocket_SpotUsdtFuture
{
    public static ClientWebSocket _spotWebSocket;
    public static ClientWebSocket _futuresWebSocket;
    public static ClientWebSocket _coinMarginedFundingRateWebSocket;

    private static CancellationTokenSource _cts;

    public static HashSet<string> _currentTradingPairs;
    public static HashSet<string> _removedPairs;
    public static HashSet<string> _addedPairs;

    private static ConcurrentDictionary<string, BestBidAsk> _spotBestPrices;
    private static ConcurrentDictionary<string, BestBidAsk> _futureBestPrices;

    public static ConcurrentDictionary<string, decimal> _coinMarginedFundingRates;

    private static bool _spot_flag = false; // 是否收到数据
    private static bool _future_flag = false; // 是否收到数据

    private static long _lastUpdateId = 0; // 最后更新的消息ID， 确保消息顺序正确，避免重复处理

    private static bool _isFirstUpdate = true; // 是否是第一次更新，标记是否是第一次收到数据

    private static int _reconnectAttempts = 0; // 重连尝试次数

    private static DateTime _lastUpdateTime = DateTime.MinValue; // 最后更新时间

    private static DateTime _beginTime;

    public static ConcurrentQueue<string[]> tableData;

    public static CancellationTokenSource _cancellationTokenSource;
    private static bool _fundingRate_flag = false; // 是否收到数据

    // 数据时效性阈值：超过该时间（毫秒）未更新则判定为过期

    public static void Initialize()
    {
        _spotWebSocket = new ClientWebSocket();
        _futuresWebSocket = new ClientWebSocket();
        _currentTradingPairs = new HashSet<string>();
        _removedPairs = new HashSet<string>();
        _addedPairs = new HashSet<string>();
        _spotBestPrices = new ConcurrentDictionary<string, BestBidAsk>();
        _futureBestPrices = new ConcurrentDictionary<string, BestBidAsk>();
        tableData = new ConcurrentQueue<string[]>();
        _beginTime = DateTime.Now;
        _coinMarginedFundingRateWebSocket = new ClientWebSocket();
        _coinMarginedFundingRates = new ConcurrentDictionary<string, decimal>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public static async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        while (!_cts.IsCancellationRequested) // 循环，直到连接成功
        {
            try
            {
                while (_currentTradingPairs.Count < 1)
                {
                    await Task.Delay(5000);
                } 
                _spotBestPrices.Clear();
                _futureBestPrices.Clear();

                // // 生成每个交易对的订阅路径
                var spotSbscriptionPaths = _currentTradingPairs.Select(pair => $"{pair.ToLower()}usdt@depth5@100ms");
                var futureSbscriptionPaths = _currentTradingPairs.Select(pair => $"{pair.ToLower()}usdt@depth5@100ms");

                // 组合成完整路径
                string spotFullPath = string.Join("/", spotSbscriptionPaths);
                string futureFullPath = string.Join("/", futureSbscriptionPaths);

                var spotUri = new Uri($"wss://stream.binance.com:9443/stream?streams={spotFullPath}");
                var futureUri = new Uri($"wss://fstream.binance.com/stream?streams={futureFullPath}");

                Console.WriteLine($"_currentTradingPairs number = {_currentTradingPairs.Count}");
                Console.WriteLine($"_lastUpdateTime: {_lastUpdateTime}");
                Console.WriteLine($"Connecting to Binance WebSocket(spot-usdt)...");

                if (!(_spotWebSocket.State == WebSocketState.Open))
                {
                    await _spotWebSocket.ConnectAsync(spotUri, CancellationToken.None); // 异步连接，CancellationToken.None表示不能取消
                    _ = SpotReceiveMessagesAsync(); // 启动消息接收任务
                }

                if (!(_futuresWebSocket.State == WebSocketState.Open)) // 两个地方可能都启动重连
                {
                    await _futuresWebSocket.ConnectAsync(futureUri, CancellationToken.None);
                    _ = FutureReceiveMessagesAsync();
                }
                Console.WriteLine($"Connected successfully!");
                _reconnectAttempts = 0; // 重置重连计数器

                break; // 连接成功，退出循环
            }
            catch (Exception ex)
            {
                _reconnectAttempts++;
                await Task.Delay(5000); // 等待5秒后重试
            }
        }
    }

    private static async Task SpotReceiveMessagesAsync()
    {
        var buffer = new byte[65536]; // 接收消息的核心方法SS
        while (_spotWebSocket?.State == WebSocketState.Open && !_cts.IsCancellationRequested) // 只要连接开着就循环
        {
            try
            {
                // 等待接收数据
                var spot_result = await _spotWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (spot_result.MessageType == WebSocketMessageType.Text) // 如果是文本消息
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, spot_result.Count);  // 将字节数据转换为字符串
                    ProcessMessage(message, "SPOT"); // 处理消息内容
                }
                else if (spot_result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket closed by server");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                // break;
                await Task.Delay(1000);
            }
        }
        // 连接断开后自动重连
        Console.WriteLine("Connection lost, attempting to reconnect...");
        // await ReconnectAsync();
    }

    private static async Task FutureReceiveMessagesAsync()
    {
        var buffer = new byte[65536]; // 接收消息的核心方法SS
        while (_futuresWebSocket?.State == WebSocketState.Open && !_cts.IsCancellationRequested) // 只要连接开着就循环
        {
            try
            {
                // 等待接收数据
                var future_result = await _futuresWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (future_result.MessageType == WebSocketMessageType.Text) // 如果是文本消息
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, future_result.Count);  // 将字节数据转换为字符串
                    ProcessMessage(message, "FUTURE"); // 处理消息内容
                }
                else if (future_result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket closed by server");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                // break;
                await Task.Delay(1000);
            }
        }

        // 连接断开后自动重连
        Console.WriteLine("Connection lost, attempting to reconnect...");
        // await ReconnectAsync();
    }



    // 处理消息内容
    private static void ProcessMessage(string message, string connectionType)
    {
        try
        {
            var newData = new BestBidAsk
            {
                Bid1Price = 0.00m,    // 新的买1价
                Bid1Quantity = 0.00m,     // 新的买1数量
                Ask1Price = 0.00m,    // 新的卖1价
                Ask1Quantity = 0.00m,     // 新的卖1数量
                ContractSize = 0.00m, // 币本位合约每张的价值
                LastUpdatedTime = DateTime.Now  // 最新更新时间
            };

            if (connectionType == "SPOT")
            {
                var datas = JObject.Parse(message);
                var stream = datas["stream"].ToString();
                var symbol = stream.Substring(0, stream.IndexOf('@')).Replace("usdt", "").ToUpper();
                var data = datas["data"]; // 解析JSON字符串为对象

                newData.Bid1Price = decimal.Parse(data["bids"][0][0].ToString());
                newData.Bid1Quantity = decimal.Parse(data["bids"][0][1].ToString());
                newData.Ask1Price = decimal.Parse(data["asks"][0][0].ToString());
                newData.Ask1Quantity = decimal.Parse(data["asks"][0][1].ToString());
                _spotBestPrices[symbol] = newData;

                _spot_flag = true;
                // 更新最后更新时间
                _lastUpdateTime = DateTime.Now;
            }
            else if (connectionType == "FUTURE")
            {
                var datas = JObject.Parse(message);
                var stream = datas["stream"].ToString();
                var symbol = stream.Substring(0, stream.IndexOf('@')).Replace("usdt", "").ToUpper();
                var data = datas["data"]; // 解析JSON字符串为对象

                newData.Bid1Price = decimal.Parse(data["b"][0][0].ToString());
                newData.Bid1Quantity = decimal.Parse(data["b"][0][1].ToString());
                newData.Ask1Price = decimal.Parse(data["a"][0][0].ToString());
                newData.Ask1Quantity = decimal.Parse(data["a"][0][1].ToString());
                _futureBestPrices[symbol] = newData;

                _future_flag = true;
                // 更新最后更新时间
                _lastUpdateTime = DateTime.Now;
            }
            // 收到数据
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
            Console.WriteLine($"Raw message: {message}");
        }
    }
    public static void PrintOrderBook()
    {
        if (!_spot_flag || !_future_flag) // 都有数据了才开始打印
            return;

        Console.Clear(); // 清空控制台
        Console.WriteLine($"=== BINANCE 现货-U本位合约监控界面 ===");
        Console.WriteLine($"Spot Connection State: {_spotWebSocket?.State}");
        Console.WriteLine($"Future Connection State: {_futuresWebSocket?.State}");
        Console.WriteLine("==============================================");

        // 输出精简后的标题行
        Console.Write(AlignString("币种", 8));          // 1. 币种
        Console.Write(AlignString("资费", 15));        // 2. 现货买1价
        Console.Write(AlignString("资费年化", 15));        // 2. 现货买1价

        Console.Write(AlignString("期挂现吃开", 15));   // 10. 期货挂单现货吃单开仓差价
        Console.Write(AlignString("期挂现吃平", 15));   // 11. 期货挂单现货吃单平仓差价
        Console.Write(AlignString("现挂期吃开", 15));   // 12. 现货挂单期货吃单开仓差价
        Console.Write(AlignString("现挂期吃平", 15));   // 13. 现货挂单期货吃单平仓差价
        Console.Write(AlignString("现吃期吃开", 15));   // 14. 现货吃单期货吃单开仓差价
        Console.Write(AlignString("现吃期吃平", 15));   // 15. 现货吃单期货吃单平仓差价

        Console.Write(AlignString("现货买1价", 15));        // 2. 现货买1价
        Console.Write(AlignString("现货买1量", 15));        // 3. 现货买1价数量
        Console.Write(AlignString("现货卖1价", 15));        // 4. 现货卖1价
        Console.Write(AlignString("现货卖1量", 15));        // 5. 现货卖1价数量
        Console.Write(AlignString("期货买1价", 12));        // 6. 期货买1价
        Console.Write(AlignString("期货买1量", 12));        // 7. 期货买1价数量
        Console.Write(AlignString("期货卖1价", 12));        // 8. 期货卖1价
        Console.Write(AlignString("期货卖1量", 12));        // 9. 期货卖1价数量

        Console.WriteLine();

        // while (tableData.TryTake(out _)) ;
        while (tableData.TryDequeue(out _)) { }
        // 先收集所有数据，然后排序
        var sortedData = _spotBestPrices
            .Select(pair =>
            {
                string symbol = pair.Key;
                decimal coinFundingRate = _coinMarginedFundingRates[symbol] * 100;
                bool spotHasValue = _spotBestPrices.TryGetValue(symbol, out BestBidAsk spotValue);
                bool futureHasValue = _futureBestPrices.TryGetValue(symbol, out BestBidAsk futureValue);

                return new
                {
                    Symbol = symbol,
                    CoinFundingRate = coinFundingRate,
                    SpotHasValue = spotHasValue,
                    FutureHasValue = futureHasValue,
                    SpotValue = spotValue,
                    FutureValue = futureValue
                };
            })
            .Where(item => item.SpotHasValue && item.FutureHasValue)
            .OrderByDescending(item => item.CoinFundingRate)
            .ToList();

        // 然后遍历排序后的数据
        int printCount = 0;
        foreach (var item in sortedData)
        {
            string symbol = item.Symbol;
            decimal coinFundingRate = item.CoinFundingRate;
            decimal coinFundingRateYear = coinFundingRate * 3 * 365;
            BestBidAsk spotValue = item.SpotValue;
            BestBidAsk futureValue = item.FutureValue;

            decimal openPercent1, closePercent1;  // 期货挂单现货吃单
            decimal openPercent2, closePercent2;  // 现货挂单期货吃单
            decimal openPercent3, closePercent3;  // 现货吃单期货吃单

            // (1) 期货挂单现货吃单
            openPercent1 = (futureValue.Ask1Price - spotValue.Ask1Price) / spotValue.Ask1Price * 100;
            closePercent1 = (futureValue.Bid1Price - spotValue.Bid1Price) / spotValue.Bid1Price * 100;

            // (2) 现货挂单期货吃单
            openPercent2 = (futureValue.Bid1Price - spotValue.Bid1Price) / spotValue.Bid1Price * 100;
            closePercent2 = (futureValue.Ask1Price - spotValue.Ask1Price) / spotValue.Ask1Price * 100;

            // (3) 现货吃单期货吃单
            openPercent3 = (futureValue.Bid1Price - spotValue.Ask1Price) / spotValue.Ask1Price * 100;
            closePercent3 = (futureValue.Ask1Price - spotValue.Bid1Price) / spotValue.Bid1Price * 100;

            var originalColor = Console.ForegroundColor;

            if (printCount < 36)
            {
                Console.Write(AlignString(symbol, 8));
                Console.Write(AlignString($"{coinFundingRate}%", 15));
                Console.Write(AlignString($"{coinFundingRateYear}%", 15));

                Console.Write(AlignString($"{openPercent1:F4}%", 15));
                Console.Write(AlignString($"{closePercent1:F4}%", 15));
                Console.Write(AlignString($"{openPercent2:F4}%", 15));
                Console.Write(AlignString($"{closePercent2:F4}%", 15));
                Console.Write(AlignString($"{openPercent3:F4}%", 15));
                Console.Write(AlignString($"{closePercent3:F4}%", 15));

                Console.Write(AlignString(spotValue.Bid1Price.ToString(), 15));
                // Console.Write(AlignString(spotValue.Bid1Quantity.ToString(), 18));
                Console.Write(AlignString($"{spotValue.Bid1Price * spotValue.Bid1Quantity:F2}u", 15));

                Console.Write(AlignString(spotValue.Ask1Price.ToString(), 15));
                // Console.Write(AlignString(spotValue.Ask1Quantity.ToString(), 18));
                Console.Write(AlignString($"{spotValue.Ask1Price * spotValue.Ask1Quantity:F2}u", 15));

                Console.Write(AlignString(futureValue.Bid1Price.ToString(), 12));
                Console.Write(AlignString(futureValue.Bid1Quantity.ToString(), 10)); // u本位合约的盘口价值直接是对应的数量，因为单位是u

                Console.Write(AlignString(futureValue.Ask1Price.ToString(), 12));
                Console.Write(AlignString(futureValue.Ask1Quantity.ToString(), 10)); // u本位合约的盘口价值直接是对应的数量，因为单位是u

                Console.WriteLine();
                printCount = printCount + 1;
            }

            // 创建行数据
            string[] row =
            {
        symbol,
        coinFundingRate.ToString() + "%",
        coinFundingRateYear.ToString() + "%",
        $"{openPercent1:F4}%",
        $"{closePercent1:F4}%",
        $"{openPercent2:F4}%",
        $"{closePercent2:F4}%",
        $"{openPercent3:F4}%",
        $"{closePercent3:F4}%",
        spotValue.Bid1Price.ToString(),
                // spotValue.Bid1Quantity.ToString(),
        $"{spotValue.Bid1Price * spotValue.Bid1Quantity:F2}u",
        spotValue.Ask1Price.ToString(),
        // spotValue.Ask1Quantity.ToString(),
        $"{spotValue.Ask1Price * spotValue.Ask1Quantity:F2}u",
        futureValue.Bid1Price.ToString(),
                // futureValue.Bid1Quantity.ToString(),
        $"{futureValue.Bid1Price * futureValue.Bid1Quantity:F2}u",
        futureValue.Ask1Price.ToString(),
                // futureValue.Ask1Quantity.ToString()
        $"{futureValue.Ask1Price * futureValue.Ask1Quantity:F2}u"
    };
            // tableData.Add(row);
            tableData.Enqueue(row);
        }

        Console.WriteLine($"还有{_spotBestPrices.Count - printCount}个未打印...");
        Console.WriteLine($"\nStart Time: {_beginTime:yyyy-MM-dd HH:mm:ss.fff}");
        // Console.WriteLine($"\nLast Updated: {DateTime.Now:HH:mm:ss.fff}");
        Console.WriteLine($"\nLast Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        Console.WriteLine($"datas lastUpdateTime: {_lastUpdateTime}");
        Console.WriteLine($"=== BINANCE 现货-U本位合约监控界面 ===");
        Console.WriteLine("按 'w' 键切换显示模式");
        Console.WriteLine("按 'q' 键退出程序");
    }
    // 计算字符串的实际宽度（中文字符算2个宽度，英文字符算1个）
    public static int GetStringWidth(string str)
    {
        int width = 0;
        foreach (char c in str)
        {
            // 判断是否为中文字符（大致范围）
            if (c >= 0x4E00 && c <= 0x9FA5)
            {
                width += 2;
            }
            else
            {
                width += 1;
            }
        }
        return width;
    }

    // 自定义对齐方法，确保总宽度一致
    public static string AlignString(string str, int totalWidth)
    {
        int currentWidth = GetStringWidth(str);
        if (currentWidth >= totalWidth)
        {
            return str;
        }
        // 计算需要补充的空格数
        int spacesToAdd = totalWidth - currentWidth;
        return str + new string(' ', spacesToAdd);
    }

    private static readonly object _reconnectLock = new object();
    public static async Task ReconnectAsync()
    {
        lock (_reconnectLock)
        {
            _cts.Cancel();
            // _cts.Dispose();
            // 重置状态标志
            _spot_flag = false;
            _future_flag = false;
            DisconnectAsync();
            _spotWebSocket = new ClientWebSocket();
            _futuresWebSocket = new ClientWebSocket();
            Thread.Sleep(5000);
            ConnectAsync();
        }
    }
    public static async Task StartConnectionMonitor()
    {
        // 检查WebSocket连接状态
        if (_spotWebSocket?.State != WebSocketState.Open ||
            _futuresWebSocket?.State != WebSocketState.Open)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Connection lost, attempting to reconnect...");
            await ReconnectAsync();
        }
        if (_coinMarginedFundingRateWebSocket?.State != WebSocketState.Open)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Connection lost, attempting to reconnect...");
            await CoinMarginedFundingRatesReconnectAsync();
        }

        // 检查数据是否过期（超过15秒没有更新）
        if (_lastUpdateTime != DateTime.MinValue &&
            (DateTime.Now - _lastUpdateTime).TotalSeconds > 15)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Data stale (last update: {_lastUpdateTime:HH:mm:ss}), reconnecting...");
            await ReconnectAsync();
        }
        else
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Connection healthy (last update: {_lastUpdateTime:HH:mm:ss})");
        }
    }

    public static async Task DisconnectAsync()
    {
        if (_spotWebSocket != null) // 1. 检查WebSocket对象是否存在，确保WebSocket对象已经被创建，避免空指针异常
        {
            try
            {
                if (_spotWebSocket.State == WebSocketState.Open) // 2. 检查连接是否还开着
                {
                    await _spotWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); // 3. 正常关闭连接
                }
                _spotWebSocket.Dispose(); // 4. 释放资源
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnect: {ex.Message}"); // 5. 错误处理
            }
        }
        if (_futuresWebSocket != null) // 1. 检查WebSocket对象是否存在，确保WebSocket对象已经被创建，避免空指针异常
        {
            try
            {
                if (_futuresWebSocket.State == WebSocketState.Open) // 2. 检查连接是否还开着
                {
                    await _futuresWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); // 3. 正常关闭连接
                }
                _futuresWebSocket.Dispose(); // 4. 释放资源
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnect: {ex.Message}"); // 5. 错误处理
            }
        }
    }

    // 订阅币本位资金费率的函数
    public static async Task ConnectAsync_SubscribeToCoinMarginedFundingRates()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        while (!_cancellationTokenSource.IsCancellationRequested) // 循环，直到连接成功
        {
            try
            {
                while (_currentTradingPairs.Count < 1) // 需要修改
                {
                    await Task.Delay(5000);
                }
                // // 生成每个交易对的订阅路径
                var subscriptionPaths = _currentTradingPairs.Select(pair => $"{pair.ToLower()}usdt@markPrice@1s");
                // 组合成完整路径
                string fundingRates = string.Join("/", subscriptionPaths);
                var fundingRatesUri = new Uri($"wss://fstream.binance.com/stream?streams={fundingRates}");


                if (!(_coinMarginedFundingRateWebSocket.State == WebSocketState.Open))
                {
                    await _coinMarginedFundingRateWebSocket.ConnectAsync(fundingRatesUri, CancellationToken.None); // 异步连接，CancellationToken.None表示不能取消
                    _ = CoinMarginedFundingRatesReceiveMessagesAsync(); // 启动消息接收任务
                }

                break; // 连接成功，退出循环
            }
            catch (Exception ex)
            {
                Console.WriteLine("Retrying in 5 seconds...");
                await Task.Delay(5000); // 等待5秒后重试
            }
        }
    }

    private static async Task CoinMarginedFundingRatesReceiveMessagesAsync()
    {
        var buffer = new byte[65536]; // 接收消息的核心方法SS
        while (_coinMarginedFundingRateWebSocket?.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested) // 只要连接开着就循环
        {
            try
            {
                // 等待接收数据
                var coinMarginedFundingRate_result = await _coinMarginedFundingRateWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (coinMarginedFundingRate_result.MessageType == WebSocketMessageType.Text) // 如果是文本消息
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, coinMarginedFundingRate_result.Count);  // 将字节数据转换为字符串
                    CoinMarginedFundingRatesProcessMessage(message); // 处理消息内容
                }
                else if (coinMarginedFundingRate_result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket closed by server");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                // break;
                await Task.Delay(1000);
            }
        }
        // 连接断开后自动重连
        Console.WriteLine("Connection lost, attempting to reconnect...");
        // await CoinMarginedFundingRatesReconnectAsync();
    }

    private static void CoinMarginedFundingRatesProcessMessage(string message)
    {
        try
        {
            var datas = JObject.Parse(message);
            var stream = datas["stream"].ToString();
            var symbol = stream.Substring(0, stream.IndexOf('@')).Replace("usdt", "").ToUpper();
            decimal fundingRate = datas["data"]["r"].ToObject<decimal>();

            _coinMarginedFundingRates.AddOrUpdate(symbol, fundingRate, (key, oldValue) => fundingRate);

            _fundingRate_flag = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
            Console.WriteLine($"Raw message: {message}");
        }
    }

    public static async Task CoinMarginedFundingRatesReconnectAsync()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _fundingRate_flag = false;
        CoinMarginedFundingRatesDisconnectAsync();
        _coinMarginedFundingRateWebSocket = new ClientWebSocket();
        ConnectAsync_SubscribeToCoinMarginedFundingRates();
    }
    public static async Task CoinMarginedFundingRatesDisconnectAsync()
    {
        if (_coinMarginedFundingRateWebSocket != null) // 1. 检查WebSocket对象是否存在，确保WebSocket对象已经被创建，避免空指针异常
        {
            try
            {
                if (_coinMarginedFundingRateWebSocket.State == WebSocketState.Open) // 2. 检查连接是否还开着
                {
                    await _coinMarginedFundingRateWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); // 3. 正常关闭连接
                }
                _coinMarginedFundingRateWebSocket.Dispose(); // 4. 释放资源
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disconnect: {ex.Message}"); // 5. 错误处理
            }
        }
    }
}