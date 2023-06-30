using System.Text;

namespace OpenAbility.Logging;

public class LogWriter : TextWriter
{


	public override Encoding Encoding { get; }
	private string Message;
	private readonly Logger logger;
	private readonly LogSeverity severity;

	public LogWriter(string name, LogSeverity severity)
	{
		logger = Logger.Get(name);
		this.severity = severity;
	}
	
	public override void Flush()
	{
		logger.Log(severity, Message);
		Message = "";
	}

	public override void Write(char value)
	{
		if (value == '\n')
		{
			Flush();
			return;
		}
		Message += value;
	}
}
