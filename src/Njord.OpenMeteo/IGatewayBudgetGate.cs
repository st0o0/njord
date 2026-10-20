using Akka.Actor;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;

namespace Njord.OpenMeteo;

internal interface IGatewayBudgetGate
{
    bool TryAcquire(int weight);
    TimeSpan EstimateDelay(int weight);
}
