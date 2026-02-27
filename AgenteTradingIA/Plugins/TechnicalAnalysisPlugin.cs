using Microsoft.SemanticKernel;
using System.ComponentModel;

public class TechnicalAnalysisPlugin
{
    [KernelFunction("CalculateRSI")]
    [Description("Calcula RSI para un timeframe")]
    public string CalculateRSI(string timeframe) => $"RSI {timeframe} actual: {new Random().Next(20, 80)} (simulado para demo)";
}