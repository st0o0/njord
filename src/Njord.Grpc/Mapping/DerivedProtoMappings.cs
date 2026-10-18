using Njord.Grpc.V2;
using Njord.Messages.Derived;
using Njord.Model;
using ProtoHorizonDerived = Njord.Grpc.V2.HorizonDerived;
using ProtoScalarDerived = Njord.Grpc.V2.ScalarDerived;

namespace Njord.Grpc.Mapping;

internal static class DerivedProtoMappings
{
    public static DerivedUpdate ToProto(this DerivedResult result)
    {
        var update = new DerivedUpdate();

        foreach (var (horizonKey, derived) in result.ByHorizon)
        {
            var proto = new ProtoHorizonDerived { Horizon = horizonKey.ToWireKey() };

            if (derived.Beaufort is { } b)
            {
                proto.Beaufort = b;
            }

            if (derived.WindChill is { } wc)
            {
                proto.WindChill = wc;
            }

            if (derived.DewPointComfort is { } dpc)
            {
                proto.DewpointComfort = dpc.ToProto();
            }

            if (derived.Condition is { } wmo)
            {
                proto.WmoDescription = wmo.ToDescription();
            }

            update.ByHorizon.Add(proto);
        }

        var scalars = new ProtoScalarDerived();
        if (result.Scalars.DiurnalAmplitude is { } da)
        {
            scalars.DiurnalAmplitude = da;
        }

        if (result.Scalars.SunshinePct is { } sp)
        {
            scalars.SunshinePct = sp;
        }

        if (result.Scalars.Inversion is { } inv)
        {
            scalars.Inversion = inv;
        }

        update.Scalars = scalars;

        return update;
    }
}
