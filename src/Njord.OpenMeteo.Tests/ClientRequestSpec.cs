using Njord.OpenMeteo.Http;
namespace Njord.OpenMeteo.Tests;

public sealed class ClientRequestSpec
{
    [Fact]
    public void A_comma_list_is_joined_by_a_plain_comma()
    {
        var uri = ClientRequest.Uri("v1/ensemble", 47.05, 8.31, ("hourly", ClientRequest.List(["temperature_2m", "rain"])));

        Assert.Equal(
            "v1/ensemble?latitude=47.05&longitude=8.31&hourly=temperature_2m,rain&timeformat=unixtime&timezone=UTC",
            uri);
    }
}
