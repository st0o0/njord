namespace Njord.OpenMeteo;

internal interface IGatewayBudgetGate
{
    bool TryAcquire(int weight);
    TimeSpan EstimateDelay(int weight);
}
