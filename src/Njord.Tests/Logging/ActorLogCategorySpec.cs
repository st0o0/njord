using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Njord.Configuration;
using Njord.Tests.Shared;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using AkkaLogging = Akka.Event.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using SerilogEvent = Serilog.Events.LogEvent;

namespace Njord.Tests.Logging;

// The actor system's logging as the host wires it (AkkaSetupContainer.ConfigureLogging) on a plain generic host:
// the Akka.Hosting TestKit installs its own loggers and would bypass the wiring under test.
public sealed class ActorLogCategorySpec
{
    private static readonly string ActorCategory = typeof(LoggingActor).FullName!;

    private static IConfiguration Configuration(IDictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? new Dictionary<string, string?>()).Build();

    private static IConfiguration ProductionConfiguration(IDictionary<string, string?>? overrides = null) =>
        new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Samples", "appsettings.json"), optional: false)
            .AddInMemoryCollection(overrides ?? new Dictionary<string, string?>())
            .Build();

    private static async Task<IHost> StartHost(
        IConfiguration configuration, Action<IServiceCollection> logging, CancellationToken cancellationToken)
    {
        var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                logging(services);
                services.AddAkka($"log-spec-{Guid.NewGuid():N}", (builder, _) =>
                    AkkaSetupContainer.ConfigureLogging(builder, configuration));
            })
            .Build();
        await host.StartAsync(cancellationToken);
        return host;
    }

    private static Task<IHost> StartHost(IConfiguration configuration, CapturingLoggerProvider provider, CancellationToken cancellationToken) =>
        StartHost(configuration, services => services.AddLogging(b => b
            .ClearProviders()
            .AddProvider(provider)
            .SetMinimumLevel(MelLogLevel.Trace)), cancellationToken);

    private static Task<IHost> StartHost(IConfiguration configuration, CapturingSink sink, CancellationToken cancellationToken)
    {
        var serilog = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Sink(sink)
            .CreateLogger();
        return StartHost(
            configuration,
            services => services.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory(serilog, dispose: true)),
            cancellationToken);
    }

    private static IActorRef LoggingActorIn(IHost host) =>
        host.Services.GetRequiredService<ActorSystem>().ActorOf(Props.Create(() => new LoggingActor()), $"logging-{Guid.NewGuid():N}");

    private static ILoggingAdapter AkkaInternalLogger(IHost host) =>
        AkkaLogging.GetLogger(host.Services.GetRequiredService<ActorSystem>(), "akka-internal-source");

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_actor_info_log_arrives_with_the_actor_type_as_category()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new CapturingLoggerProvider();
        using var host = await StartHost(Configuration(), provider, ct);

        LoggingActorIn(host).Tell(new LogInfo("vreden"));

        var entry = await provider.WaitFor(e => e.Message == "Info for vreden", ct);
        Assert.Multiple(
            () => Assert.Equal(ActorCategory, entry.Category),
            () => Assert.Equal(MelLogLevel.Information, entry.Level));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_actor_log_keeps_its_structured_properties()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new CapturingLoggerProvider();
        using var host = await StartHost(Configuration(), provider, ct);
        var actor = LoggingActorIn(host);

        actor.Tell(new LogInfo("vreden"));

        var entry = await provider.WaitFor(e => e.Message == "Info for vreden", ct);
        Assert.Multiple(
            () => Assert.Equal("vreden", entry.Properties["Location"]),
            () => Assert.Equal("Info for {Location}", entry.Properties["{OriginalFormat}"]),
            () => Assert.Equal(actor.Path.ToString(), entry.Properties["ActorPath"]),
            () => Assert.Equal(actor.Path.ToString(), entry.Properties["LogSource"]));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_akka_internal_log_keeps_an_akka_category()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new CapturingLoggerProvider();
        using var host = await StartHost(Configuration(), provider, ct);

        AkkaInternalLogger(host).Info("Internal {Value}", 42);

        var entry = await provider.WaitFor(e => e.Message == "Internal 42", ct);
        Assert.Equal(typeof(DummyClassForStringSources).FullName, entry.Category);
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task An_actor_debug_log_arrives_when_the_configured_minimum_is_debug()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new CapturingLoggerProvider();
        using var host = await StartHost(
            Configuration(new Dictionary<string, string?> { ["Serilog:MinimumLevel:Default"] = "Debug" }), provider, ct);

        LoggingActorIn(host).Tell(new LogDebug("vreden"));

        var entry = await provider.WaitFor(e => e.Message == "Debug for vreden", ct);
        Assert.Multiple(
            () => Assert.Equal(ActorCategory, entry.Category),
            () => Assert.Equal(MelLogLevel.Debug, entry.Level));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task Production_settings_show_actor_info_logs_and_hide_akka_internal_info_logs()
    {
        var ct = TestContext.Current.CancellationToken;
        var sink = new CapturingSink();
        using var host = await StartHost(ProductionConfiguration(), sink, ct);
        var akka = AkkaInternalLogger(host);

        akka.Info("Internal info");
        akka.Warning("Internal warning");
        LoggingActorIn(host).Tell(new LogInfo("vreden"));

        var actorEvent = await sink.WaitFor(e => e.RenderMessage() == "Info for \"vreden\"", ct);
        await sink.WaitFor(e => e.RenderMessage() == "Internal warning", ct);
        Assert.Multiple(
            () => Assert.Equal($"\"{ActorCategory}\"", actorEvent.Properties["SourceContext"].ToString()),
            () => Assert.Equal(LogEventLevel.Information, actorEvent.Level),
            () => Assert.DoesNotContain(sink.Events, e => e.RenderMessage() == "Internal info"));
    }

    [Fact(Timeout = TestTimeouts.Hosted)]
    public async Task A_debug_override_for_a_namespace_shows_that_actors_debug_logs()
    {
        var ct = TestContext.Current.CancellationToken;
        var sink = new CapturingSink();
        using var host = await StartHost(
            ProductionConfiguration(new Dictionary<string, string?> { ["Serilog:MinimumLevel:Override:Njord.Tests"] = "Debug" }),
            sink,
            ct);
        var akka = AkkaInternalLogger(host);

        akka.Debug("Internal debug");
        LoggingActorIn(host).Tell(new LogDebug("vreden"));
        akka.Warning("Internal warning");

        var actorEvent = await sink.WaitFor(e => e.RenderMessage() == "Debug for \"vreden\"", ct);
        await sink.WaitFor(e => e.RenderMessage() == "Internal warning", ct);
        Assert.Multiple(
            () => Assert.Equal($"\"{ActorCategory}\"", actorEvent.Properties["SourceContext"].ToString()),
            () => Assert.Equal(LogEventLevel.Debug, actorEvent.Level),
            () => Assert.DoesNotContain(sink.Events, e => e.RenderMessage() == "Internal debug"));
    }

    [Fact]
    public void A_generic_logging_class_is_categorized_by_its_generic_definition() =>
        Assert.Equal("System.Collections.Generic.List`1", ActorCategoryLogger.CategoryFor(typeof(List<int>)));

    [Fact]
    public void An_event_without_a_logging_class_falls_back_to_the_actor_system_category() =>
        Assert.Equal("Akka.Actor.ActorSystem", ActorCategoryLogger.CategoryFor(null));

    private sealed record LogInfo(string Location);

    private sealed record LogDebug(string Location);

    private sealed class LoggingActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public LoggingActor()
        {
            Receive<LogInfo>(m => _log.Info("Info for {Location}", m.Location));
            Receive<LogDebug>(m => _log.Debug("Debug for {Location}", m.Location));
        }
    }

    // Completes a wait as soon as a matching item was captured, or fails with a descriptive message after the
    // real-time bound.
    private sealed class Capture<T>
    {
        private readonly Lock _gate = new();
        private readonly List<T> _items = [];
        private readonly List<(Func<T, bool> Match, TaskCompletionSource<T> Done)> _waiters = [];

        public IReadOnlyList<T> Items
        {
            get
            {
                lock (_gate)
                {
                    return [.. _items];
                }
            }
        }

        public void Add(T item)
        {
            lock (_gate)
            {
                _items.Add(item);
                foreach (var waiter in _waiters.Where(w => w.Match(item)).ToList())
                {
                    waiter.Done.TrySetResult(item);
                    _waiters.Remove(waiter);
                }
            }
        }

        public async Task<T> WaitFor(Func<T, bool> match, CancellationToken cancellationToken)
        {
            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                var existing = _items.FirstOrDefault(match);
                if (existing is not null)
                {
                    return existing;
                }

                _waiters.Add((match, done));
            }

            try
            {
                return await done.Task.WaitAsync(TestTimeouts.RealTimeWaitMax, cancellationToken);
            }
            catch (TimeoutException)
            {
                Assert.Fail($"No matching log entry within {TestTimeouts.RealTimeWaitMax}; captured: "
                    + string.Join(" | ", Items));
                throw;
            }
        }
    }

    private sealed record CapturedEntry(string Category, MelLogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly Capture<CapturedEntry> _capture = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _capture);

        public Task<CapturedEntry> WaitFor(Func<CapturedEntry, bool> match, CancellationToken cancellationToken) =>
            _capture.WaitFor(match, cancellationToken);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, Capture<CapturedEntry> capture) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(MelLogLevel logLevel) => true;

            public void Log<TState>(
                MelLogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.ToDictionary(p => p.Key, p => p.Value)
                    : new Dictionary<string, object?>();
                capture.Add(new CapturedEntry(category, logLevel, formatter(state, exception), properties));
            }
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly Capture<SerilogEvent> _capture = new();

        public IReadOnlyList<SerilogEvent> Events => _capture.Items;

        public void Emit(SerilogEvent logEvent) => _capture.Add(logEvent);

        public Task<SerilogEvent> WaitFor(Func<SerilogEvent, bool> match, CancellationToken cancellationToken) =>
            _capture.WaitFor(match, cancellationToken);
    }
}
