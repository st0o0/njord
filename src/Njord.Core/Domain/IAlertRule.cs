using Njord.Core.Configuration;
using Njord.Messages.Alerts;

namespace Njord.Core.Domain;

public interface IAlertRule<in T> : IFeature
{
    Alert Evaluate(T input, AlertOptions options, DateTimeOffset now);
}
