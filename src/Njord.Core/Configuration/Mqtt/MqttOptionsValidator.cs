using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration.Mqtt;

public sealed class MqttOptionsValidator : IValidateOptions<MqttOptions>
{
    public ValidateOptionsResult Validate(string? name, MqttOptions options)
    {
        var failures = new List<string>();

        if (options.Enabled && string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("The MQTT broker host is missing: set Njord:Mqtt:Host.");
        }

        if (options.Port is < 1 or > 65535)
        {
            failures.Add($"Njord:Mqtt:Port must be between 1 and 65535, got {options.Port}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
