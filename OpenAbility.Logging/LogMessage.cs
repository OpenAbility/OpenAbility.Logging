namespace OpenAbility.Logging;

public struct LogMessage
{
	public LogSeverity Severity;
	public string LoggerName;
	public string LoggerModule;
	public string Message;
	public string Formatted;
	public DateTime Time;
}