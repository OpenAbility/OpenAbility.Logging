using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenAbility.Logging;

/// <summary>
/// The meat and bones of the system. It provides loggers and logging!
/// </summary>
public class Logger
{
	private static readonly Dictionary<string, Logger> Loggers = new Dictionary<string, Logger>();
	private static readonly List<TextWriter> Outputs = new List<TextWriter>();
	private static readonly List<LogMessage> Messages = new List<LogMessage>();
	private static readonly string GlobalFormat = "[%severity%/%thread%] (%time%) (%name%/%module%): %message%";

	private static readonly TextWriter ConsoleOut;
	private static readonly object Lock = new object();

	public static LogSeverity MinimumSeverity = LogSeverity.Debug;
    
	
	/// <summary>
	/// Create any needed things
	/// </summary>
	static Logger()
	{
		Outputs.Add(Console.Out);
		ConsoleOut = Console.Out;
	}

	public static void RegisterLogFile(string path)
	{
		TextWriter writer = File.CreateText(path);
		foreach (var message in Messages.ToArray())
		{
			Print(message, writer);
		}
		Outputs.Add(writer);
	}

	/// <summary>
	/// Get a logger by name
	/// </summary>
	/// <param name="name">The name of the logger</param>
	/// <param name="module">The module of the logger</param>
	/// <returns>The logger with that name</returns>
	public static Logger Get(string name, string module = "")
	{
		string id = name + "/" + module;
		if (Loggers.TryGetValue(id, out Logger? existing))
			return existing;

		Logger logger = new Logger(name, module);
		Loggers[id] = logger;
		return logger;
	}

	public static void AddOutput(TextWriter output)
	{
		Outputs.Add(output);
	}

	public static List<LogMessage> GetMessages()
	{
		return Messages;
	}

	/// <summary>
	/// Get a logger by type
	/// </summary>
	/// <param name="module">The module of the logger</param>
	/// <typeparam name="T">The type to get the logger from</typeparam>
	/// <returns>The logger with the same name as the type</returns>
	public static Logger Get<T>()
	{
		if(typeof(T).Namespace == null)
			return Get(typeof(T).Name);
		return Get(typeof(T).Namespace!, typeof(T).Name);
	}

	private readonly string format;
	private readonly string name;
	private readonly string module;
	private Logger(string name, string module)
	{
		format = GlobalFormat;
		this.name = name;
		this.module = module;
	}
	
	/// <summary>
	/// Log a debug message, wrapper for <see cref="Log"/>
	/// </summary>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The replacements</param>
	public void Debug(string fmt, params object?[] content)
	{
		Log(LogSeverity.Debug, fmt, content);
	}
	
	/// <summary>
	/// Log an info message, wrapper for <see cref="Log"/>
	/// </summary>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The replacements</param>
	public void Info(string fmt, params object?[] content)
	{
		Log(LogSeverity.Info, fmt, content);
	}
	
	/// <summary>
	/// Log a warning message, wrapper for <see cref="Log"/>
	/// </summary>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The replacements</param>
	public void Warning(string fmt, params object?[] content)
	{
		Log(LogSeverity.Warning, fmt, content);
	}
	
	/// <summary>
	/// Log an error message, wrapper for <see cref="Log"/>
	/// </summary>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The replacements</param>
	public void Error(string fmt, params object?[] content)
	{
		Log(LogSeverity.Error, fmt, content);
	}
	
	/// <summary>
	/// Log a fatal message, wrapper for <see cref="Log"/>
	/// </summary>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The replacements</param>
	public void Fatal(string fmt, params object?[] content)
	{
		Log(LogSeverity.Fatal, fmt, content);
	}

	public void Throw(string fmt, params object?[] content)
	{
		StackTrace stackTrace = new StackTrace(2);
		Log(LogSeverity.Fatal, "{}\n{}", Format(fmt, content), stackTrace.ToString());
	}

	private string Format(string fmt, params object?[] content)
	{
		for (int i = 0; i < content.Length; i++)
		{
			fmt = fmt.Replace("{" + i + "}", content[i]?.ToString() ?? "null");

			int firstEmpty = fmt.IndexOf("{}", StringComparison.InvariantCulture);
			if (firstEmpty >= 0)
			{
				fmt = fmt[..firstEmpty] + content[i] + fmt[(firstEmpty + 2)..];
			}
		}
		return fmt;
	}

	/// <summary>
	/// The core backend for logging, it logs a message by severity, format and inlines content.
	/// Content is inlined via <c>{}</c>'s(like log4j)
	/// </summary>
	/// <param name="severity">The message severity</param>
	/// <param name="fmt">The message format</param>
	/// <param name="content">The content to inline</param>
	public void Log(LogSeverity severity, string fmt, params object?[] content)
	{
		lock (Lock)
		{


			if (severity < MinimumSeverity)
				return;

			LogMessage message = new LogMessage
			{
				Severity = severity,
				LoggerName = name,
				LoggerModule = module,
				Message = Format(fmt, content),
				Time = DateTime.Now
			};


			string formatted = format;
			formatted = formatted.Replace("%severity%", message.Severity.ToString());
			formatted = formatted.Replace("%name%", message.LoggerName);
			formatted = formatted.Replace("%module%", message.LoggerModule);
			formatted = formatted.Replace("%message%", message.Message);
			formatted = formatted.Replace("%time%", message.Time.ToString("HH:mm:ss"));
			formatted = Thread.CurrentThread.Name != null
				? formatted.Replace("%thread%", Thread.CurrentThread.Name + "(" + Thread.CurrentThread.ManagedThreadId + ")")
				: formatted.Replace("%thread%", "Thread " + Thread.CurrentThread.ManagedThreadId);

			message.Formatted = formatted;

			Print(message);
			Messages.Add(message);
		}
	}

	private static void Print(LogMessage message)
	{
		foreach (var output in Outputs)
		{
			if(output == ConsoleOut)
				SetConsoleColours(message.Severity);
			
			Print(message, output);
			
			if(output == ConsoleOut)
				SetConsoleColours(message.Severity, true);
		}
	}

	private static void SetConsoleColours(LogSeverity severity, bool reset = false)
	{
		if (reset)
		{
			Console.ResetColor();
			return;
		}
		Console.ForegroundColor = severity switch
		{
			LogSeverity.Debug => ConsoleColor.Gray,
			LogSeverity.Info => ConsoleColor.White,
			LogSeverity.Warning => ConsoleColor.Yellow,
			LogSeverity.Error => ConsoleColor.Red,
			LogSeverity.Fatal => ConsoleColor.DarkRed,
			_ => Console.ForegroundColor
		};
	}

	private static readonly object WriteLock = new object();

	private static void Print(LogMessage message, TextWriter writer)
	{
		if (!Monitor.TryEnter(WriteLock, 20))
		{
			Thread.Sleep(Random.Shared.Next(0, 1000));
			ConsoleOut.WriteLine("Logging print reached race condition!");
			return;
		}

		writer.WriteLine(message.Formatted);
		writer.Flush();
		
		Monitor.Exit(WriteLock);
	}
}
