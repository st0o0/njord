using Newtonsoft.Json;

namespace Njord.Persistence.Tests;

public static class PersistenceRoundtripHelper
{
    private static readonly JsonSerializerSettings _settings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
    };

    public static T Roundtrip<T>(T dto)
    {
        var json = JsonConvert.SerializeObject(dto, _settings);
        var result = JsonConvert.DeserializeObject<T>(json, _settings);
        return result!;
    }
}
