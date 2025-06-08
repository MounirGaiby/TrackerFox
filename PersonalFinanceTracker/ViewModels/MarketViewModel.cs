using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.ViewModels
{
    public class MarketDataViewModel
    {
        public List<StockMarketIndex> StockMarketIndices { get; set; } = new List<StockMarketIndex>();
        public List<Cryptocurrency> Cryptocurrencies { get; set; } = new List<Cryptocurrency>();
        public List<ForexRate> ForexRates { get; set; } = new List<ForexRate>();
        public List<Commodity> Commodities { get; set; } = new List<Commodity>();
        public List<WeatherInfo> WeatherData { get; set; } = new List<WeatherInfo>();
        public List<MarketNewsItem> MarketNews { get; set; } = new List<MarketNewsItem>();
    }

    public class StockMarketIndex
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Index" or "Stock"
        public decimal CurrentValue { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal PreviousClose { get; set; }
    }

    public class Cryptocurrency
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal MarketCap { get; set; }
        public decimal Volume24h { get; set; }
        public decimal CirculatingSupply { get; set; }
        public decimal? TotalSupply { get; set; }
        public decimal PriceChange1h { get; set; }
        public decimal PriceChange7d { get; set; }
        public decimal PriceChange30d { get; set; }
        public decimal[]? SparklineData { get; set; }
        public string ChartColor { get; set; } = string.Empty;
        public string CoinGeckoUrl { get; set; } = string.Empty;
        public string TradeUrl { get; set; } = string.Empty;
    }

    public class ForexRate
    {
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
    }

    public class Commodity
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class WeatherInfo
    {
        public string City { get; set; } = string.Empty;
        public int Temperature { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsUserLocation { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Humidity { get; set; }
        public decimal WindSpeed { get; set; }
        public string FeelsLike { get; set; } = string.Empty;
    }

    public class MarketNewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? UrlToImage { get; set; }
    }

    public class StockDetailsViewModel
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public long Volume { get; set; }
        public decimal MarketCap { get; set; }
        public decimal PE { get; set; }
        public decimal DividendYield { get; set; }
        public decimal EPS { get; set; }
        public decimal Beta { get; set; }
        public decimal FiftyTwoWeekHigh { get; set; }
        public decimal FiftyTwoWeekLow { get; set; }
        public List<HistoricalPrice> HistoricalPrices { get; set; } = new List<HistoricalPrice>();
    }

    public class CryptoDetailsViewModel
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal MarketCap { get; set; }
        public decimal Volume24h { get; set; }
        public decimal CirculatingSupply { get; set; }
        public decimal TotalSupply { get; set; }
        public decimal AllTimeHigh { get; set; }
        public DateTime AllTimeHighDate { get; set; }
        public List<HistoricalPrice> HistoricalPrices { get; set; } = new List<HistoricalPrice>();
    }

    public class ForexDetailsViewModel
    {
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal PreviousClose { get; set; }
        public List<HistoricalPrice> HistoricalRates { get; set; } = new List<HistoricalPrice>();
    }
    
    public class CommodityDetailsViewModel
    {
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal PercentChange { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal PreviousClose { get; set; }
        public long Volume { get; set; }
        public string Unit { get; set; } = string.Empty;
        public List<HistoricalPrice> HistoricalPrices { get; set; } = new List<HistoricalPrice>();
    }

    public class HistoricalPrice
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
    }
}
