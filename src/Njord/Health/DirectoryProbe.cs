namespace Njord.Health;

internal static class DirectoryProbe
{
    internal static bool CanWrite(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return false;
            }

            var probe = Path.Combine(directory, Path.GetRandomFileName());
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
