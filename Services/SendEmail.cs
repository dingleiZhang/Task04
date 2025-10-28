using System;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;

namespace Services;

public class EmailService
{

    public static SmtpClient smtpClient;
    private static NetworkCredential credentials;
    private static MailAddress fromAddress;
    private static List<string> receivers;

    private static List<String> _tableName = new List<string> { "现货-币本位合约", "现货-U本位合约" };

    public static void Initialize()
    {
        var config = ConfigManager.Instance.Settings;
        Console.WriteLine("开始配置客户端...");
        // 初始化SMTP服务器配置
        smtpClient = new SmtpClient(config.EmailSettings.SmtpServer)
        {
            Port = config.EmailSettings.SmtpPort,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        // 初始化认证信息
        credentials = new NetworkCredential(
            config.EmailSettings.SenderEmail,
            config.EmailSettings.SenderPassword
        );
        smtpClient.Credentials = credentials;

        // 初始化发件人地址
        fromAddress = new MailAddress(config.EmailSettings.SenderEmail);

        // 初始化收件人列表
        receivers = config.EmailSettings.ReceiverEmails;

        Console.WriteLine("初始化成功...");

    }


    // 原始方法保持不变，用于向后兼容
    public static void SendEmail()
    {
        var tableData = new List<string[]>();
        if (BinanceBatchSymbolsWebSocket._currentTradingPairs.Count == BinanceBatchSymbolsWebSocket.tableData.Count)
        {
            tableData.Clear(); // 清空现有元素（如果有）
            tableData.AddRange(BinanceBatchSymbolsWebSocket.tableData); // 添加 tableData 中的所有元素

        }
        else
        {
            return;
        }
        // 表格标题
        string[] headers =
                        {
                            "币种",
                            "资金费率",
                            "资金费率年化",
                            "期挂现吃开仓差价",
                            "期挂现吃平仓差价",
                            "现挂期吃开仓差价",
                            "现挂期吃平仓差价",
                            "现吃期吃开仓差价",
                            "现吃期吃平仓差价",
                            "现货买1价",
                            "现货买1量",
                            "现货卖1价",
                            "现货卖1量",
                            "期货买1价",
                            "期货买1量",
                            "期货卖1价",
                            "期货卖1量"
                        };

        SendEmailMessage("可交易币种详情。", tableData, headers);
    }
    public static void SendEmailMessage(string subject, List<string[]> tableData, string[] headers = null)
    {
        // 每个元素包含多列的情况
        Console.WriteLine("开始创建邮件");

        // 创建表格形式的HTML内容
        string body = CreateHtmlTable(tableData, headers);

        // 创建邮件消息
        var mailMessage = new MailMessage
        {
            From = fromAddress,
            Subject = subject,
            Body = body,
            IsBodyHtml = true  // 这个属性必须设置为true，否则会显示原始HTML代码
        };

        Console.WriteLine("开始添加收件人");
        // 添加收件人
        foreach (var email in receivers)
        {
            mailMessage.To.Add(email);
        }
        Console.WriteLine("开始发送邮件");
        // // // 发送邮件
        smtpClient.Send(mailMessage);
        try
        {
            Console.WriteLine("邮件发送成功！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"邮件发送失败: {ex.Message}");
        }
    }


    // 创建HTML表格的方法
    private static string CreateHtmlTable(List<string[]> tableData, string[] headers = null)
    {
        var html = new System.Text.StringBuilder();

        // 表格样式，使表格更美观
        html.AppendLine("<style>");
        html.AppendLine("table { border-collapse: collapse; width: 100%; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine("</style>");

        // 开始表格
        html.AppendLine("<table>");

        // 添加表头（如果有）
        if (headers != null && headers.Length > 0)
        {
            html.AppendLine("<tr>");
            foreach (var header in headers)
            {
                html.AppendLine($"<th>{header}</th>");
            }
            html.AppendLine("</tr>");
        }

        // 添加表格内容
        foreach (var row in tableData)
        {
            html.AppendLine("<tr>");
            foreach (var cell in row)
            {
                html.AppendLine($"<td>{cell}</td>");
            }
            html.AppendLine("</tr>");
        }

        // 结束表格
        html.AppendLine("</table>");

        return html.ToString();
    }


    public static void Dispose()
    {
        if (smtpClient != null)
        {
            smtpClient.Dispose();
            smtpClient = null;
        }
        Console.WriteLine("邮件服务资源已释放");
    }

    // 一次性发送多个邮件
    public static void TwoTable_SendEmail()
    {
        var tableData1 = new List<string[]>();
        var tableData2 = new List<string[]>();

        // 获取第一个表格数据
        if (BinanceBatchSymbolsWebSocket._currentTradingPairs.Count == BinanceBatchSymbolsWebSocket.tableData.Count)
        {
            tableData1.Clear();
            tableData1.AddRange(BinanceBatchSymbolsWebSocket.tableData);
        }
        else
        {
            return;
        }

        // 获取第二个表格数据
        if (BinanceBatchSymbolsWebSocket_SpotUsdtFuture._currentTradingPairs.Count == BinanceBatchSymbolsWebSocket_SpotUsdtFuture.tableData.Count)
        {
            tableData2.Clear();
            tableData2.AddRange(BinanceBatchSymbolsWebSocket_SpotUsdtFuture.tableData);
        }
        else
        {
            return;
        }

        // 表格标题（两个表格使用相同的表头）
        string[] headers =
        {
        "币种",
        "资金费率",
        "资金费率年化",
        "期挂现吃开仓差价",
        "期挂现吃平仓差价",
        "现挂期吃开仓差价",
        "现挂期吃平仓差价",
        "现吃期吃开仓差价",
        "现吃期吃平仓差价",
        "现货买1价",
        "现货买1量",
        "现货卖1价",
        "现货卖1量",
        "期货买1价",
        "期货买1量",
        "期货卖1价",
        "期货卖1量"
    };

        // 创建包含两个表格的邮件内容
        TwoTable_SendEmailMessage("可交易币种详情。", new List<List<string[]>> { tableData1, tableData2 }, headers);
    }

    // 修改SendEmailMessage方法，支持多个表格
    public static void TwoTable_SendEmailMessage(string subject, List<List<string[]>> tablesData, string[] headers = null)
    {
        Console.WriteLine("开始创建邮件");

        // 创建包含多个表格的HTML内容
        string body = TwoTable_CreateHtmlTables(tablesData, headers);

        // 创建邮件消息
        var mailMessage = new MailMessage
        {
            From = fromAddress,
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        Console.WriteLine("开始添加收件人");
        // 添加收件人
        foreach (var email in receivers)
        {
            mailMessage.To.Add(email);
        }

        Console.WriteLine("开始发送邮件");
        try
        {
            // 发送邮件
            smtpClient.Send(mailMessage);
            Console.WriteLine("邮件发送成功！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"邮件发送失败: {ex.Message}");
        }
    }

    // 新增方法：创建包含多个表格的HTML
    private static string TwoTable_CreateHtmlTables(List<List<string[]>> tablesData, string[] headers = null)
    {
        var html = new System.Text.StringBuilder();

        // 表格样式
        html.AppendLine("<style>");
        html.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine("h2 { margin-top: 30px; margin-bottom: 10px; }");
        html.AppendLine("</style>");

        // 为每个表格创建HTML
        for (int i = 0; i < tablesData.Count; i++)
        {
            // 添加表格标题
            // html.AppendLine($"<h2>表格 {i + 1}</h2>");
            html.AppendLine($"<h2>{_tableName[i]}</h2>");

            // 开始表格
            html.AppendLine("<table>");

            // 添加表头（如果有）
            if (headers != null && headers.Length > 0)
            {
                html.AppendLine("<tr>");
                foreach (var header in headers)
                {
                    html.AppendLine($"<th>{header}</th>");
                }
                html.AppendLine("</tr>");
            }

            // 添加表格内容
            foreach (var row in tablesData[i])
            {
                html.AppendLine("<tr>");
                foreach (var cell in row)
                {
                    html.AppendLine($"<td>{cell}</td>");
                }
                html.AppendLine("</tr>");
            }

            // 结束表格
            html.AppendLine("</table>");
        }

        return html.ToString();
    }

}
