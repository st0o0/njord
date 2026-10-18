using System.Runtime.ExceptionServices;
using Njord.Grpc.V2;
using Contracts = Njord.Messages.Config;
using DomainSection = Njord.Messages.Config.EnrichmentSection;
using ProtoSection = Njord.Grpc.V2.EnrichmentSection;

namespace Njord.Grpc.Mapping;

internal static class ConfigResponses
{
    // Wire values the command cannot express are reported as issues before the configuration manager is asked.
    public static ConfigResponse? RejectUnknownValues(this SetEnrichmentRequest request)
    {
        List<Contracts.ConfigIssue> issues = [];
        if (request.Consensus is { HasMethod: true } consensus && !Enum.IsDefined(consensus.Method))
        {
            issues.Add(new Contracts.ConfigIssue(
                DomainSection.Consensus, $"Unknown consensus method: {(int)consensus.Method}"));
        }

        issues.AddRange(request.Reset
            .Where(section => section == ProtoSection.Unspecified || !Enum.IsDefined(section))
            .Select(section => new Contracts.ConfigIssue(
                null, $"Unknown or unspecified section in reset: {(int)section}")));

        return issues.Count > 0 ? Rejected(issues, request.DryRun) : null;
    }

    public static ConfigResponse Applied(Contracts.EffectiveConfig effective, IReadOnlyList<string> restartRequired)
    {
        var config = effective.ToProto();
        var response = new ConfigResponse
        {
            Applied = true,
            Config = config,
            BudgetProjection = config.BudgetProjection.Clone(),
        };
        response.Warnings.AddRange(effective.BudgetProjection.Warnings);
        response.RestartRequired.AddRange(restartRequired);
        return response;
    }

    public static ConfigResponse Validated(Contracts.EffectiveConfig effective, IReadOnlyList<string> restartRequired)
    {
        var response = Applied(effective, restartRequired);
        response.Applied = false;
        response.DryRun = true;
        return response;
    }

    public static ConfigResponse Rejected(IReadOnlyList<Contracts.ConfigIssue> issues, bool dryRun)
    {
        var response = Rejected(string.Join("; ", issues.Select(i => i.Message)));
        response.DryRun = dryRun;
        response.Issues.AddRange(issues.Select(i => new ConfigIssue
        {
            Section = i.Section.ToProto(),
            Message = i.Message,
        }));
        return response;
    }

    public static ConfigResponse Rejected(string reason) =>
        new()
        {
            Applied = false,
            RejectionReason = reason,
        };

    // A failure inside the configuration manager surfaces as it did when the API wrote the configuration itself.
    public static T Rethrow<T>(Exception cause)
    {
        ExceptionDispatchInfo.Capture(cause).Throw();
        return default!;
    }

    public static ArgumentOutOfRangeException Unknown(object response) =>
        new(nameof(response), response.GetType().Name, "Unknown configuration response");
}
