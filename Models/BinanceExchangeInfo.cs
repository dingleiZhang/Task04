using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Models;

public class BinanceSymbol
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("isSpotTradingAllowed")]
    public bool IsSpotTradingAllowed { get; set; }

    [JsonProperty("baseAsset")]
    public string BaseAsset { get; set; }

    [JsonProperty("quoteAsset")]
    public string QuoteAsset { get; set; }
}

public class BinanceExchangeInfo
{
    [JsonProperty("symbols")]
    public List<BinanceSymbol> Symbols { get; set; }
}

public class BinanceCoinMarginedExchangeInfo
{
    [JsonProperty("symbols")]
    public List<BinanceCoinMarginedSymbol> Symbols { get; set; }
}
public class BinanceCoinMarginedSymbol
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("pair")]
    public string Pair { get; set; }

    [JsonProperty("contractStatus")]
    public string Status { get; set; }
    
    [JsonProperty("contractSize")]
    public string ContractSize { get; set; }

    [JsonProperty("contractType")]
    public string ContractType { get; set; }

    [JsonProperty("baseAsset")]
    public string BaseAsset { get; set; }

    [JsonProperty("quoteAsset")]
    public string QuoteAsset { get; set; }
}

public class BinanceCoinMarginedExchangeInfo_SpotUsdtFuture
{
    [JsonProperty("symbols")]
    public List<BinanceCoinMarginedSymbol_SpotUsdtFuture> Symbols { get; set; }
}
public class BinanceCoinMarginedSymbol_SpotUsdtFuture
{
    [JsonProperty("symbol")]
    public string Symbol { get; set; }

    [JsonProperty("pair")]
    public string Pair { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("contractType")]
    public string ContractType { get; set; }

    [JsonProperty("baseAsset")]
    public string BaseAsset { get; set; }

    [JsonProperty("quoteAsset")]
    public string QuoteAsset { get; set; }
}

