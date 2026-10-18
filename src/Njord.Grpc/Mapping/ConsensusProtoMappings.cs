using Njord.Consensus;
using Njord.Grpc.V2;
using ProtoHorizonConsensus = Njord.Grpc.V2.HorizonConsensus;
using ProtoParameterConsensus = Njord.Grpc.V2.ParameterConsensus;

namespace Njord.Grpc.Mapping;

internal static class ConsensusProtoMappings
{
    public static ConsensusUpdate ToProto(this ConsensusResult result)
    {
        var update = new ConsensusUpdate();
        update.HourlyParameters.AddRange(result.Parameters.Select(p => p.ToProto()));
        update.DailyParameters.AddRange(result.DailyParameters.Select(p => p.ToProto()));
        return update;
    }

    private static ProtoParameterConsensus ToProto(this Njord.Consensus.ParameterConsensus paramConsensus)
    {
        var proto = new ProtoParameterConsensus
        {
            Parameter = paramConsensus.Parameter.ApiName,
            Unit = paramConsensus.Parameter.Unit,
        };

        foreach (var (horizonKey, horizon) in paramConsensus.ByHorizon)
        {
            proto.ByHorizon.Add(horizon.ToProto(horizonKey));
        }

        return proto;
    }

    private static ProtoHorizonConsensus ToProto(this Njord.Consensus.HorizonConsensus horizon, Njord.Model.Horizon horizonKey)
    {
        var protoHorizon = new ProtoHorizonConsensus
        {
            Horizon = horizonKey.ToWireKey(),
            AvailableModels = horizon.AvailableModels.Count,
        };

        if (horizon.Median is { } med)
        {
            protoHorizon.Median = med;
        }

        if (horizon.TrimmedMean is { } tm)
        {
            protoHorizon.TrimmedMean = tm;
        }

        if (horizon.Spread is { } s)
        {
            protoHorizon.Spread = s;
        }

        if (horizon.Iqr is { } iqr)
        {
            protoHorizon.Iqr = iqr;
        }

        if (horizon.Agreement is { } ag)
        {
            protoHorizon.Agreement = ag;
        }

        return protoHorizon;
    }
}
