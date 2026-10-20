namespace Njord.Messages.Alerts;

public sealed record NoDetails : AlertDetails
{
    public static readonly NoDetails Instance = new();

    private NoDetails()
    {
    }
}
