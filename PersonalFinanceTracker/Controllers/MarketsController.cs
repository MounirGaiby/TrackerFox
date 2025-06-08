using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.ViewModels;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalFinanceTracker.Controllers
{
    //[Authorize] // Temporarily commented for testing
    public class MarketsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<MarketsController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private readonly string _alphaVantageApiKey;
        private readonly string _coinGeckoApiKey;
        private readonly string _coinGeckoApiUrl;
        private readonly string _newsApiKey;
        private readonly string _openWeatherApiKey;

        public MarketsController(
            UserManager<User> userManager,
            ILogger<MarketsController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;

            _alphaVantageApiKey = _configuration["MarketAPIs:AlphaVantage:ApiKey"];
            _coinGeckoApiKey = _configuration["MarketAPIs:CoinGecko:ApiKey"];
            _coinGeckoApiUrl = _configuration["MarketAPIs:CoinGecko:ApiUrl"];
            _newsApiKey = _configuration["MarketAPIs:NewsAPI:ApiKey"];
            _openWeatherApiKey = _configuration["MarketAPIs:OpenWeather:ApiKey"];
        }

        public async Task<IActionResult> Index()
        {
            var marketData = new MarketDataViewModel();
            
            var fetchTasks = new List<Task>
            {
                FetchStockMarketData(marketData, _alphaVantageApiKey),
                FetchCryptocurrencyData(marketData, _coinGeckoApiUrl),
                FetchForexData(marketData, _alphaVantageApiKey),
                FetchMarketNews(marketData, _newsApiKey),
                FetchWeatherData(marketData, _openWeatherApiKey),
                Task.Run(() => marketData.Commodities = GetSimulatedCommoditiesData())
            };

            await Task.WhenAll(fetchTasks);

            if (fetchTasks.Any(t => t.IsFaulted))
            {
                 _logger.LogError("One or more market data fetch tasks failed.");
                 TempData["ErrorMessage"] = "Could not fetch some real-time market data. Showing simulated data for the failed sections.";
            }

            return View(marketData);
        }

        public async Task<IActionResult> StockDetails(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return BadRequest("Stock symbol cannot be empty.");

            try
            {
                var client = _httpClientFactory.CreateClient("AlphaVantage");
                var overviewTask = client.GetFromJsonAsync<AlphaVantageStockOverview>(
                    $"query?function=OVERVIEW&symbol={symbol}&apikey={_alphaVantageApiKey}");
                var timeSeriesTask = client.GetFromJsonAsync<AlphaVantageTimeSeriesDaily>(
                    $"query?function=TIME_SERIES_DAILY&symbol={symbol}&outputsize=full&apikey={_alphaVantageApiKey}");
                
                await Task.WhenAll(overviewTask, timeSeriesTask);

                var overview = await overviewTask;
                var timeSeries = await timeSeriesTask;

                if (overview == null || string.IsNullOrEmpty(overview.Symbol) || timeSeries?.DailyData == null || !timeSeries.DailyData.Any())
                {
                    TempData["ErrorMessage"] = $"Could not retrieve complete data for {symbol}. It may be an invalid symbol or an API limit was reached.";
                    return RedirectToAction(nameof(Index));
                }

                var historicalPrices = timeSeries.DailyData
                    .Select(kvp => new HistoricalPrice
                    {
                        Date = DateTime.Parse(kvp.Key),
                        Open = decimal.Parse(kvp.Value.Open, CultureInfo.InvariantCulture),
                        High = decimal.Parse(kvp.Value.High, CultureInfo.InvariantCulture),
                        Low = decimal.Parse(kvp.Value.Low, CultureInfo.InvariantCulture),
                        Close = decimal.Parse(kvp.Value.Close, CultureInfo.InvariantCulture),
                        Volume = long.Parse(kvp.Value.Volume, CultureInfo.InvariantCulture)
                    })
                    .OrderBy(p => p.Date)
                    .ToList();
                
                var latestPrice = historicalPrices.Last();
                var previousPrice = historicalPrices.Count > 1 ? historicalPrices[^2] : latestPrice;

                var stockDetails = new StockDetailsViewModel
                {
                    Symbol = overview.Symbol,
                    CompanyName = overview.Name,
                    CurrentPrice = latestPrice.Close,
                    Change = latestPrice.Close - previousPrice.Close,
                    PercentChange = previousPrice.Close == 0 ? 0 : (latestPrice.Close - previousPrice.Close) / previousPrice.Close * 100,
                    Open = latestPrice.Open,
                    High = latestPrice.High,
                    Low = latestPrice.Low,
                    Volume = latestPrice.Volume,
                    MarketCap = decimal.TryParse(overview.MarketCapitalization, out var mc) ? mc : 0,
                    PE = decimal.TryParse(overview.PERatio, out var pe) ? pe : 0,
                    DividendYield = decimal.TryParse(overview.DividendYield, out var dy) ? dy * 100 : 0,
                    EPS = decimal.TryParse(overview.EPS, out var eps) ? eps : 0,
                    Beta = decimal.TryParse(overview.Beta, out var beta) ? beta : 0,
                    FiftyTwoWeekHigh = decimal.TryParse(overview.FiftyTwoWeekHigh, out var wh) ? wh : 0,
                    FiftyTwoWeekLow = decimal.TryParse(overview.FiftyTwoWeekLow, out var wl) ? wl : 0,
                    HistoricalPrices = historicalPrices
                };

                ViewBag.HistoricalDataJson = JsonSerializer.Serialize(historicalPrices);
                return View(stockDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stock details for {Symbol}", symbol);
                TempData["ErrorMessage"] = "An error occurred while fetching stock details.";
                return RedirectToAction(nameof(Index));
            }
        }
        
        public async Task<IActionResult> CryptoDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest("Crypto ID cannot be empty.");
            
            try
            {
                var client = _httpClientFactory.CreateClient("CoinGecko");
                var detailsTask = client.GetFromJsonAsync<CoinGeckoCoinDetails>($"coins/{id}");
                var historyTask = client.GetFromJsonAsync<CoinGeckoMarketChart>($"coins/{id}/market_chart?vs_currency=usd&days=365&interval=daily");

                await Task.WhenAll(detailsTask, historyTask);

                var details = await detailsTask;
                var history = await historyTask;
                
                if (details == null || history == null || history.Prices == null)
                {
                    TempData["ErrorMessage"] = $"Could not retrieve data for {id}.";
                    return RedirectToAction(nameof(Index));
                }

                var historicalPrices = history.Prices
                    .Select(p => new HistoricalPrice
                    {
                        Date = DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).DateTime,
                        Close = (decimal)p[1]
                    })
                    .ToList();

                var cryptoDetails = new CryptoDetailsViewModel
                {
                    Symbol = details.Symbol.ToUpper(),
                    Name = details.Name,
                    CurrentPrice = details.MarketData.CurrentPrice["usd"],
                    Change = details.MarketData.PriceChange24hInCurrency["usd"],
                    PercentChange = details.MarketData.PriceChangePercentage24h,
                    MarketCap = details.MarketData.MarketCap["usd"],
                    Volume24h = details.MarketData.TotalVolume["usd"],
                    CirculatingSupply = details.MarketData.CirculatingSupply,
                    TotalSupply = details.MarketData.TotalSupply ?? 0,
                    AllTimeHigh = details.MarketData.AllTimeHigh["usd"],
                    AllTimeHighDate = details.MarketData.AllTimeHighDate["usd"],
                    HistoricalPrices = historicalPrices
                };
                
                ViewBag.HistoricalDataJson = JsonSerializer.Serialize(historicalPrices.Select(p => new {p.Date, p.Close}));
                return View(cryptoDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching crypto details for {ID}", id);
                TempData["ErrorMessage"] = "An error occurred while fetching cryptocurrency details.";
                return RedirectToAction(nameof(Index));
            }
        }
        
        public async Task<IActionResult> ForexDetails(string fromCurrency, string toCurrency)
        {
            if (string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency))
                return BadRequest("Currency symbols cannot be empty.");

            try
            {
                var client = _httpClientFactory.CreateClient("AlphaVantage");
                var timeSeries = await client.GetFromJsonAsync<AlphaVantageFxDaily>(
                    $"query?function=FX_DAILY&from_symbol={fromCurrency}&to_symbol={toCurrency}&outputsize=full&apikey={_alphaVantageApiKey}");
                
                if (timeSeries?.DailyData == null || !timeSeries.DailyData.Any())
                {
                    TempData["ErrorMessage"] = $"Could not retrieve data for {fromCurrency}/{toCurrency}.";
                    return RedirectToAction(nameof(Index));
                }

                var historicalRates = timeSeries.DailyData
                    .Select(kvp => new HistoricalPrice
                    {
                        Date = DateTime.Parse(kvp.Key),
                        Open = decimal.Parse(kvp.Value.Open, CultureInfo.InvariantCulture),
                        High = decimal.Parse(kvp.Value.High, CultureInfo.InvariantCulture),
                        Low = decimal.Parse(kvp.Value.Low, CultureInfo.InvariantCulture),
                        Close = decimal.Parse(kvp.Value.Close, CultureInfo.InvariantCulture)
                    })
                    .OrderBy(r => r.Date)
                    .ToList();

                var latestRate = historicalRates.Last();
                var previousRate = historicalRates.Count > 1 ? historicalRates[^2] : latestRate;

                var forexDetails = new ForexDetailsViewModel
                {
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Rate = latestRate.Close,
                    Change = latestRate.Close - previousRate.Close,
                    PercentChange = previousRate.Close == 0 ? 0 : (latestRate.Close - previousRate.Close) / previousRate.Close * 100,
                    Open = latestRate.Open,
                    High = latestRate.High,
                    Low = latestRate.Low,
                    PreviousClose = previousRate.Close,
                    HistoricalRates = historicalRates
                };

                ViewBag.HistoricalDataJson = JsonSerializer.Serialize(historicalRates);
                return View(forexDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching forex details for {From}/{To}", fromCurrency, toCurrency);
                TempData["ErrorMessage"] = "An error occurred while fetching forex details.";
                return RedirectToAction(nameof(Index));
            }
        }
        
        public async Task<IActionResult> CommodityDetails(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return BadRequest("Commodity symbol cannot be empty.");
            
            try
            {
                var client = _httpClientFactory.CreateClient("AlphaVantage");
                var function = GetCommodityApiFunction(symbol);
                var response = await client.GetFromJsonAsync<AlphaVantageCommodityResponse>(
                    $"query?function={function}&interval=daily&apikey={_alphaVantageApiKey}");
                
                if (response?.Data == null || !response.Data.Any())
                {
                    TempData["ErrorMessage"] = $"Could not retrieve data for {symbol}.";
                    return RedirectToAction(nameof(Index));
                }

                var historicalPrices = response.Data
                    .Select(d => new HistoricalPrice
                    {
                        Date = DateTime.Parse(d.Date),
                        Close = decimal.Parse(d.Value, CultureInfo.InvariantCulture)
                    })
                    .OrderBy(p => p.Date)
                    .ToList();

                var latestPrice = historicalPrices.Last();
                var previousPrice = historicalPrices.Count > 1 ? historicalPrices[^2] : latestPrice;
                
                var commodityDetails = new CommodityDetailsViewModel
                {
                    Symbol = symbol,
                    Name = response.Name,
                    Unit = response.Unit,
                    CurrentPrice = latestPrice.Close,
                    Change = latestPrice.Close - previousPrice.Close,
                    PercentChange = previousPrice.Close == 0 ? 0 : (latestPrice.Close - previousPrice.Close) / previousPrice.Close * 100,
                    PreviousClose = previousPrice.Close,
                    HistoricalPrices = historicalPrices,
                };
                
                ViewBag.HistoricalDataJson = JsonSerializer.Serialize(historicalPrices.Select(p => new {p.Date, p.Close}));
                return View(commodityDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching commodity details for {Symbol}", symbol);
                TempData["ErrorMessage"] = "An error occurred while fetching commodity details.";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetCommodityApiFunction(string symbol) => symbol.ToUpperInvariant() switch
        {
            "CL" => "WTI",
            "GC" => "GOLD",
            "SI" => "SILVER",
            "NG" => "NATURAL_GAS",
            _ => "WTI"
        };
        
        private async Task FetchStockMarketData(MarketDataViewModel marketData, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) { marketData.StockMarketIndices = GetSimulatedStockData(); return; }

            // Reduced list to stay within API limits (free tier: 25 requests/day)
            // Focus on major indices and most popular stocks
            var stocksAndIndices = new[] 
            { 
                // Market Indices (4)
                ("S&P 500", "SPY"),
                ("Dow Jones", "DIA"), 
                ("NASDAQ", "QQQ"),
                ("Russell 2000", "IWM"),
                
                // Major Tech Stocks (6)
                ("Apple", "AAPL"),
                ("Microsoft", "MSFT"),
                ("Amazon", "AMZN"),
                ("Tesla", "TSLA"),
                ("NVIDIA", "NVDA"),
                ("Alphabet", "GOOGL")
            };
            
            var tempIndices = new List<StockMarketIndex>();
            var client = _httpClientFactory.CreateClient("AlphaVantage");

            // Process in batches to avoid API rate limits
            var batches = stocksAndIndices.Chunk(5).ToList();
            
            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(async item =>
                {
                    try
                    {
                        var (name, symbol) = item;
                        var response = await client.GetFromJsonAsync<AlphaVantageGlobalQuoteResponse>(
                            $"query?function=GLOBAL_QUOTE&symbol={symbol}&apikey={apiKey}");
                        
                        if (response?.GlobalQuote != null && !string.IsNullOrEmpty(response.GlobalQuote.Price))
                        {
                            var quote = response.GlobalQuote;
                            var isIndex = name.Contains("S&P") || name.Contains("Dow") || name.Contains("NASDAQ") || name.Contains("Russell");
                            return new StockMarketIndex
                            {
                                Name = name,
                                Symbol = symbol,
                                Type = isIndex ? "Index" : "Stock",
                                CurrentValue = decimal.Parse(quote.Price, CultureInfo.InvariantCulture),
                                Change = decimal.Parse(quote.Change, CultureInfo.InvariantCulture),
                                PercentChange = decimal.Parse(quote.ChangePercent.TrimEnd('%'), CultureInfo.InvariantCulture)
                            };
                        }
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogError(ex, "Error fetching stock data for {Symbol}", item.Item2); 
                    }
                    return null;
                }).ToArray();

                var batchResults = await Task.WhenAll(batchTasks);
                tempIndices.AddRange(batchResults.Where(r => r != null));
                
                // Small delay between batches to respect API limits
                if (batches.IndexOf(batch) < batches.Count - 1)
                    await Task.Delay(1000);
            }
            
            marketData.StockMarketIndices = tempIndices.Any() ? tempIndices : GetSimulatedStockData();
        }
        
        private async Task FetchCryptocurrencyData(MarketDataViewModel marketData, string apiUrl)
        {
            if (string.IsNullOrEmpty(apiUrl)) { marketData.Cryptocurrencies = GetSimulatedCryptoData(); return; }
            
            try
            {
                var client = _httpClientFactory.CreateClient("CoinGecko");
                
                // Expanded list of popular cryptocurrencies (top 30 by market cap)
                var cryptoIds = "bitcoin,ethereum,tether,binancecoin,solana,usd-coin,ripple,staked-ether,dogecoin,toncoin,cardano,avalanche-2,shiba-inu,chainlink,tron,bitcoin-cash,polkadot,near,uniswap,internet-computer,kaspa,litecoin,dai,polygon,fetch-ai,stellar,cronos,monero,ethereum-classic,render-token";
                
                var url = $"coins/markets?vs_currency=usd&ids={cryptoIds}&order=market_cap_desc&sparkline=true&price_change_percentage=1h,24h,7d,30d&per_page=30";
                var cryptoData = await client.GetFromJsonAsync<List<CoinGeckoMarketResponse>>(url);

                if (cryptoData != null && cryptoData.Any())
                {
                    marketData.Cryptocurrencies = cryptoData.Select(c => new Cryptocurrency
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Symbol = c.Symbol.ToUpper(),
                        CurrentPrice = c.CurrentPrice,
                        Change = c.PriceChange24h,
                        PercentChange = c.PriceChangePercentage24h,
                        MarketCap = c.MarketCap,
                        Volume24h = c.TotalVolume,
                        CirculatingSupply = c.CirculatingSupply,
                        TotalSupply = c.TotalSupply,
                        PriceChange1h = c.PriceChangePercentage1h,
                        PriceChange7d = c.PriceChangePercentage7d,
                        PriceChange30d = c.PriceChangePercentage30d,
                        SparklineData = c.SparklineIn7d?.Price,
                        ChartColor = GetTrendColor(c.PriceChangePercentage24h),
                        CoinGeckoUrl = $"https://www.coingecko.com/en/coins/{c.Id}",
                        TradeUrl = GetCryptoTradeUrl(c.Symbol)
                    }).ToList();
                }
                else 
                { 
                    marketData.Cryptocurrencies = GetSimulatedCryptoData(); 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching cryptocurrency data");
                marketData.Cryptocurrencies = GetSimulatedCryptoData();
            }
        }

        private string GetTrendColor(decimal percentChange) => 
            percentChange switch
            {
                > 5 => "#22c55e",  // Significant gain
                > 0 => "#4ade80",  // Moderate gain
                < -5 => "#ef4444", // Significant loss
                < 0 => "#f87171",  // Moderate loss
                _ => "#6b7280"     // Neutral
            };

        private string GetCryptoTradeUrl(string symbol) =>
            $"https://www.binance.com/en/trade/{symbol}_USDT";
        
        private async Task FetchForexData(MarketDataViewModel marketData, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) { marketData.ForexRates = GetSimulatedForexData(); return; }

            var pairs = new[] { "EUR", "JPY", "GBP", "CAD" };
            var tempRates = new List<ForexRate>();
            var client = _httpClientFactory.CreateClient("AlphaVantage");
            
            foreach (var toCurrency in pairs)
            {
                try
                {
                    var response = await client.GetFromJsonAsync<AlphaVantageExchangeRateResponse>(
                        $"query?function=CURRENCY_EXCHANGE_RATE&from_currency=USD&to_currency={toCurrency}&apikey={apiKey}");
                    
                    if (response?.ExchangeRateData == null) continue;
                    
                    var rateData = response.ExchangeRateData;
                    tempRates.Add(new ForexRate
                    {
                        FromCurrency = rateData.FromCurrencyCode,
                        ToCurrency = rateData.ToCurrencyCode,
                        Rate = decimal.Parse(rateData.ExchangeRate, CultureInfo.InvariantCulture)
                    });
                }
                catch (Exception ex) { _logger.LogError(ex, "Error fetching forex data for USD/{ToCurrency}", toCurrency); }
            }
            marketData.ForexRates = tempRates.Any() ? tempRates : GetSimulatedForexData();
        }
        
        private async Task FetchMarketNews(MarketDataViewModel marketData, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) { marketData.MarketNews = GetSimulatedMarketNews(); return; }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "PersonalFinanceTracker");
                
                var newsCategories = new[] 
                { 
                    ("business", "Business"),
                    ("technology", "Technology"),
                    ("finance", "Finance")
                };

                var allNews = new List<MarketNewsItem>();
                
                foreach (var (category, categoryName) in newsCategories)
                {
                    var url = $"https://newsapi.org/v2/top-headlines?category={category}&language=en&pageSize=4&apiKey={apiKey}";
                    var newsResponse = await client.GetFromJsonAsync<NewsApiResponse>(url);

                    if (newsResponse?.Articles != null && newsResponse.Articles.Any())
                    {
                        allNews.AddRange(newsResponse.Articles.Select(a => new MarketNewsItem
                        {
                            Title = a.Title ?? "No title",
                            Summary = a.Description ?? "No description available",
                            Source = a.Source?.Name ?? "Unknown",
                            Category = categoryName,
                            Timestamp = a.PublishedAt,
                            Url = a.Url ?? "#",
                            UrlToImage = a.UrlToImage
                        }));
                    }
                }

                // Also fetch financial news from specific domains
                var domainsUrl = $"https://newsapi.org/v2/everything?domains=bloomberg.com,reuters.com,wsj.com,ft.com&language=en&pageSize=4&sortBy=publishedAt&apiKey={apiKey}";
                var financialNewsResponse = await client.GetFromJsonAsync<NewsApiResponse>(domainsUrl);
                
                if (financialNewsResponse?.Articles != null && financialNewsResponse.Articles.Any())
                {
                    allNews.AddRange(financialNewsResponse.Articles.Select(a => new MarketNewsItem
                    {
                        Title = a.Title ?? "No title",
                        Summary = a.Description ?? "No description available",
                        Source = a.Source?.Name ?? "Unknown",
                        Category = "Financial Markets",
                        Timestamp = a.PublishedAt,
                        Url = a.Url ?? "#",
                        UrlToImage = a.UrlToImage
                    }));
                }

                marketData.MarketNews = allNews
                    .OrderByDescending(n => n.Timestamp)
                    .Take(12)
                    .ToList();
                
                if (!marketData.MarketNews.Any())
                {
                    marketData.MarketNews = GetSimulatedMarketNews();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching market news");
                marketData.MarketNews = GetSimulatedMarketNews();
            }
        }
        
        private async Task FetchWeatherData(MarketDataViewModel marketData, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) 
            { 
                _logger.LogWarning("Weather API key is missing, using simulated data");
                marketData.WeatherData = GetSimulatedWeatherData(); 
                return; 
            }

            var majorFinancialCenters = new[] 
            { 
                "New York", "London", "Tokyo", "Hong Kong", "Singapore", "Frankfurt", 
                "Shanghai", "Sydney", "Casablanca", "Toronto", "Mumbai", "Seoul"
            };
            
            var tempWeather = new List<WeatherInfo>();
            var client = _httpClientFactory.CreateClient("OpenWeather");

            foreach (var city in majorFinancialCenters)
            {
                try
                {
                    var weatherResponse = await client.GetFromJsonAsync<OpenWeatherResponse>(
                        $"weather?q={city}&appid={apiKey}&units=metric");

                    if (weatherResponse?.Main != null && weatherResponse.Weather?.Length > 0)
                    {
                        tempWeather.Add(new WeatherInfo
                        {
                            City = weatherResponse.Name,
                            Temperature = (int)Math.Round(weatherResponse.Main.Temp),
                            Condition = weatherResponse.Weather[0].Main ?? "Unknown",
                            Description = weatherResponse.Weather[0].Description ?? "Unknown conditions",
                            Icon = GetWeatherIcon(weatherResponse.Weather[0].Main ?? "Unknown"),
                            IsUserLocation = false,
                            Humidity = weatherResponse.Main.Humidity,
                            WindSpeed = weatherResponse.Wind?.Speed ?? 0,
                            FeelsLike = $"{Math.Round(weatherResponse.Main.FeelsLike)}°C"
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Invalid weather response for city: {City}", city);
                    }
                }
                catch (Exception ex) 
                { 
                    _logger.LogError(ex, "Error fetching weather data for {City}", city); 
                }
            }
            
            _logger.LogInformation("Retrieved weather data for {Count} locations", tempWeather.Count);
            
            marketData.WeatherData = tempWeather.Any() ? 
                tempWeather.OrderBy(w => w.City).ToList() : 
                GetSimulatedWeatherData();
        }
        
        private string GetWeatherIcon(string condition) => condition.ToLower() switch
        {
            "clear" => "clear-day", "clouds" => "partly-cloudy-day", "rain" => "rainy",
            "snow" => "snowy", "thunderstorm" => "thunderstorm", "drizzle" => "rainy",
            "mist" or "fog" => "foggy", _ => "partly-cloudy-day"
        };
        
        #region Simulated Data Fallbacks
        private List<StockMarketIndex> GetSimulatedStockData() => new() 
        { 
            new() { Name = "S&P 500", Symbol = "SPY", Type = "Index", CurrentValue = 5280.10m, Change = -15.20m, PercentChange = -0.29m },
            new() { Name = "Dow Jones", Symbol = "DIA", Type = "Index", CurrentValue = 41850.34m, Change = 123.45m, PercentChange = 0.30m },
            new() { Name = "NASDAQ", Symbol = "QQQ", Type = "Index", CurrentValue = 16315.70m, Change = -45.67m, PercentChange = -0.28m },
            new() { Name = "Apple", Symbol = "AAPL", Type = "Stock", CurrentValue = 173.50m, Change = 2.15m, PercentChange = 1.25m },
            new() { Name = "Microsoft", Symbol = "MSFT", Type = "Stock", CurrentValue = 378.85m, Change = -1.23m, PercentChange = -0.32m },
            new() { Name = "Amazon", Symbol = "AMZN", Type = "Stock", CurrentValue = 131.20m, Change = 3.45m, PercentChange = 2.70m },
            new() { Name = "Tesla", Symbol = "TSLA", Type = "Stock", CurrentValue = 244.12m, Change = -8.33m, PercentChange = -3.30m },
            new() { Name = "NVIDIA", Symbol = "NVDA", Type = "Stock", CurrentValue = 502.66m, Change = 15.42m, PercentChange = 3.16m }
        };
        
        private List<Cryptocurrency> GetSimulatedCryptoData() => new() 
        { 
            new() { 
                Id = "bitcoin",
                Name = "Bitcoin", 
                Symbol = "BTC", 
                CurrentPrice = 68500.00m, 
                Change = -543.21m, 
                PercentChange = -0.79m, 
                MarketCap = 1350000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/bitcoin",
                TradeUrl = "https://www.binance.com/en/trade/BTC_USDT"
            },
            new() { 
                Id = "ethereum",
                Name = "Ethereum", 
                Symbol = "ETH", 
                CurrentPrice = 3420.15m, 
                Change = 45.67m, 
                PercentChange = 1.35m, 
                MarketCap = 410000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/ethereum",
                TradeUrl = "https://www.binance.com/en/trade/ETH_USDT"
            },
            new() { 
                Id = "tether",
                Name = "Tether", 
                Symbol = "USDT", 
                CurrentPrice = 1.00m, 
                Change = 0.001m, 
                PercentChange = 0.10m, 
                MarketCap = 83000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/tether",
                TradeUrl = "https://www.binance.com/en/trade/USDT_USD"
            },
            new() { 
                Id = "solana",
                Name = "Solana", 
                Symbol = "SOL", 
                CurrentPrice = 135.43m, 
                Change = -2.15m, 
                PercentChange = -1.56m, 
                MarketCap = 62000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/solana",
                TradeUrl = "https://www.binance.com/en/trade/SOL_USDT"
            },
            new() { 
                Id = "binancecoin",
                Name = "BNB", 
                Symbol = "BNB", 
                CurrentPrice = 578.90m, 
                Change = 12.34m, 
                PercentChange = 2.18m, 
                MarketCap = 58000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/bnb",
                TradeUrl = "https://www.binance.com/en/trade/BNB_USDT"
            },
            new() { 
                Id = "cardano",
                Name = "Cardano", 
                Symbol = "ADA", 
                CurrentPrice = 0.45m, 
                Change = 0.02m, 
                PercentChange = 4.65m, 
                MarketCap = 16000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/cardano",
                TradeUrl = "https://www.binance.com/en/trade/ADA_USDT"
            },
            new() { 
                Id = "ripple",
                Name = "XRP", 
                Symbol = "XRP", 
                CurrentPrice = 0.52m, 
                Change = -0.01m, 
                PercentChange = -1.85m, 
                MarketCap = 29000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/ripple",
                TradeUrl = "https://www.binance.com/en/trade/XRP_USDT"
            },
            new() { 
                Id = "dogecoin",
                Name = "Dogecoin", 
                Symbol = "DOGE", 
                CurrentPrice = 0.085m, 
                Change = 0.005m, 
                PercentChange = 6.25m, 
                MarketCap = 12000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/dogecoin",
                TradeUrl = "https://www.binance.com/en/trade/DOGE_USDT"
            },
            new() { 
                Id = "polygon",
                Name = "Polygon", 
                Symbol = "MATIC", 
                CurrentPrice = 0.72m, 
                Change = 0.04m, 
                PercentChange = 5.88m, 
                MarketCap = 7000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/polygon",
                TradeUrl = "https://www.binance.com/en/trade/MATIC_USDT"
            },
            new() { 
                Id = "chainlink",
                Name = "Chainlink", 
                Symbol = "LINK", 
                CurrentPrice = 15.45m, 
                Change = 0.78m, 
                PercentChange = 5.32m, 
                MarketCap = 9000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/chainlink",
                TradeUrl = "https://www.binance.com/en/trade/LINK_USDT"
            },
            new() { 
                Id = "litecoin",
                Name = "Litecoin", 
                Symbol = "LTC", 
                CurrentPrice = 88.50m, 
                Change = -1.25m, 
                PercentChange = -1.39m, 
                MarketCap = 6500000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/litecoin",
                TradeUrl = "https://www.binance.com/en/trade/LTC_USDT"
            },
            new() { 
                Id = "avalanche-2",
                Name = "Avalanche", 
                Symbol = "AVAX", 
                CurrentPrice = 28.75m, 
                Change = 1.15m, 
                PercentChange = 4.17m, 
                MarketCap = 11000000000m,
                CoinGeckoUrl = "https://www.coingecko.com/en/coins/avalanche",
                TradeUrl = "https://www.binance.com/en/trade/AVAX_USDT"
            }
        };
        
        private List<ForexRate> GetSimulatedForexData() => new() 
        { 
            new() { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.92m },
            new() { FromCurrency = "USD", ToCurrency = "JPY", Rate = 149.50m },
            new() { FromCurrency = "USD", ToCurrency = "GBP", Rate = 0.79m },
            new() { FromCurrency = "USD", ToCurrency = "CAD", Rate = 1.37m }
        };
        
        private List<Commodity> GetSimulatedCommoditiesData() => new() 
        { 
            new() { Name = "Gold", Symbol = "GC", CurrentPrice = 2350.50m, Change = 25.30m, PercentChange = 1.09m },
            new() { Name = "Silver", Symbol = "SI", CurrentPrice = 28.45m, Change = -0.23m, PercentChange = -0.80m },
            new() { Name = "Crude Oil", Symbol = "CL", CurrentPrice = 82.15m, Change = 1.45m, PercentChange = 1.80m }
        };
        
        private List<MarketNewsItem> GetSimulatedMarketNews() => new() 
        { 
            new() { Title = "Simulated: Market Hits Record High", Timestamp = DateTime.Now, Source = "Finance Times", Summary = "Indices reached a new peak today.", Category = "Markets" },
            new() { Title = "Simulated: Crypto Market Shows Strong Performance", Timestamp = DateTime.Now.AddHours(-2), Source = "Crypto Daily", Summary = "Major cryptocurrencies are seeing significant gains.", Category = "Cryptocurrency" },
            new() { Title = "Simulated: Tech Stocks Lead Market Rally", Timestamp = DateTime.Now.AddHours(-4), Source = "Tech News", Summary = "Technology sector outperforms broader market.", Category = "Technology" }
        };
        
        private List<WeatherInfo> GetSimulatedWeatherData() => new() 
        { 
            new() { City = "New York", Temperature = 22, Condition = "Clear", Icon = "clear-day", Description = "Sunny skies" },
            new() { City = "London", Temperature = 15, Condition = "Cloudy", Icon = "partly-cloudy-day", Description = "Partly cloudy" },
            new() { City = "Tokyo", Temperature = 18, Condition = "Rain", Icon = "rainy", Description = "Light rain" }
        };
        #endregion

        #region API Response Models
        private class AlphaVantageGlobalQuoteResponse { [JsonPropertyName("Global Quote")] public AlphaVantageGlobalQuote GlobalQuote { get; set; } }
        private class AlphaVantageGlobalQuote { [JsonPropertyName("05. price")] public string Price { get; set; } [JsonPropertyName("09. change")] public string Change { get; set; } [JsonPropertyName("10. change percent")] public string ChangePercent { get; set; } }
        private class AlphaVantageExchangeRateResponse { [JsonPropertyName("Realtime Currency Exchange Rate")] public AlphaVantageExchangeRateData ExchangeRateData { get; set; } }
        private class AlphaVantageExchangeRateData { [JsonPropertyName("1. From_Currency Code")] public string FromCurrencyCode { get; set; } [JsonPropertyName("3. To_Currency Code")] public string ToCurrencyCode { get; set; } [JsonPropertyName("5. Exchange Rate")] public string ExchangeRate { get; set; } }
        private class AlphaVantageStockOverview { [JsonPropertyName("Symbol")] public string Symbol { get; set; } [JsonPropertyName("Name")] public string Name { get; set; } [JsonPropertyName("MarketCapitalization")] public string MarketCapitalization { get; set; } [JsonPropertyName("PERatio")] public string PERatio { get; set; } [JsonPropertyName("EPS")] public string EPS { get; set; } [JsonPropertyName("DividendYield")] public string DividendYield { get; set; } [JsonPropertyName("Beta")] public string Beta { get; set; } [JsonPropertyName("52WeekHigh")] public string FiftyTwoWeekHigh { get; set; } [JsonPropertyName("52WeekLow")] public string FiftyTwoWeekLow { get; set; } }
        private class AlphaVantageTimeSeriesDaily { [JsonPropertyName("Time Series (Daily)")] public Dictionary<string, AlphaVantageDailyData> DailyData { get; set; } }
        private class AlphaVantageDailyData { [JsonPropertyName("1. open")] public string Open { get; set; } [JsonPropertyName("2. high")] public string High { get; set; } [JsonPropertyName("3. low")] public string Low { get; set; } [JsonPropertyName("4. close")] public string Close { get; set; } [JsonPropertyName("5. volume")] public string Volume { get; set; } }
        private class AlphaVantageFxDaily { [JsonPropertyName("Time Series FX (Daily)")] public Dictionary<string, AlphaVantageDailyData> DailyData { get; set; } }
        private class AlphaVantageCommodityResponse { [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("unit")] public string Unit { get; set; } [JsonPropertyName("data")] public List<AlphaVantageCommodityDataPoint> Data { get; set; } }
        private class AlphaVantageCommodityDataPoint { [JsonPropertyName("date")] public string Date { get; set; } [JsonPropertyName("value")] public string Value { get; set; } }
        
        private class CoinGeckoMarketResponse 
        { 
            [JsonPropertyName("id")] public string Id { get; set; } 
            [JsonPropertyName("symbol")] public string Symbol { get; set; } 
            [JsonPropertyName("name")] public string Name { get; set; } 
            [JsonPropertyName("current_price")] public decimal CurrentPrice { get; set; } 
            [JsonPropertyName("market_cap")] public decimal MarketCap { get; set; } 
            [JsonPropertyName("total_volume")] public decimal TotalVolume { get; set; }
            [JsonPropertyName("price_change_24h")] public decimal PriceChange24h { get; set; } 
            [JsonPropertyName("price_change_percentage_24h")] public decimal PriceChangePercentage24h { get; set; }
            [JsonPropertyName("price_change_percentage_1h_in_currency")] public decimal PriceChangePercentage1h { get; set; }
            [JsonPropertyName("price_change_percentage_7d_in_currency")] public decimal PriceChangePercentage7d { get; set; }
            [JsonPropertyName("price_change_percentage_30d_in_currency")] public decimal PriceChangePercentage30d { get; set; }
            [JsonPropertyName("circulating_supply")] public decimal CirculatingSupply { get; set; }
            [JsonPropertyName("total_supply")] public decimal? TotalSupply { get; set; }
            [JsonPropertyName("sparkline_in_7d")] public CoinGeckoSparklineData SparklineIn7d { get; set; }
        }

        private class CoinGeckoSparklineData
        {
            [JsonPropertyName("price")] public decimal[] Price { get; set; }
        }

        private class CoinGeckoCoinDetails { [JsonPropertyName("id")] public string Id { get; set; } [JsonPropertyName("symbol")] public string Symbol { get; set; } [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("market_data")] public CoinGeckoMarketData MarketData { get; set; } }
        private class CoinGeckoMarketData { [JsonPropertyName("current_price")] public Dictionary<string, decimal> CurrentPrice { get; set; } [JsonPropertyName("market_cap")] public Dictionary<string, decimal> MarketCap { get; set; } [JsonPropertyName("total_volume")] public Dictionary<string, decimal> TotalVolume { get; set; } [JsonPropertyName("price_change_24h_in_currency")] public Dictionary<string, decimal> PriceChange24hInCurrency { get; set; } [JsonPropertyName("price_change_percentage_24h")] public decimal PriceChangePercentage24h { get; set; } [JsonPropertyName("circulating_supply")] public decimal CirculatingSupply { get; set; } [JsonPropertyName("total_supply")] public decimal? TotalSupply { get; set; } [JsonPropertyName("ath")] public Dictionary<string, decimal> AllTimeHigh { get; set; } [JsonPropertyName("ath_date")] public Dictionary<string, DateTime> AllTimeHighDate { get; set; } }
        private class CoinGeckoMarketChart { [JsonPropertyName("prices")] public List<double[]> Prices { get; set; } }
        
        private class NewsApiResponse { [JsonPropertyName("articles")] public NewsArticle[] Articles { get; set; } }
        private class NewsArticle { [JsonPropertyName("source")] public NewsSource Source { get; set; } [JsonPropertyName("title")] public string Title { get; set; } [JsonPropertyName("description")] public string Description { get; set; } [JsonPropertyName("url")] public string Url { get; set; } [JsonPropertyName("urlToImage")] public string UrlToImage { get; set; } [JsonPropertyName("publishedAt")] public DateTime PublishedAt { get; set; } }
        private class NewsSource { [JsonPropertyName("name")] public string Name { get; set; } }
        
        private class OpenWeatherResponse 
        { 
            [JsonPropertyName("coord")] public Coord? Coord { get; set; }
            [JsonPropertyName("weather")] public WeatherCondition[] Weather { get; set; } 
            [JsonPropertyName("main")] public WeatherMain Main { get; set; }
            [JsonPropertyName("wind")] public Wind Wind { get; set; }
            [JsonPropertyName("sys")] public Sys Sys { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
        }
        
        private class WeatherCondition 
        { 
            [JsonPropertyName("main")] public string Main { get; set; }
            [JsonPropertyName("description")] public string Description { get; set; }
        }
        
        private class WeatherMain 
        { 
            [JsonPropertyName("temp")] public double Temp { get; set; }
            [JsonPropertyName("feels_like")] public double FeelsLike { get; set; }
            [JsonPropertyName("humidity")] public int Humidity { get; set; }
        }
        
        private class Wind 
        {
            [JsonPropertyName("speed")] public decimal Speed { get; set; }
            [JsonPropertyName("deg")] public int Degree { get; set; }
        }
        
        private class Coord
        {
            [JsonPropertyName("lat")] public double Lat { get; set; }
            [JsonPropertyName("lon")] public double Lon { get; set; }
        }
        
        private class Sys
        {
            [JsonPropertyName("country")] public string Country { get; set; }
            [JsonPropertyName("sunrise")] public long Sunrise { get; set; }
            [JsonPropertyName("sunset")] public long Sunset { get; set; }
        }
        #endregion

        // Test action for debugging weather API calls
        [HttpGet]
        public async Task<IActionResult> TestWeather(double? lat, double? lon, string? city = null)
        {
            if (!User.Identity.IsAuthenticated) return Unauthorized();
            
            var apiKey = _openWeatherApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return Json(new { error = "Weather API key not configured" });
            }

            var client = _httpClientFactory.CreateClient("OpenWeather");
            
            try
            {
                OpenWeatherResponse response;
                
                if (lat.HasValue && lon.HasValue)
                {
                    _logger.LogInformation("Testing weather API with coordinates: lat={Lat}, lon={Lon}", lat.Value, lon.Value);
                    response = await client.GetFromJsonAsync<OpenWeatherResponse>(
                        $"weather?lat={lat.Value}&lon={lon.Value}&appid={apiKey}&units=metric");
                }
                else if (!string.IsNullOrEmpty(city))
                {
                    _logger.LogInformation("Testing weather API with city: {City}", city);
                    response = await client.GetFromJsonAsync<OpenWeatherResponse>(
                        $"weather?q={city}&appid={apiKey}&units=metric");
                }
                else
                {
                    return Json(new { error = "Please provide either lat/lon coordinates or city name" });
                }

                if (response?.Main != null)
                {
                    return Json(new { 
                        success = true,
                        city = response.Name,
                        country = response.Sys?.Country,
                        temperature = Math.Round(response.Main.Temp),
                        description = response.Weather?[0]?.Description,
                        humidity = response.Main.Humidity,
                        coordinates = new { lat = response.Coord?.Lat, lon = response.Coord?.Lon }
                    });
                }
                else
                {
                    return Json(new { error = "Invalid response from weather API" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing weather API");
                return Json(new { error = ex.Message });
            }
        }
        
        // Public test endpoint that doesn't require authentication
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> TestAPIs()
        {
            var results = new
            {
                alphaVantageConfigured = !string.IsNullOrEmpty(_alphaVantageApiKey),
                openWeatherConfigured = !string.IsNullOrEmpty(_openWeatherApiKey),
                coinGeckoConfigured = !string.IsNullOrEmpty(_coinGeckoApiUrl),
                httpClientsTest = await TestHttpClients()
            };
            
            return Json(results);
        }
        
        private async Task<object> TestHttpClients()
        {
            var tests = new Dictionary<string, object>();
            
            // Test AlphaVantage client
            try
            {
                var alphaClient = _httpClientFactory.CreateClient("AlphaVantage");
                tests["alphaVantage"] = new { configured = true, baseAddress = alphaClient.BaseAddress?.ToString() };
            }
            catch (Exception ex)
            {
                tests["alphaVantage"] = new { configured = false, error = ex.Message };
            }
            
            // Test OpenWeather client
            try
            {
                var weatherClient = _httpClientFactory.CreateClient("OpenWeather");
                tests["openWeather"] = new { configured = true, baseAddress = weatherClient.BaseAddress?.ToString() };
            }
            catch (Exception ex)
            {
                tests["openWeather"] = new { configured = false, error = ex.Message };
            }
            
            // Test CoinGecko client
            try
            {
                var coinClient = _httpClientFactory.CreateClient("CoinGecko");
                tests["coinGecko"] = new { configured = true, baseAddress = coinClient.BaseAddress?.ToString() };
            }
            catch (Exception ex)
            {
                tests["coinGecko"] = new { configured = false, error = ex.Message };
            }
            
            // Test weather API call to Casablanca
            try
            {
                if (!string.IsNullOrEmpty(_openWeatherApiKey))
                {
                    var weatherClient = _httpClientFactory.CreateClient("OpenWeather");
                    var response = await weatherClient.GetFromJsonAsync<OpenWeatherResponse>(
                        $"weather?q=Casablanca,MA&appid={_openWeatherApiKey}&units=metric");
                    
                    if (response?.Main != null)
                    {
                        tests["casablancaWeather"] = new { 
                            success = true,
                            city = response.Name,
                            temperature = Math.Round(response.Main.Temp),
                            description = response.Weather?[0]?.Description
                        };
                    }
                    else
                    {
                        tests["casablancaWeather"] = new { success = false, error = "Invalid response" };
                    }
                }
                else
                {
                    tests["casablancaWeather"] = new { success = false, error = "No API key configured" };
                }
            }
            catch (Exception ex)
            {
                tests["casablancaWeather"] = new { success = false, error = ex.Message };
            }
            
            return tests;
        }
    }
}