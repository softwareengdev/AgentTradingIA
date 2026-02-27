using Microsoft.SemanticKernel;
using System.ComponentModel;

public class RiskManagementPlugin
{
    [KernelFunction("ValidateRisk")]
    [Description("Valida que el riesgo no supere 8% del equity")]
    public bool ValidateRisk(decimal sizePercent, decimal equity) => sizePercent <= 8;
}