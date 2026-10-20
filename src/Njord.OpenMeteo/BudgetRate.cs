using Akka.Actor;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;

namespace Njord.OpenMeteo;

public sealed record BudgetRate(int CostPerMinute, int MaxBurst);
