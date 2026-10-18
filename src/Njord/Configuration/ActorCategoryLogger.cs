using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Event;
using Akka.Hosting.Logging;

namespace Njord.Configuration;

// Replaces Akka.Hosting's LoggerFactoryLogger, which writes every Akka log event to ILogger<ActorSystem>.
// Each event goes to the category of its logging class instead: an actor's Context.GetLogger() events carry
// the actor type, Akka's own events keep their Akka.* type. Properties match the Akka.Hosting bridge.
public sealed class ActorCategoryLogger : ActorBase, IRequiresMessageQueue<ILoggerMessageQueueSemantics>
{
    private const string FallbackCategory = "Akka.Actor.ActorSystem";

    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Type, ILogger> _loggers = [];

    public ActorCategoryLogger()
    {
        var setup = Context.System.Settings.Setup.Get<LoggerFactorySetup>();
        if (!setup.HasValue)
        {
            throw new ConfigurationException(
                $"{nameof(ActorCategoryLogger)} needs a {nameof(LoggerFactorySetup)} in the actor system setup.");
        }

        _loggerFactory = setup.Value.LoggerFactory;
    }

    protected override bool Receive(object message)
    {
        switch (message)
        {
            case InitializeLogger:
                Sender.Tell(new LoggerInitialized());
                return true;
            case LogEvent log:
                Log(log);
                return true;
            default:
                return false;
        }
    }

    internal static string CategoryFor(Type? logClass) => logClass switch
    {
        null => FallbackCategory,
        { IsGenericType: true } => logClass.GetGenericTypeDefinition().FullName ?? logClass.Name,
        _ => logClass.FullName ?? logClass.Name,
    };

    private void Log(LogEvent log)
    {
        var logger = LoggerFor(log.LogClass);
        var level = log.LogLevel().ToLogLevel();
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var message = Format(log);
        logger.Log(level, default, State(log, message), log.Cause, (_, _) => message);
    }

    private ILogger LoggerFor(Type? logClass)
    {
        var key = logClass ?? typeof(ActorSystem);
        if (!_loggers.TryGetValue(key, out var logger))
        {
            logger = _loggerFactory.CreateLogger(CategoryFor(logClass));
            _loggers[key] = logger;
        }

        return logger;
    }

    private List<KeyValuePair<string, object?>> State(LogEvent log, string message)
    {
        var state = new List<KeyValuePair<string, object?>>();
        var trace = log.ActivityContext.GetValueOrDefault();
        if (trace.TraceId != default)
        {
            state.Add(new("Akka.TraceId", trace.TraceId));
            state.Add(new("Akka.SpanId", trace.SpanId));
            state.Add(new("Akka.TraceFlags", (int)trace.TraceFlags));
        }

        if (log.TryGetProperties(out var properties) && properties is not null)
        {
            state.AddRange(properties.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value)));
        }

        state.Add(new("ActorPath", Sender.Path.ToString()));
        state.Add(new("Timestamp", log.Timestamp));
        state.Add(new("Thread", log.Thread.ManagedThreadId));
        state.Add(new("LogSource", log.LogSource));
        state.Add(new("{OriginalFormat}", TemplateOf(log, message)));
        return state;
    }

    private static string TemplateOf(LogEvent log, string message)
    {
        var template = log.GetTemplate();
        return string.IsNullOrEmpty(template) ? message : template;
    }

    private static string Format(LogEvent log)
    {
        try
        {
            return log.Message?.ToString() ?? string.Empty;
        }
        catch (FormatException)
        {
            return log.Message is LogMessage malformed
                ? $"Malformed log message. Template: [{malformed.Format}], args: [{malformed.Unformatted()}]"
                : "Malformed log message.";
        }
    }
}
