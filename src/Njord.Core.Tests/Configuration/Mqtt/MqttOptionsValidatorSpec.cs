using Njord.Core.Configuration.Mqtt;
using Njord.Tests.Shared;

namespace Njord.Core.Tests.Configuration.Mqtt;

public sealed class MqttOptionsValidatorSpec
{
    private static readonly MqttOptionsValidator Validator = new();

    [Fact]
    public void Missing_host_is_rejected_when_enabled()
    {
        var result = Validator.Validate(null, new MqttOptions { Enabled = true, Host = "" });

        Assert.True(result.Failed);
        Assert.Contains("Mqtt", result.FailureMessage);
        Assert.Contains("Host", result.FailureMessage);
    }

    [Fact]
    public void Missing_host_is_accepted_when_disabled()
    {
        var result = Validator.Validate(null, AppSettingsDefaults.Options<MqttOptions>(o => { o.Enabled = false; o.Host = ""; }));

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Out_of_range_port_is_rejected()
    {
        var result = Validator.Validate(null, new MqttOptions { Host = "broker.local", Port = 70_000 });

        Assert.True(result.Failed);
        Assert.Contains("Port", result.FailureMessage);
    }

    [Fact]
    public void Failure_messages_never_contain_the_password()
    {
        var options = new MqttOptions { Enabled = true, Host = "", Password = "super-secret-broker-pass" };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.DoesNotContain("super-secret-broker-pass", result.FailureMessage);
    }
}
