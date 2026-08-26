namespace UavMissionControl.Core.Logging;

public sealed record EventLogEntry(DateTimeOffset Timestamp, LogSeverity Severity, string Message);
