using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;


namespace AgenteTradingIA.Plugins
{
    public class MarketDataPlugin
    {
        private readonly BybitService _bybit;
        public MarketDataPlugin(BybitService bybit) => _bybit = bybit;

        [KernelFunction("GetCurrentMarketState")]
        [Description("Devuelve precio, RSI M5/H1, liquidez y balance actual")]
        public async Task<string> GetCurrentMarketStateAsync() => JsonSerializer.Serialize(await _bybit.GetMarketStateAsync());
    }
}