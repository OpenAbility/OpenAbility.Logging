using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenAbility.Logging;

/// <summary>
/// The meat and bones of the system. It provides loggers and logging!
/// </summary>
public class Logger
{
	private static readonly Dictionary<string, Logger> Loggers = new Dictionary<string, Logger>();
	private static readonly List<TextWriter> DefaultOutputs = new List<TextWriter>();
	private static readonly string GlobalFormat = "[%severity%/%thread%] (%name%): %message%";
	/// <summary>
	/// The directory in which log files are put, set *before* InitializeSystem
	/// </summary>
	public static readonly string LogDirectory = Path.Join(Directory.GetCurrentDirectory(), "Logs");
	
	/// <summary>
	/// Create any needed things
	/// </summary>
	static Logger() {
		DefaultOutputs.Add(Console.Out);
		DefaultOutputs.Add(CreateLogFile("latest.log"));
		DefaultOutputs.Add(
			CreateLogFile($"{DateTime.Now.Day}-{DateTime.Now.Month}-{DateTime.Now.Year} {DateTime.Now.Hour}-{DateTime.Now.Minute}.log"));
	}
	
	/// <summary>
	/// Get a logger by name
	/// </summary>
	/// <param name="name">The name of the logger</param>
	/// <returns>The logger with that name</returns>
	public static Logger Get(string name)
	{
		if (Loggers.TryGetValue(name, out Logger? existing))
			return existing;

		Logger logger = new Logger(name);
		Loggers[name] = logger;
		return logger;
	}
	
	/// <summary>
	/// Get a logger by type
	/// </summary>
	/// <typeparam name="T">The type to get the logger from</typeparam>
	/// <returns>The logger with the same name as the type</returns>
	public static Logger Get<T>()
	{
		return Get(typeof(T).Name);
	}
	
	private static TextWriter CreateLogFile(string name)
	{
		if (!Directory.Exists(LogDirectory))
			Directory.CreateDirectory(LogDirectory);
		return File.CreateText(Path.Combine(LogDirectory, name));
	}

	private readonly string format;
	private readonly string name;
	private readonly List<TextWriter> outputs;
	private Logger(string name)
	{
		this.format = GlobalFormat;
		this.name = name;
		outputs = new List<TextWriter>(DefaultOutputs);
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
		Log(LogSeverity.Fatal, "{}:\n{}", Format(fmt, content), Environment.StackTrace);
	}

	private string Format(string fmt, params object?[] content)
	{
		Regex replaceRegex = new Regex(Regex.Escape("{}"), RegexOptions.Compiled | RegexOptions.Multiline);
		
		foreach (object? value in content)
		{
			string replacement = String.Empty;
			if (value != null)
				replacement = value.ToString() ?? String.Empty;
			fmt = replaceRegex.Replace(fmt, replacement, 1);
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
		LogMessage message = new LogMessage
		{
			Severity = severity,
			LoggerName = name,
			Message = Format(fmt, content)
		};
		Print(message);
	}

	private void Print(LogMessage message)
	{
		foreach (var output in outputs)
		{
			if(output == Console.Out)
				SetConsoleColours(message.Severity);
			
			Print(message, output);
			
			if(output == Console.Out)
				SetConsoleColours(message.Severity, true);
		}
	}

	private void SetConsoleColours(LogSeverity severity, bool reset = false)
	{
		if (reset)
		{
			Console.ResetColor();
			return;
		}
		switch (severity)
		{
			case LogSeverity.Debug:
				Console.ForegroundColor = ConsoleColor.Gray;
				break;
			case LogSeverity.Info:
				Console.ForegroundColor = ConsoleColor.White;
				break;
			case LogSeverity.Warning:
				Console.ForegroundColor = ConsoleColor.Yellow;
				break;
			case LogSeverity.Error:
				Console.ForegroundColor = ConsoleColor.Red;
				break;
			case LogSeverity.Fatal:
				Console.ForegroundColor = ConsoleColor.DarkRed;
				break;
		}
	}

	private void Print(LogMessage message, TextWriter writer)
	{
		string formatted = format;
		formatted = formatted.Replace("%severity%", message.Severity.ToString());
		formatted = formatted.Replace("%name%", message.LoggerName);
		formatted = formatted.Replace("%message%", message.Message);
		formatted = Thread.CurrentThread.Name != null ? 
			formatted.Replace("%thread%", Thread.CurrentThread.Name + "(" + Thread.CurrentThread.ManagedThreadId + ")") : 
			formatted.Replace("%thread%", "Thread " + Thread.CurrentThread.ManagedThreadId);

		writer.WriteLine(formatted);
		writer.Flush();
	}
}