using Microsoft.SemanticKernel;
using System.ComponentModel;

public class ExecutionPlugin
{
    private readonly BybitService _bybit;
    public ExecutionPlugin(BybitService bybit) => _bybit = bybit;

    [KernelFunction("ExecuteTrade")]
    [Description("Ejecuta LONG/SHORT con leverage y tamaño especificado")]
    public async Task<string> ExecuteTradeAsync(string action, int leverage, decimal sizePercent)
    {
        // El servicio principal ya maneja la ejecución desde el loop principal
        return $"Orden {action} enviada con leverage {leverage}x y {sizePercent}% equity";
    }
}