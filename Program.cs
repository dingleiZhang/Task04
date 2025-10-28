using System;
using Models;
using Services;

class Program
{
    public static CancellationTokenSource _cts = new CancellationTokenSource();
    private static Config _config;

    // 当前显示模式，初始化为PrintThread模式
    private static DisplayMode _currentDisplayMode = DisplayMode.PrintThread;

    // 线程锁对象，用于确保多线程环境下对共享资源的访问安全
    private static readonly object _displayLock = new object();

    // 定义显示模式的枚举
    public enum DisplayMode
    {
        PrintThread,           // 模式1：显示现货-币本位打印内容
        SpotUsdtPrintThread    // 模式2：显示现货-USDT打印内容
    }
    static void Main(string[] args)
    {
        // Initialize
        Initialize();

        // 获得 现货-u本位 可交易状态列表
        var tradingPairsThread = new Thread(TradingPairsThread);
        tradingPairsThread.IsBackground = true;
        tradingPairsThread.Start();

        // 获得 现货-u本位 可交易状态列表
        var tradingPairsThread_SpotUsdtFuture = new Thread(TradingPairsThread_SpotUsdtFuture);
        tradingPairsThread_SpotUsdtFuture.IsBackground = true;
        tradingPairsThread_SpotUsdtFuture.Start();

        BinanceBatchSymbolsWebSocket.ConnectAsync();
        BinanceBatchSymbolsWebSocket.ConnectAsync_SubscribeToCoinMarginedFundingRates();

        BinanceBatchSymbolsWebSocket_SpotUsdtFuture.ConnectAsync();
        BinanceBatchSymbolsWebSocket_SpotUsdtFuture.ConnectAsync_SubscribeToCoinMarginedFundingRates();

        // 启动打印线程
        var printThread = new Thread(PrintThread);
        printThread.IsBackground = true;
        printThread.Name = "PrintThread"; // 给线程命名，便于调试
        printThread.Start();

        // // 启动spot-usdt打印线程
        var spotUsdtPrintThread = new Thread(SpotUsdtPrintThread);
        spotUsdtPrintThread.IsBackground = true;
        spotUsdtPrintThread.Name = "SpotUsdtPrintThread";
        spotUsdtPrintThread.Start();

        // 启动键盘监听线程 - 专门监听用户按键输入
        var keyListenerThread = new Thread(KeyListener);
        keyListenerThread.IsBackground = true;
        keyListenerThread.Name = "KeyListener";
        keyListenerThread.Start();

        // // 启动风控监控
        var monitorRiskThread = new Thread(MonitorRiskThread);
        monitorRiskThread.IsBackground = true;
        monitorRiskThread.Start();


        // 启动邮件线程
        var sendEmailThread = new Thread(SendEmailThread);
        sendEmailThread.IsBackground = true;
        sendEmailThread.Start();

        // 显示初始界面信息
        // 显示初始界面
        Console.Clear();
        Console.WriteLine("=== 币种行情监控系统 ===");
        Console.WriteLine($"当前显示模式: {_currentDisplayMode}");
        Console.WriteLine("按 'w' 键切换显示模式");
        Console.WriteLine("按 'q' 键退出程序");
        Console.WriteLine("=======================");

        // 主线程等待退出信号
        WaitForExit();

        _cts.Cancel();
        Console.WriteLine("程序正在退出...");
        Thread.Sleep(1000); // 给线程一点时间优雅退出
    }

    /// <summary>
    /// 等待退出信号，而不是立即退出
    /// </summary>
    private static void WaitForExit()
    {
        while (!_cts.IsCancellationRequested)
        {
            // 改用简单的循环等待，由KeyListener线程处理退出逻辑
            Thread.Sleep(100);
        }
    }

    public static void Initialize()
    {
        // 配置类
        _config = ConfigManager.Instance.Settings;
        // 初始化spot-币本位httpClient
        BinanceTradingPairManager.InitializeHttpClient();
        // 初始化spot-U本位httpClient
        BinanceTradingPairManager_SpotUsdtFuture.InitializeHttpClient();
        // 初始化spot-币本文webSocket
        BinanceBatchSymbolsWebSocket.Initialize();
        // 初始化spot-币本文webSocket
        BinanceBatchSymbolsWebSocket_SpotUsdtFuture.Initialize();

        // 初始化邮件配置信息
        EmailService.Initialize();

        // 初始化 spot-币本位 交易币种
        BinanceTradingPairManager.InitialCurrentTradingPairs();

        // 初始化 spot-u本位 交易币种
        BinanceTradingPairManager_SpotUsdtFuture.InitialCurrentTradingPairs();
    }

    private static async void TradingPairsThread()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.RestUpdateFrequencySeconds * 1000, _cts.Token); // 每10秒检查一次
                Console.WriteLine("spot-币本位合约 交易对获取线程启动...");
                await BinanceTradingPairManager.UpdateTradingPairsAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection check error: {ex.Message}");
            }
        }
    }

    private static async void TradingPairsThread_SpotUsdtFuture()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.RestUpdateFrequencySeconds * 1000, _cts.Token); // 每10秒检查一次
                Console.WriteLine("spot-u本位合约 交易对获取线程启动...");
                await BinanceTradingPairManager_SpotUsdtFuture.UpdateTradingPairsAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine("rrr error");
                Console.WriteLine($"Connection check error: {ex.Message}");
            }
        }
    }

    // <summary>
    /// 键盘监听线程方法
    /// 专门负责检测用户按键并切换显示模式
    /// </summary>
    private static void KeyListener()
    {
        Console.WriteLine("键盘监听线程启动");

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)  // 检查是否有按键可用
                {
                    var key = Console.ReadKey(true);  // true表示不显示按下的键

                    // 只响应'w'键（大小写都支持）
                    if (key.KeyChar == 'w' || key.KeyChar == 'W')
                    {
                        // 使用lock确保线程安全地修改显示模式
                        lock (_displayLock)
                        {
                            // 切换显示模式
                            if (_currentDisplayMode == DisplayMode.PrintThread)
                            {
                                _currentDisplayMode = DisplayMode.SpotUsdtPrintThread;
                            }
                            else
                            {
                                _currentDisplayMode = DisplayMode.PrintThread;
                            }

                            // 清空控制台并显示新模式信息
                            Console.Clear();
                            Console.WriteLine("=== 币种行情监控系统 ===");
                            Console.WriteLine("按 'w' 键切换显示模式");
                            Console.WriteLine("按 'q' 键退出程序");
                            Console.WriteLine("=======================");
                        }
                    }
                    else if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        // 按下q键时退出程序
                        Console.WriteLine("\n收到退出指令，正在停止程序...");
                        _cts.Cancel();
                        break;
                    }
                }

                // 休眠100毫秒，减少CPU占用
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"键盘监听错误: {ex.Message}");
            }
        }

        Console.WriteLine("键盘监听线程退出");
    }

    private static async void PrintThread()
    {
        Console.WriteLine("现货-币本位 打印输出线程启动");
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.ConsoleRefreshFrequencySeconds * 1000, _cts.Token);
                // BinanceBatchSymbolsWebSocket.PrintOrderBook();
                // 使用锁确保安全地读取当前显示模式
                lock (_displayLock)
                {
                    // 关键：只有在当前模式是PrintThread时才执行打印
                    if (_currentDisplayMode == DisplayMode.PrintThread)
                    {
                        // 执行实际的打印操作
                        BinanceBatchSymbolsWebSocket.PrintOrderBook();
                    }
                    // 如果模式不匹配，就跳过打印，什么都不输出
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection check error: {ex.Message}");
            }
        }
        Console.WriteLine("现货-币本位打印线程退出");
    }

    private static async void SpotUsdtPrintThread()
    {
        Console.WriteLine("现货-本位打印输出线程启动");
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_config.ConsoleRefreshFrequencySeconds * 1000, _cts.Token);
                // BinanceBatchSymbolsWebSocket_SpotUsdtFuture.PrintOrderBook();
                // 使用锁确保安全地读取当前显示模式
                lock (_displayLock)
                {
                    // 关键：只有在当前模式是SpotUsdtPrintThread时才执行打印
                    if (_currentDisplayMode == DisplayMode.SpotUsdtPrintThread)
                    {
                        // 执行实际的打印操作
                        BinanceBatchSymbolsWebSocket_SpotUsdtFuture.PrintOrderBook();
                    }
                    // 如果模式不匹配，就跳过打印，什么都不输出
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection check error: {ex.Message}");
            }
        }
        Console.WriteLine("现货-USDT打印线程退出");
    }

    private static async void MonitorRiskThread()
    {
        Console.WriteLine("监控线程启动");
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000 * 60, _cts.Token);
                BinanceBatchSymbolsWebSocket.StartConnectionMonitor();
                BinanceBatchSymbolsWebSocket_SpotUsdtFuture.StartConnectionMonitor();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection check error: {ex.Message}");
            }
        }
    }

    private static async void SendEmailThread()
    {
        Console.WriteLine("发送邮件线程启动");
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (BinanceTradingPairManager._getPairFlag)
                {
                    Console.WriteLine("开始发送邮件");
                    // EmailService.SendEmail(); // 发送一个表格的数据
                    EmailService.TwoTable_SendEmail(); // 发送两个表格的数据
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Display error: {ex.Message}");
            }
            await Task.Delay(_config.EmailSettings.EmailFrequencyMinutes * 1000, _cts.Token);
        }
        // 释放资源
        EmailService.Dispose();
    }
}