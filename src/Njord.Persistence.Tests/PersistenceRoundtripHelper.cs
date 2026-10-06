using Newtonsoft.Json;

namespace Njord.Persistence.Tests;

public static class PersistenceRoundtripHelper
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
    };

    public static T Roundtrip<T>(T dto)
    {
        var json = JsonConvert.SerializeObject(dto, Settings);
        var result = JsonConvert.DeserializeObject<T>(json, Settings);
        return result!;
    }
}
