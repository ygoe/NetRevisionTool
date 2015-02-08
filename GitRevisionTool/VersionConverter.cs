using System;
using System.Linq;
using Unclassified;

namespace RevisionToolShared
{
	internal class VersionConverter
	{
		#region Hex minutes

		public static string HexMinutes(DateTimeOffset revTime, int baseYear, int length)
		{
			int min = (int) (revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes;
			if (min < 0)
				return "-" + (-min).ToString("x" + length);
			else
				return min.ToString("x" + length);
		}

		public static DateTime DehexMinutes(int baseYear, string xmin)
		{
			bool negative = false;
			if (xmin.StartsWith("-"))
			{
				negative = true;
				xmin = xmin.Substring(1);
			}
			int min = int.Parse(xmin, System.Globalization.NumberStyles.AllowHexSpecifier);
			try
			{
				return new DateTime(baseYear, 1, 1).AddMinutes(negative ? -min : min);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		public static int ShowDehexMinutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			string xmin = clp.GetArgument(1).Trim().ToLowerInvariant();
			DateTime time = DehexMinutes(baseYear, xmin);
			if (time == DateTime.MinValue)
			{
				Console.Error.WriteLine("Invalid xmin value.");
				return 0;
			}
			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
			return 0;
		}

		public static int ShowTestHexMinutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			DateTimeOffset revTime = DateTime.UtcNow;
			long ticks1min = TimeSpan.FromMinutes(1).Ticks;
			revTime = new DateTime(revTime.Ticks / ticks1min * ticks1min, DateTimeKind.Utc);
			for (int i = 0; i < 10; i++)
			{
				Console.WriteLine(revTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm K") + " = " + HexMinutes(revTime, baseYear, 1));
				revTime = revTime.AddMinutes(1);
			}
			return 0;
		}

		#endregion Hex minutes

		#region Base28 minutes

		/// <summary>
		/// List of digits for the base28 representation. This uses the digits 0 through 9, and
		/// all characters from a-z that are no vowels and have a low chance of being confused
		/// with digits or each other when hand-written. Omitting vowels prevents generating
		/// profane words.
		/// </summary>
		static private char[] base28Chars = new char[]
		{
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'b', 'c', 'd', 'f', 'g', 'h', 'j',
			'k', 'm', 'n', 'p', 'q', 'r', 't', 'v', 'w', 'x', 'y'
		};

		public static string Base28Minutes(DateTimeOffset revTime, int baseYear, int length)
		{
			int min = (int) ((revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes / 20);
			bool negative = false;
			if (min < 0)
			{
				negative = true;
				min = -min;
			}
			string s = "";
			while (min > 0)
			{
				int digit = min % 28;
				min = min / 28;
				s = base28Chars[digit] + s;
			}
			return (negative ? "-" : "") + s.PadLeft(length, '0');
		}

		public static DateTime Debase28Minutes(int baseYear, string bmin)
		{
			bool negative = false;
			if (bmin.StartsWith("-"))
			{
				negative = true;
				bmin = bmin.Substring(1);
			}
			int min = 0;
			while (bmin.Length > 0)
			{
				int digit = Array.IndexOf(base28Chars, bmin[0]);
				if (digit == -1)
				{
					return DateTime.MinValue;
				}
				min = min * 28 + digit;
				bmin = bmin.Substring(1);
			}
			min *= 20;
			try
			{
				return new DateTime(baseYear, 1, 1).AddMinutes(negative ? -min : min);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		public static int ShowDebase28Minutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			string bmin = clp.GetArgument(1).Trim().ToLowerInvariant();
			DateTime time = Debase28Minutes(baseYear, bmin);
			if (time == DateTime.MinValue)
			{
				Console.Error.WriteLine("Invalid bmin value.");
				return 0;
			}
			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
			return 0;
		}

		public static int ShowTestBase28Minutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			DateTimeOffset revTime = DateTime.UtcNow;
			long ticks20min = TimeSpan.FromMinutes(20).Ticks;
			revTime = new DateTime(revTime.Ticks / ticks20min * ticks20min, DateTimeKind.Utc);
			for (int i = 0; i < 10; i++)
			{
				Console.WriteLine(revTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm K") + " = " + Base28Minutes(revTime, baseYear, 1));
				revTime = revTime.AddMinutes(20);
			}
			return 0;
		}

		#endregion Base28 minutes

		#region Base36 minutes

		public static string Base36Minutes(DateTimeOffset revTime, int baseYear, int length)
		{
			int min = (int) ((revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes / 10);
			bool negative = false;
			if (min < 0)
			{
				negative = true;
				min = -min;
			}
			string s = "";
			while (min > 0)
			{
				int digit = min % 36;
				min = min / 36;
				if (digit < 10)
					s = digit + s;
				else
					s = Char.ConvertFromUtf32('a' + (digit - 10)) + s;
			}
			return (negative ? "-" : "") + s.PadLeft(length, '0');
		}

		public static DateTime Debase36Minutes(int baseYear, string bmin)
		{
			bool negative = false;
			if (bmin.StartsWith("-"))
			{
				negative = true;
				bmin = bmin.Substring(1);
			}
			int min = 0;
			while (bmin.Length > 0)
			{
				int digit;
				if (bmin[0] <= '9')
					digit = bmin[0] - '0';
				else
					digit = bmin[0] - 'a' + 10;
				min = min * 36 + digit;
				bmin = bmin.Substring(1);
			}
			min *= 10;
			try
			{
				return new DateTime(baseYear, 1, 1).AddMinutes(negative ? -min : min);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		public static int ShowDebase36Minutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			string bmin = clp.GetArgument(1).Trim().ToLowerInvariant();
			DateTime time = Debase36Minutes(baseYear, bmin);
			if (time == DateTime.MinValue)
			{
				Console.Error.WriteLine("Invalid b36min value.");
				return 0;
			}
			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
			return 0;
		}

		public static int ShowTestBase36Minutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			DateTimeOffset revTime = DateTime.UtcNow;
			long ticks10min = TimeSpan.FromMinutes(10).Ticks;
			revTime = new DateTime(revTime.Ticks / ticks10min * ticks10min, DateTimeKind.Utc);
			for (int i = 0; i < 10; i++)
			{
				Console.WriteLine(revTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm K") + " = " + Base36Minutes(revTime, baseYear, 1));
				revTime = revTime.AddMinutes(10);
			}
			return 0;
		}

		#endregion Base36 minutes

		#region Dec minutes

		public static string DecMinutes(DateTimeOffset revTime, int baseYear)
		{
			int min = (int) ((revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes / 15);
			int minutesPerDay = 24 * 4;
			if (min < 0)
				return "0.0";
			else
				return (min / minutesPerDay).ToString() + "." + (min % minutesPerDay).ToString();
		}

		public static DateTime DedecMinutes(int baseYear, string dmin)
		{
			string[] parts = dmin.Split('.');
			if (parts.Length != 2) return DateTime.MinValue;
			int days, time;
			if (!int.TryParse(parts[0], out days)) return DateTime.MinValue;
			if (!int.TryParse(parts[1], out time)) return DateTime.MinValue;
			if (days < 0 || days >= UInt16.MaxValue) return DateTime.MinValue;
			if (time < 0 || time >= 96) return DateTime.MinValue;
			try
			{
				return new DateTime(baseYear, 1, 1).AddDays(days).AddMinutes(time * 15);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		public static int ShowDedecMinutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			string dmin = clp.GetArgument(1).Trim();
			DateTime time = DedecMinutes(baseYear, dmin);
			if (time == DateTime.MinValue)
			{
				Console.Error.WriteLine("Invalid dmin value.");
				return 0;
			}
			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
			return 0;
		}

		#endregion Dec minutes

		#region Dec 2 minutes

		public static string Dec2Minutes(DateTimeOffset revTime, int baseYear)
		{
			int min = (int) ((revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes / 2);
			int minutesPerDay = 24 * 30;
			if (min < 0)
				return "0.0";
			else
				return (min / minutesPerDay).ToString() + "." + (min % minutesPerDay).ToString();
		}

		public static DateTime Dedec2Minutes(int baseYear, string dmin)
		{
			string[] parts = dmin.Split('.');
			if (parts.Length != 2) return DateTime.MinValue;
			int days, time;
			if (!int.TryParse(parts[0], out days)) return DateTime.MinValue;
			if (!int.TryParse(parts[1], out time)) return DateTime.MinValue;
			if (days < 0 || days >= UInt16.MaxValue) return DateTime.MinValue;
			if (time < 0 || time >= 720) return DateTime.MinValue;
			try
			{
				return new DateTime(baseYear, 1, 1).AddDays(days).AddMinutes(time * 2);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		public static int ShowDedec2Minutes(CommandLineParser clp)
		{
			int baseYear;
			if (!int.TryParse(clp.GetArgument(0), out baseYear))
			{
				Console.Error.WriteLine("Invalid argument: Base year expected");
				return 1;
			}
			string dmin = clp.GetArgument(1).Trim();
			DateTime time = Dedec2Minutes(baseYear, dmin);
			if (time == DateTime.MinValue)
			{
				Console.Error.WriteLine("Invalid dmin2 value.");
				return 0;
			}
			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
			return 0;
		}

		#endregion Dec 2 minutes

		// NOTE: Future enhancements:
		// {d10sec}, 10 second time of day, 4 digits
		// {d2sec}, 2 second time of day, 5 digits up to ~43000 (max. allowed 65535)
	}
}
