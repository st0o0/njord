namespace Njord.Core.Configuration;

public interface IConfigSection
{
    static abstract string SectionName { get; }

    static string SectionOf<T>() where T : IConfigSection => T.SectionName;
}
