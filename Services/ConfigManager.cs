using System.IO;
using System.Text.Json;
using Models;

namespace Services;
public sealed class ConfigManager
{
    private static readonly object _lock = new object();
    private static ConfigManager? _instance;
    public Config? Settings { get; private set; }


    private ConfigManager()
    {
        LoadConfig();
    }

    public static ConfigManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                        _instance = new ConfigManager();
                }
            }
            return _instance;
        }
    }

    private void LoadConfig()
    {
        string json = File.ReadAllText("Configs/config.json"); // 你的 JSON 文件路径
        Settings = JsonSerializer.Deserialize<Config>(json);
        Console.WriteLine("配置已加载");
    }

    // 如果需要随时刷新配置
    public void Reload()
    {
        LoadConfig();
    }
}
