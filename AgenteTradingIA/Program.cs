using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;
using AgenteTradingIA.Services;
using AgenteTradingIA.Plugins;
using System.Text.Json;

namespace AgenteTradingIA
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Information().CreateLogger();
            Log.Information("🚀 Iniciando Agente Trading IA - Objetivo 10x en 24h DEMO");

            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            builder.Services.AddSingleton<BybitService>();
            builder.Services.AddSingleton<TechnicalAnalysisService>();

            var host = builder.Build();
            var config = host.Services.GetRequiredService<IConfiguration>();
            var bybit = host.Services.GetRequiredService<BybitService>();

            var kernel = CreateKernel(config, bybit);

            var agent = new ChatCompletionAgent
            {
                Kernel = kernel,
                Instructions = "Eres un trader agresivo experto en BTCUSDT Perpetual. Objetivo: multiplicar capital DEMO 10x en 24h usando leverage 50x-100x. Siempre respeta risk management (máx 8% riesgo por trade). Usa solo datos reales, liquidez y sentimiento. Devuelve SIEMPRE JSON válido.",
                Name = "HighLeverageTrader"
            };

            while (true)
            {
                try
                {
                    await RunTradingCycleAsync(kernel, agent, bybit);
                    await Task.Delay(TimeSpan.FromSeconds(45)); // cada 45 segundos
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error en ciclo");
                    await Task.Delay(10000);
                }
            }
        }

        private static Kernel CreateKernel(IConfiguration config, BybitService bybit)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(config["OpenAI:Model"]!, config["OpenAI:ApiKey"]!);
            var kernel = builder.Build();

            kernel.ImportPluginFromType<MarketDataPlugin>(new MarketDataPlugin(bybit));
            kernel.ImportPluginFromType<ExecutionPlugin>(new ExecutionPlugin(bybit));
            kernel.ImportPluginFromType<TechnicalAnalysisPlugin>();
            kernel.ImportPluginFromType<NewsPlugin>();
            kernel.ImportPluginFromType<RiskManagementPlugin>();

            return kernel;
        }

        private static async Task RunTradingCycleAsync(Kernel kernel, ChatCompletionAgent agent, BybitService bybit)
        {
            var market = await bybit.GetMarketStateAsync("BTCUSDT");
            var news = await new NewsPlugin().GetLatestCryptoNewsAsync();

            var prompt = $"""
                ESTADO ACTUAL (BTCUSDT Perpetual):
                Precio: {market.LastPrice}
                Cambio 5m: {market.Change5m}%
                RSI 14 (M5): {market.Rsi5m}
                RSI 14 (H1): {market.Rsi1h}
                Liquidez 2%: {market.LiquidityDepth}%
                Balance USDT: {market.BalanceUsdt}
                Noticias recientes: {news}

                Analiza multi-timeframe, liquidez y sentimiento.
                Devuelve SOLO JSON válido:
                {{
                  "action": "LONG"|"SHORT"|"HOLD"|"CLOSE_ALL",
                  "leverage": 50-100,
                  "sizePercentOfEquity": 5-15,
                  "reasoning": "explicación detallada",
                  "confidence": 70-100
                }}
                """;

            var history = new ChatHistory { new ChatMessageContent(AuthorRole.User, prompt) };
            var result = await agent.InvokeAsync(history);

            Log.Information($"🤖 Decisión Agente: {result.Content}");

            var decision = JsonSerializer.Deserialize<TradingDecision>(result.Content) ?? new TradingDecision { Action = "HOLD" };

            if (decision.Confidence > 75 && decision.Action != "HOLD")
            {
                await bybit.ExecuteDecisionAsync(decision, market.LastPrice);
            }
            else
            {
                Log.Information("🛑 HOLD - Esperando mejor setup");
            }
        }
    }

    public class TradingDecision
    {
        public string Action { get; set; } = "HOLD";
        public int Leverage { get; set; } = 75;
        public decimal SizePercentOfEquity { get; set; } = 8;
        public string Reasoning { get; set; } = "";
        public int Confidence { get; set; } = 0;
    }
}