using System.Collections.Generic;

namespace Models;

public class Config
{
    public EmailSettings? EmailSettings { get; set; }
    public int ConsoleRefreshFrequencySeconds { get; set; }
    public int RestUpdateFrequencySeconds { get; set; }
    public string? BinanceApiUrl { get; set; }
    public string? BinanceCoinMarginedApiUrl { get; set; }
    public string? BinanceUsdtMarginedApiUrl { get; set; }
}

public class EmailSettings
{
    public string? SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public string? SenderEmail { get; set; }
    public string? SenderPassword { get; set; }
    public List<string>? ReceiverEmails { get; set; }
    public int EmailFrequencyMinutes { get; set; }
}

