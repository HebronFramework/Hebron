using System;

namespace Hebron
{
	public static class Logger
	{
		public static Action<string> LogFunction = Console.Write;

		public static void Log(string data)
		{
			if (LogFunction != null)
			{
				LogFunction(data);
			}
		}

		public static void LogLine(string data)
		{
			if (LogFunction != null)
			{
				LogFunction(data + Environment.NewLine);
			}
		}

		public static void Warning(string message, params object[] args)
		{
			LogLine(string.Format("Warning: " + message, args));
		}

		public static void Info(string message, params object[] args)
		{
			LogLine(string.Format("Info: " + message, args));
		}
	}
}
