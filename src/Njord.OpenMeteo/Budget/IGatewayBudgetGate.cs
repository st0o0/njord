namespace Njord.OpenMeteo.Budget;

internal interface IGatewayBudgetGate
{
    bool TryAcquire(int weight);
}
