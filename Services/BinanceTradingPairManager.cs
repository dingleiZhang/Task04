using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using Models;
using System.Threading.Tasks;
namespace Services;

public class BinanceTradingPairManager
{
    private static HttpClient _httpClient;
    private static object _lockObject = new object();

    public static bool _getPairFlag = false;
    public static void InitializeHttpClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "BinanceTradingSystem/1.0");
        Console.WriteLine("HTTP客户端初始化成功");
    }

    public static async Task InitialCurrentTradingPairs()
    {
        while (BinanceBatchSymbolsWebSocket._currentTradingPairs.Count == 0)
        {
            await UpdateTradingPairsAsync();
        }
    }

    public static async Task UpdateTradingPairsAsync()
    {

        try
        {
            // 并行获取现货和币本位合约交易对
            var spotTask = GetSpotTradingPairsAsync();
            var coinMarginedTask = GetCoinMarginedTradingPairsAsync();


            await Task.WhenAll(spotTask, coinMarginedTask);

            var spotTradingPairs = await spotTask;
            var coinMarginedTradingPairs = await coinMarginedTask;





            var allPairs = new HashSet<string>(spotTradingPairs);
            allPairs.IntersectWith(coinMarginedTradingPairs);

            if (allPairs.Count > 0)
            {
                // 比较新旧列表并输出结果
                CompareAndPrintTradingPairs(allPairs);
                _getPairFlag = true;

            }

            // 打印最新的前10个交易对
            // PrintTop10TradingPairs(BinanceBatchSymbolsWebSocket._currentTradingPairs);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"更新交易对失败: {ex.Message}");
        }
    }

    private static async Task<HashSet<string>> GetSpotTradingPairsAsync()
    {
        var tradingPairs = new HashSet<string>();

        try
        {
            var url = $"https://api.binance.com/api/v3/exchangeInfo";
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            var response = await _httpClient.GetStringAsync(url);
            var exchangeInfo = JsonConvert.DeserializeObject<BinanceExchangeInfo>(response);

            Console.WriteLine("开始查找现货");
            if (exchangeInfo?.Symbols != null)
            {
                foreach (var symbol in exchangeInfo.Symbols)
                {
                    // 只选择状态为TRADING且允许现货交易的交易对
                    if (symbol.Status == "TRADING" && symbol.IsSpotTradingAllowed && symbol.QuoteAsset == "USDT")
                    {
                        tradingPairs.Add(symbol.BaseAsset);
                        //Console.WriteLine(symbol.Symbol);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Console.WriteLine($"获取现货交易对失败: {ex.Message}");
        }

        return tradingPairs;
    }

    private static async Task<HashSet<string>> GetCoinMarginedTradingPairsAsync()
    {
        var tradingPairs = new HashSet<string>();

        try
        {
            var url = $"https://dapi.binance.com/dapi/v1/exchangeInfo";
            var response = await _httpClient.GetStringAsync(url);
            var exchangeInfo = JsonConvert.DeserializeObject<BinanceCoinMarginedExchangeInfo>(response);

            if (exchangeInfo?.Symbols != null)
            {
                foreach (var symbol in exchangeInfo.Symbols)
                {
                    // 只选择状态为TRADING的币本位永续合约
                    if (symbol.Status == "TRADING" && symbol.ContractType == "PERPETUAL")
                    {
                        tradingPairs.Add(symbol.BaseAsset);
                        //Console.WriteLine(symbol.Pair);
                        var symbolName = symbol.Symbol.Replace("USD_PERP", "").ToUpper();
                        var contractSize = symbol.ContractSize;
                        BinanceBatchSymbolsWebSocket._contractSize[symbolName] = decimal.Parse(contractSize) ;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取币本位合约交易对失败: {ex.Message}");
        }

        return tradingPairs;
    }

    private static void CompareAndPrintTradingPairs(HashSet<string> newPairs)
    {
        lock (_lockObject)
        {
            if (BinanceBatchSymbolsWebSocket._currentTradingPairs.Count == 0)
            {
                BinanceBatchSymbolsWebSocket._currentTradingPairs = newPairs;
                Console.WriteLine("初始交易对列表已加载");
                return;
            }

            BinanceBatchSymbolsWebSocket._removedPairs = new HashSet<string>(BinanceBatchSymbolsWebSocket._currentTradingPairs);
            BinanceBatchSymbolsWebSocket._removedPairs.ExceptWith(newPairs);

            BinanceBatchSymbolsWebSocket._addedPairs = new HashSet<string>(newPairs);
            BinanceBatchSymbolsWebSocket._addedPairs.ExceptWith(BinanceBatchSymbolsWebSocket._currentTradingPairs);

            // 更新当前交易对列表
            BinanceBatchSymbolsWebSocket._currentTradingPairs = newPairs;
            if (BinanceBatchSymbolsWebSocket._removedPairs.Count > 0 || BinanceBatchSymbolsWebSocket._addedPairs.Count > 0)
            {
                BinanceBatchSymbolsWebSocket.ReconnectAsync(); // 交易队变化，则断开重新订阅
            }
        }
    }

    private static void PrintTop10TradingPairs(HashSet<string> tradingPairs)
    {
        if (tradingPairs.Count == 0)
        {
            // Console.WriteLine("当前没有可交易对");
            return;
        }
        int printNumber = tradingPairs.Count;

        var sortedPairs = tradingPairs.OrderBy(p => p).ToList();
        var top10 = sortedPairs.Take(printNumber).ToList();

        Console.WriteLine($"最新交易对列表前{printNumber}个:");
        for (int i = 0; i < top10.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {top10[i]}");
        }

        if (tradingPairs.Count > printNumber)
        {
            Console.WriteLine($"... 还有 {tradingPairs.Count - printNumber} 个交易对");
        }
        Console.WriteLine("===================");
    }
}