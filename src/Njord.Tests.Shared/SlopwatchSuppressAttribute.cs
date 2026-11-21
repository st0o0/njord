namespace Njord.Tests.Shared;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class SlopwatchSuppressAttribute(string rule, string reason) : Attribute
{
    public string Rule { get; } = rule;
    public string Reason { get; } = reason;
}
