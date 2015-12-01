using System;
using System.Linq;

namespace NetRevisionTool
{
	internal class ConsoleException : Exception
	{
		public ConsoleException(string message, ExitCodes exitCode)
			: base(message)
		{
			ExitCode = exitCode;
		}

		public ExitCodes ExitCode { get; private set; }
	}
}
