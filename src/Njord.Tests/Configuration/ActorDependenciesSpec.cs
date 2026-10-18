using System.Reflection;
using Akka.Actor;
using Microsoft.Extensions.DependencyInjection;

namespace Njord.Tests.Configuration;

// Actors are created through DI props, so a constructor dependency that no setup container registers only fails
// when the actor starts inside a running host. This pins the registrations against the constructors.
public sealed class ActorDependenciesSpec : IDisposable
{
    // Constructor arguments that the registration passes explicitly instead of resolving them from the container.
    private static readonly Dictionary<string, string> ExplicitArguments = new()
    {
        ["PollWorker.entityId"] = "the shard entity id, supplied by the shard region",
        ["SensorHub.expiryInterval"] = "optional override, the singleton registration passes none",
    };

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"njord-actor-deps-{Guid.NewGuid():N}");

    private static IEnumerable<Type> ActorTypes() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Njord*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && !name.EndsWith("Tests", StringComparison.Ordinal) && name != "Njord.Tests.Shared")
            .Select(name => Assembly.Load(name!))
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true } && typeof(ActorBase).IsAssignableFrom(type));

    [Fact]
    public void Every_constructor_dependency_of_every_production_actor_is_registered()
    {
        using var provider = HostConfiguration.Services(HostConfiguration.Sample(_tempDir)).BuildServiceProvider();
        var registered = provider.GetRequiredService<IServiceProviderIsService>();

        var unresolved = ActorTypes()
            .SelectMany(actor => actor.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .Take(1)
                .SelectMany(c => c.GetParameters())
                .Where(p => !registered.IsService(p.ParameterType)
                            && !ExplicitArguments.ContainsKey($"{actor.Name}.{p.Name}"))
                .Select(p => $"{actor.FullName}: {p.ParameterType.Name} {p.Name}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0, string.Join(Environment.NewLine, ["", .. unresolved]));
    }

    [Fact]
    public void The_scan_finds_the_pipeline_and_the_poll_actors()
    {
        var names = ActorTypes().Select(t => t.Name).ToHashSet();

        Assert.Multiple(
            () => Assert.Contains("ForecastPipeline", names),
            () => Assert.Contains("PollManager", names),
            () => Assert.Contains("PollWorker", names),
            () => Assert.Contains("BudgetTracker", names));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
