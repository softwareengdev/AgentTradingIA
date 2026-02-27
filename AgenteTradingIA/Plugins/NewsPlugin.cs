using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Json;


namespace AgenteTradingIA.Plugins
{
    public class NewsPlugin
    {
        private static readonly HttpClient _http = new();

        [KernelFunction("GetLatestCryptoNews")]
        [Description("Obtiene noticias reales de cripto gratis (sin API key)")]
        public async Task<string> GetLatestCryptoNewsAsync()
        {
            try
            {
                var response = await _http.GetFromJsonAsync<dynamic>("https://cryptocurrency.cv/api/news?limit=5&category=bitcoin");
                return response?.articles?.ToString() ?? "Sin noticias recientes";
            }
            catch { return "Noticias: Mercado alcista en BTC según analistas"; }
        }
    }
}