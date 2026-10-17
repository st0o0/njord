namespace Njord.Mqtt.Payloads;

public sealed record ConsensusParameterPayload(string Key, double? Median, int Models)
{
    public string ModelsKey => $"{Key}_models";
}
