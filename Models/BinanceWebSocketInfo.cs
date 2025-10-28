
namespace Models;

public class BestBidAsk
{
    public decimal Bid1Price { get; set; } // 买1价格
    public decimal Bid1Quantity { get; set; } // 买1数量（可选存储）
    public decimal Ask1Price { get; set; } // 卖1价格
    public decimal Ask1Quantity { get; set; } // 卖1数量（可选存储）
    public decimal ContractSize { get; set; } // 币本位合约每张合约的价值
    public DateTime LastUpdatedTime { get; set; } // 最后更新时间（用于判断数据新鲜度）
    
}