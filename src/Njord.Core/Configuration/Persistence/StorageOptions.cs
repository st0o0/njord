namespace Njord.Core.Configuration.Persistence;

public sealed class StorageOptions
{
    public const string SectionName = "Njord:Storage";

    public string PersistencePath { get; set; } = string.Empty;
}
