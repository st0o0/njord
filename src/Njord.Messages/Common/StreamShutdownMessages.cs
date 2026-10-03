namespace Njord.Messages.Common;

public sealed record StopStreams;

public sealed record StreamsStopped;

public sealed record StreamsStopFailed(Exception Cause);
