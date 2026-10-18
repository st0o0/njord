namespace Njord.IntegrationTests;

// A unique directory per fixture for the SQLite file and the override file, removed on dispose.
public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "njord-it-" + Guid.NewGuid().ToString("N"));

    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A SQLite handle that outlives the host on a slow runner must not fail the test run.
        }
    }
}
