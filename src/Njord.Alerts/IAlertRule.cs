using Njord.Messages.Alerts;

namespace Njord.Alerts;

public interface IAlertRule<in TInput>
{
    AlertType Type { get; }

    Alert Evaluate(TInput input, AlertSettings settings, DateTimeOffset now);
}
