using Akka.Actor;
using Microsoft.Extensions.Options;
using Njord.Core.Configuration.Polling;
using Njord.Messages.Budget;

namespace Njord.OpenMeteo;

public interface IBudgetProvider
{
    BudgetRate GetCurrentRate();
}
