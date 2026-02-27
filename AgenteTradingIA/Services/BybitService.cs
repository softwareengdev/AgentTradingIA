using AgenteTradingIA;
using Bybit.Net;
using Bybit.Net.Clients;
using Bybit.Net.Enums;
using Bybit.Net.Interfaces.Clients;
using Bybit.Net.Objects.Options;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using Serilog;
using Skender.Stock.Indicators;

public class BybitService
{
    private readonly IBybitRestClient _client;
    private readonly IConfiguration _config;

    public BybitService(IConfiguration config)
    {
        _config = config;

        // Constructor correcto (el original no compilaba)
        _client = new BybitRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(
                config["Bybit:ApiKey"] ?? throw new InvalidOperationException("Bybit:ApiKey no encontrada"),
                config["Bybit:ApiSecret"] ?? throw new InvalidOperationException("Bybit:ApiSecret no encontrada")
            );

            options.Environment = config.GetValue<bool>("Bybit:UseDemo")
                ? BybitEnvironment.DemoTrading
                : BybitEnvironment.Live;
        });
    }

    public async Task<MarketState> GetMarketStateAsync(string symbol = "BTCUSDT")
    {
        // Ticker (usando ExchangeData que es la ruta correcta en Bybit.Net)
        var tickerResult = await _client.V5Api.ExchangeData.GetTickersAsync(Category.Linear, symbol);
        if (!tickerResult.Success || tickerResult.Data?.List?.Any() != true)
            throw new Exception($"Error obteniendo ticker: {tickerResult.Error?.Message}");

        var price = tickerResult.Data.List.First().LastPrice;

        // Klines 5m
        var klines5m = await _client.V5Api.ExchangeData.GetKlinesAsync(Category.Linear, symbol, KlineInterval.FiveMinutes, 200);
        var quotes5m = klines5m.Data.Select(k => new Quote
        {
            Date = k.OpenTime,
            Open = k.OpenPrice,   // Propiedad correcta en Bybit.Net V5
            High = k.HighPrice,
            Low = k.LowPrice,
            Close = k.ClosePrice
        }).ToList();

        var rsi5m = quotes5m.GetRsi(14).LastOrDefault()?.Rsi ?? 50m;

        // Klines 1h
        var klines1h = await _client.V5Api.ExchangeData.GetKlinesAsync(Category.Linear, symbol, KlineInterval.OneHour, 100);
        var quotes1h = klines1h.Data.Select(k => new Quote
        {
            Date = k.OpenTime,
            Open = k.OpenPrice,
            High = k.HighPrice,
            Low = k.LowPrice,
            Close = k.ClosePrice
        }).ToList();

        var rsi1h = quotes1h.GetRsi(14).LastOrDefault()?.Rsi ?? 50m;

        // Balance (Unified es más moderno/recomendado)
        var balanceResult = await _client.V5Api.Account.GetBalanceAsync(AccountType.Unified);
        var usdt = balanceResult.Success
            ? balanceResult.Data.List?.FirstOrDefault(b => b.Coin == "USDT")?.Available ?? 1000m
            : 1000m;

        // Liquidez aproximada
        var ob = await _client.V5Api.ExchangeData.GetOrderBookAsync(Category.Linear, symbol, 50);
        var depth = ob.Success
            ? (ob.Data.Asks.Sum(a => a.Quantity) + ob.Data.Bids.Sum(b => b.Quantity)) / 2m
            : 0m;

        return new MarketState
        {
            LastPrice = price,
            Change5m = quotes5m.Count >= 2
                ? (price - quotes5m[^2].Close) / quotes5m[^2].Close * 100   // Cambio real de los últimos 5 min
                : 0,
            Rsi5m = rsi5m,
            Rsi1h = rsi1h,
            LiquidityDepth = depth,
            BalanceUsdt = usdt
        };
    }

    public async Task ExecuteDecisionAsync(TradingDecision decision, decimal entryPrice, string symbol = "BTCUSDT")
    {
        if (decision.SizePercentOfEquity <= 0) return;

        // 1. Configurar leverage (OBLIGATORIO antes de la orden)
        await _client.V5Api.Account.SetLeverageAsync(
            Category.Linear,
            symbol,
            buyLeverage: decision.Leverage,
            sellLeverage: decision.Leverage);   // One-Way mode (el más común)

        // 2. Calcular cantidad
        decimal equityToRisk = 1000m * (decision.SizePercentOfEquity / 100m);
        decimal qty = (equityToRisk * decision.Leverage) / entryPrice;
        qty = Math.Max(Math.Round(qty, 3, MidpointRounding.AwayFromZero), 0.001m); // mínimo válido BTCUSDT

        var side = decision.Action?.Trim().ToUpper() == "LONG" ? OrderSide.Buy : OrderSide.Sell;

        // 3. Colocar orden
        var orderResult = await _client.V5Api.Trading.PlaceOrderAsync(
            Category.Linear,
            symbol: symbol,
            side: side,
            type: NewOrderType.Market,
            quantity: qty);

        if (!orderResult.Success)
        {
            Log.Error("❌ Error colocando orden: {Error}", orderResult.Error?.Message);
            throw new Exception($"Fallo en orden: {orderResult.Error?.Message}");
        }

        Log.Information("✅ ORDEN EJECUTADA: {Action} {Qty:F4} {Symbol} @ {Price} (lev {Leverage}x)",
            decision.Action, qty, symbol, entryPrice, decision.Leverage);
    }
}