namespace Njord.Tests.Shared;

public static class FixtureReader
{
    public static string Read(string name)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
