using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Unclassified;
using System.Diagnostics;
using Microsoft.Win32;

namespace SvnRevisionTool
{
	class Program
	{
		static bool multiProjectMode;
		static bool onlyInformationalVersion;
		static string repositoryUrl;
		static int revision;
		static bool mixedRevisions;
		static bool debugOutput;
		static bool isModified;
		static DateTimeOffset revTime;
		static DateTimeOffset buildTime;

		static int Main(string[] args)
		{
			CommandLineParser clp = new CommandLineParser();
			clp.AddKnownOption("a", "assembly-info");
			clp.AddKnownOption("b", "test-bmin");
			clp.AddKnownOption("f", "format", true);
			clp.AddKnownOption("h", "help");
			clp.AddKnownOption("i", "ignore-missing");
			clp.AddKnownOption("m", "multi-project");
			clp.AddKnownOption("r", "revision");
			clp.AddKnownOption("s", "restore");
			clp.AddKnownOption("v", "version");
			clp.AddKnownOption("B", "de-bmin");
			clp.AddKnownOption("D", "debug");
			clp.AddKnownOption("I", "only-infver");
			clp.AddKnownOption("M", "stop-if-modified");
			clp.AddKnownOption("X", "de-xmin");

			debugOutput = clp.IsOptionSet("D");

			if (clp.IsOptionSet("h") || clp.IsOptionSet("v"))
			{
				HandleHelp(clp.IsOptionSet("h"));
				return 0;
			}
			if (clp.IsOptionSet("X"))
			{
				int baseYear = int.Parse(clp.GetArgument(0));
				string xmin = clp.GetArgument(1).Trim().ToLowerInvariant();
				DateTime time = DehexMinutes(baseYear, xmin);
				Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
				Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
				return 0;
			}
			if (clp.IsOptionSet("B"))
			{
				int baseYear = int.Parse(clp.GetArgument(0));
				string bmin = clp.GetArgument(1).Trim().ToLowerInvariant();
				DateTime time = Debase36Minutes(baseYear, bmin);
				Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm") + " UTC");
				Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm K"));
				return 0;
			}
			if (clp.IsOptionSet("b"))
			{
				int baseYear = int.Parse(clp.GetArgument(0));
				revTime = DateTime.UtcNow;
				long ticks10min = TimeSpan.FromMinutes(10).Ticks;
				revTime = new DateTime(revTime.Ticks / ticks10min * ticks10min, DateTimeKind.Utc);
				for (int i = 0; i < 10; i++)
				{
					Console.WriteLine(revTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm K") + " = " + Base36Minutes(baseYear, 1));
					revTime = revTime.AddMinutes(10);
				}
				return 0;
			}

			buildTime = DateTimeOffset.Now;

			bool patchAssemblyInfoFile = clp.IsOptionSet("a");
			bool restoreAssemblyInfoFile = clp.IsOptionSet("s");
			multiProjectMode = clp.IsOptionSet("m");
			bool showRevision = clp.IsOptionSet("r");
			bool stopIfModified = clp.IsOptionSet("M");
			bool ignoreMissing = clp.IsOptionSet("i");
			onlyInformationalVersion = clp.IsOptionSet("I");
			string path = clp.GetArgument(0);
			string customFormat = "{!}{commit}";
			if (clp.IsOptionSet("f"))
				customFormat = clp.GetOptionValue("f");

			if (!patchAssemblyInfoFile && !restoreAssemblyInfoFile && !showRevision)
				showRevision = true;   // Default action
			if (string.IsNullOrEmpty(path))
				path = ".";
			if (debugOutput)
				Console.Error.WriteLine("Working on path " + path);

			bool hasProcessed = false;

			List<string> projectDirs = new List<string>();
			if (multiProjectMode)
			{
				// Treat the single directory argument as the solution file name
				if (!path.ToLowerInvariant().EndsWith(".sln"))
				{
					Console.Error.WriteLine("Error: Specified file name is invalid. Only *.sln files accepted in\nmulti-project mode.");
					return 1;
				}
				if (!File.Exists(path))
				{
					Console.Error.WriteLine("Error: Specified solution file does not exist.");
					return 1;
				}

				// Scan the solution file for projects and add them to the list
				string solutionDir = Path.GetDirectoryName(path);
				using (StreamReader sr = new StreamReader(path))
				{
					while (!sr.EndOfStream)
					{
						string line = sr.ReadLine();
						Match m = Regex.Match(line, @"^Project\(.+\) = "".+"", ""(.+\.(?:csproj|vbproj))""");
						if (m.Success)
						{
							string projectPath = Path.Combine(solutionDir, m.Groups[1].Value);
							string projectDir = Path.GetDirectoryName(projectPath);
							if (debugOutput)
								Console.Error.WriteLine("Add project in " + projectDir);
							projectDirs.Add(projectDir);
						}
					}
				}

				if (projectDirs.Count == 0)
				{
					Console.Error.WriteLine("Error: Specified solution file contains no projects.");
					return 1;
				}

				// From now on, work with the solution directory as default path to get the revision of
				path = solutionDir;
			}

			if (patchAssemblyInfoFile)
			{
				if (!hasProcessed)
				{
					ProcessDirectory(path, ignoreMissing, true);
					if (revision == 0)
					{
						if (ignoreMissing)
						{
							revTime = DateTimeOffset.Now;
						}
						else
						{
							Console.Error.WriteLine("Error: Not a Subversion working directory.");
							return 1;
						}
					}
					hasProcessed = true;
				}

				if (stopIfModified && isModified)
				{
					Console.Error.WriteLine("Error: Subversion working directory contains uncommited changes, stop requested by option.");
					return 1;
				}

				if (multiProjectMode)
				{
					bool success = true;
					foreach (string projectDir in projectDirs)
					{
						success &= PatchAssemblyInfoFile(projectDir);
					}
					if (!success)
					{
						return 1;
					}
				}
				else
				{
					if (!PatchAssemblyInfoFile(path))
					{
						return 1;
					}
				}
			}
			if (restoreAssemblyInfoFile)
			{
				if (multiProjectMode)
				{
					bool success = true;
					foreach (string projectDir in projectDirs)
					{
						success &= RestoreAssemblyInfoFile(projectDir);
					}
					if (!success)
					{
						return 1;
					}
				}
				else
				{
					if (!RestoreAssemblyInfoFile(path))
					{
						return 1;
					}
				}
			}
			if (showRevision)
			{
				if (!hasProcessed)
				{
					ProcessDirectory(path, ignoreMissing, true);
					if (revision == 0)
					{
						if (ignoreMissing)
						{
							revTime = DateTimeOffset.Now;
						}
						else
						{
							Console.Error.WriteLine("Error: Not a Subversion working directory.");
							return 1;
						}
					}
					hasProcessed = true;
				}

				Console.WriteLine(ResolveFormat(customFormat));
			}

			return 0;
		}

		static bool PatchAssemblyInfoFile(string path)
		{
			string aiFilename = Path.Combine(path, "Properties\\AssemblyInfo.cs");
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "My Project\\AssemblyInfo.vb");
			}
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "AssemblyInfo.cs");
			}
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "AssemblyInfo.vb");
			}
			if (!File.Exists(aiFilename))
			{
				if (multiProjectMode)
					Console.Error.WriteLine("Project: " + path);
				Console.Error.WriteLine("Error: Assembly info file not found.");
				return false;
			}
			string aiBackup = aiFilename + ".bak";
			if (!File.Exists(aiBackup))
			{
				File.Copy(aiFilename, aiBackup);
				if (debugOutput)
					Console.Error.WriteLine("Created backup to " + Path.GetFileName(aiBackup));
			}

			if (debugOutput)
				Console.Error.WriteLine("Patching " + Path.GetFileName(aiFilename) + "...");

			string attrStart = null, attrEnd = null;
			switch (Path.GetExtension(aiFilename).ToLower())
			{
				case ".cs":
					attrStart = "[";
					attrEnd = "]";
					break;
				case ".vb":
					attrStart = "<";
					attrEnd = ">";
					break;
				default:
					if (multiProjectMode)
						Console.Error.WriteLine("Project: " + path);
					Console.Error.WriteLine("Logic error: invalid AssemblyInfo file extension: " + Path.GetExtension(aiFilename).ToLower());
					return false;
			}

			StreamReader sr = new StreamReader(aiBackup, Encoding.Default, true);
			sr.Peek();
			StreamWriter sw = new StreamWriter(aiFilename, false, sr.CurrentEncoding);

			while (!sr.EndOfStream)
			{
				string line = sr.ReadLine();

				Match m;
				if (!onlyInformationalVersion)
				{
					m = Regex.Match(
						line,
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyVersion\s*\(\s*""[0-9]+\.[0-9]+\.[0-9]+\.)[0-9]+(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (m.Success)
					{
						int rev = revision;
						if (rev > UInt16.MaxValue)
							rev = UInt16.MaxValue;   // TODO: Add options of different behaviour
						line = m.Groups[1].Value + rev + m.Groups[2].Value;
						if (debugOutput)
							Console.Error.WriteLine("Found AssemblyVersion attribute");
					}
					m = Regex.Match(
						line,
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyFileVersion\s*\(\s*""[0-9]+\.[0-9]+\.[0-9]+\.)[0-9]+(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (m.Success)
					{
						string rev = revision.ToString();
						line = m.Groups[1].Value + rev + m.Groups[2].Value;
						if (debugOutput)
							Console.Error.WriteLine("Found AssemblyFileVersion attribute");
					}
				}
				m = Regex.Match(
					line,
					@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyInformationalVersion\s*\(\s*"")(.*)(""\s*\)\s*\" + attrEnd + @".*)$",
					RegexOptions.IgnoreCase);
				if (m.Success)
				{
					string value = m.Groups[2].Value;
					value = ResolveFormat(value);
					line = m.Groups[1].Value + value + m.Groups[3].Value;
					if (debugOutput)
						Console.Error.WriteLine("Found AssemblyInformationalVersion attribute");
				}

				sw.WriteLine(line);
			}

			sr.Close();
			sw.Close();
			if (debugOutput)
				Console.Error.WriteLine("Patched " + Path.GetFileName(aiFilename));
			return true;
		}

		static bool RestoreAssemblyInfoFile(string path)
		{
			string aiFilename = Path.Combine(path, "Properties\\AssemblyInfo.cs");
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "My Project\\AssemblyInfo.vb");
			}
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "AssemblyInfo.cs");
			}
			if (!File.Exists(aiFilename))
			{
				aiFilename = Path.Combine(path, "AssemblyInfo.vb");
			}
			if (!File.Exists(aiFilename))
			{
				Console.Error.WriteLine("Error: Assembly info file not found.");
				return false;
			}
			string aiBackup = aiFilename + ".bak";
			if (File.Exists(aiBackup))
			{
				File.Delete(aiFilename);
				File.Move(aiBackup, aiFilename);
				if (debugOutput)
					Console.Error.WriteLine("Restored " + Path.GetFileName(aiBackup));
			}
			else
			{
				if (debugOutput)
					Console.Error.WriteLine(Path.GetFileName(aiBackup) + " does not exist");
				return false;
			}
			return true;
		}

		static string ResolveFormat(string value)
		{
			value = value.Replace("{!}", isModified ? "!" : "");
			value = value.Replace("{commit}", revision.ToString());
			value = value.Replace("{url}", repositoryUrl);
			
			value = value.Replace("{date}", revTime.ToString("yyyyMMdd"));
			value = value.Replace("{date:ymd-}", revTime.ToString("yyyy-MM-dd"));
			value = value.Replace("{time}", revTime.ToString("HHmmss"));
			value = value.Replace("{time:hms}", revTime.ToString("HHmmss"));
			value = value.Replace("{time:hms:}", revTime.ToString("HH:mm:ss"));
			value = value.Replace("{time:hm}", revTime.ToString("HHmm"));
			value = value.Replace("{time:hm:}", revTime.ToString("HH:mm"));
			value = value.Replace("{time:h}", revTime.ToString("HH"));
			value = value.Replace("{time:o}", revTime.ToString("%K"));

			value = value.Replace("{utdate}", revTime.ToUniversalTime().ToString("yyyyMMdd"));
			value = value.Replace("{utdate:ymd-}", revTime.ToUniversalTime().ToString("yyyy-MM-dd"));
			value = value.Replace("{uttime}", revTime.ToUniversalTime().ToString("HHmmss"));
			value = value.Replace("{uttime:hms}", revTime.ToUniversalTime().ToString("HHmmss"));
			value = value.Replace("{uttime:hms:}", revTime.ToUniversalTime().ToString("HH:mm:ss"));
			value = value.Replace("{uttime:hm}", revTime.ToUniversalTime().ToString("HHmm"));
			value = value.Replace("{uttime:hm:}", revTime.ToUniversalTime().ToString("HH:mm"));
			value = value.Replace("{uttime:h}", revTime.ToUniversalTime().ToString("HH"));
			value = value.Replace("{uttime:o}", revTime.ToUniversalTime().ToString("%K"));

			value = value.Replace("{builddate}", buildTime.ToString("yyyyMMdd"));
			value = value.Replace("{builddate:ymd-}", buildTime.ToString("yyyy-MM-dd"));
			value = value.Replace("{buildtime}", buildTime.ToString("HHmmss"));
			value = value.Replace("{buildtime:hms}", buildTime.ToString("HHmmss"));
			value = value.Replace("{buildtime:hms:}", buildTime.ToString("HH:mm:ss"));
			value = value.Replace("{buildtime:hm}", buildTime.ToString("HHmm"));
			value = value.Replace("{buildtime:hm:}", buildTime.ToString("HH:mm"));
			value = value.Replace("{buildtime:h}", buildTime.ToString("HH"));
			value = value.Replace("{buildtime:o}", buildTime.ToString("%K"));

			value = value.Replace("{utbuilddate}", buildTime.ToUniversalTime().ToString("yyyyMMdd"));
			value = value.Replace("{utbuilddate:ymd-}", buildTime.ToUniversalTime().ToString("yyyy-MM-dd"));
			value = value.Replace("{utbuildtime}", buildTime.ToUniversalTime().ToString("HHmmss"));
			value = value.Replace("{utbuildtime:hms}", buildTime.ToUniversalTime().ToString("HHmmss"));
			value = value.Replace("{utbuildtime:hms:}", buildTime.ToUniversalTime().ToString("HH:mm:ss"));
			value = value.Replace("{utbuildtime:hm}", buildTime.ToUniversalTime().ToString("HHmm"));
			value = value.Replace("{utbuildtime:hm:}", buildTime.ToUniversalTime().ToString("HH:mm"));
			value = value.Replace("{utbuildtime:h}", buildTime.ToUniversalTime().ToString("HH"));
			value = value.Replace("{utbuildtime:o}", buildTime.ToUniversalTime().ToString("%K"));

			value = Regex.Replace(value, @"\{!:(.*?)\}", delegate(Match m) { return isModified ? m.Groups[1].Value : ""; });
			
			value = Regex.Replace(value, @"\{xmin:([0-9]{4})\}", delegate(Match m) { return HexMinutes(int.Parse(m.Groups[1].Value), 1); });
			value = Regex.Replace(value, @"\{xmin:([0-9]{4}):([0-9]{1,2})\}", delegate(Match m) { return HexMinutes(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)); });
			value = Regex.Replace(value, @"\{bmin:([0-9]{4})\}", delegate(Match m) { return Base36Minutes(int.Parse(m.Groups[1].Value), 1); });
			value = Regex.Replace(value, @"\{bmin:([0-9]{4}):([0-9]{1,2})\}", delegate(Match m) { return Base36Minutes(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)); });
			
			value = Regex.Replace(value, @"\{Xmin:([0-9]{4})\}", delegate(Match m) { return HexMinutes(int.Parse(m.Groups[1].Value), 1).ToUpperInvariant(); });
			value = Regex.Replace(value, @"\{Xmin:([0-9]{4}):([0-9]{1,2})\}", delegate(Match m) { return HexMinutes(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)).ToUpperInvariant(); });
			value = Regex.Replace(value, @"\{Bmin:([0-9]{4})\}", delegate(Match m) { return Base36Minutes(int.Parse(m.Groups[1].Value), 1).ToUpperInvariant(); });
			value = Regex.Replace(value, @"\{Bmin:([0-9]{4}):([0-9]{1,2})\}", delegate(Match m) { return Base36Minutes(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)).ToUpperInvariant(); });
			return value;
		}

		static string HexMinutes(int baseYear, int length)
		{
			int min = (int) (revTime.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalMinutes;
			if (min < 0)
				return "-" + (-min).ToString("x" + length);
			else
				return min.ToString("x" + length);
		}

		static DateTime DehexMinutes(int baseYear, string xmin)
		{
			bool negative = false;
			if (xmin.StartsWith("-"))
			{
				negative = true;
				xmin = xmin.Substring(1);
			}
			int min = int.Parse(xmin, System.Globalization.NumberStyles.AllowHexSpecifier);
			return new DateTime(baseYear, 1, 1).AddMinutes(negative ? -min : min);
		}

		static string Base36Minutes(int baseYear, int length)
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

		static DateTime Debase36Minutes(int baseYear, string bmin)
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
			return new DateTime(baseYear, 1, 1).AddMinutes(negative ? -min : min);
		}

		static void HandleHelp(bool showHelp)
		{
			string productName = "";
			string productVersion = "";
			string productDescription = "";
			string productCopyright = "";
			string appFilename = "";

			object[] customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productName = ((AssemblyProductAttribute) customAttributes[0]).Product;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productVersion = ((AssemblyFileVersionAttribute) customAttributes[0]).Version;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productDescription = ((AssemblyDescriptionAttribute) customAttributes[0]).Description;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productCopyright = ((AssemblyCopyrightAttribute) customAttributes[0]).Copyright;
			}

			Console.WriteLine(productName + " " + productVersion);
			Console.WriteLine(productCopyright);

			if (showHelp)
			{
				appFilename = Path.GetFileName(Assembly.GetEntryAssembly().Location);

				Console.WriteLine();
				Console.WriteLine(productDescription);
				Console.WriteLine();
				Console.WriteLine("Usage:");
				Console.WriteLine("  " + appFilename + " [options] [<path>]");
				Console.WriteLine();
				Console.WriteLine("Options:");
				Console.WriteLine("  -a, --assembly-info");
				Console.WriteLine("                  Patches the AssemblyInfo.cs/vb file's version specifications");   // 78c
				Console.WriteLine("  -b, --test-bmin <year>");
				Console.WriteLine("                  Prints the current and next few bmin values");
				Console.WriteLine("  -f, --format    Prints the revision string with the specified format");
				Console.WriteLine("  -h, --help      Shows this help page");
				Console.WriteLine("  -i, --ignore-missing");
				Console.WriteLine("                  Does nothing if this is not a Subversion working directory");
				Console.WriteLine("  -m, --multi-project");
				Console.WriteLine("                  Processes all projects in the solution specified by <path>");
				Console.WriteLine("  -r, --revision  Shows the working copy revision");
				Console.WriteLine("  -s, --restore   Restores AssemblyInfo.cs/vb file from backup");
				Console.WriteLine("  -v, --version   Shows product version");
				Console.WriteLine("  -B, --de-bmin <year> <bmin>");
				Console.WriteLine("                  Decodes a bmin value to UTC and local time");
				Console.WriteLine("  -D, --debug     Shows debug information");
				Console.WriteLine("  -I, --only-infver");
				Console.WriteLine("                  Only changes the AssemblyInformationalVersion attribute,");
				Console.WriteLine("                  not AssemblyVersion or AssemblyFileVersion.");
				Console.WriteLine("  -M, --stop-if-modified");
				Console.WriteLine("                  Stops if the working copy contains uncommited changes");
				Console.WriteLine("  -X, --de-xmin <year> <xmin>");
				Console.WriteLine("                  Decodes an xmin value to UTC and local time");
				Console.WriteLine();
				Console.WriteLine("The path parameter is used to locate the project's AssemblyInfo files that");
				Console.WriteLine("shall be updated. Therefore the path parameter must be the project's root");
				Console.WriteLine("directory. If no path is specified, the current working directory is used.");
				Console.WriteLine("The Subversion hidden directory is searched up the path from there.");
				Console.WriteLine();
				Console.WriteLine("To use SvnRevisionTool in a C# or VB.NET project to update AssemblyInfo.cs/vb,");   // 78c
				Console.WriteLine("use these commands as pre- and post-build events in the project settings:");
				Console.WriteLine("  Pre:    $(ProjectDir)SvnRevisionTool -a \"$(ProjectDir)\"");
				Console.WriteLine("  Post:   $(ProjectDir)SvnRevisionTool -s \"$(ProjectDir)\"");
				Console.WriteLine("IMPORTANT: Set the post-build event to be executed always to ensure the");
				Console.WriteLine("           modified source file is always restored correctly.");
				Console.WriteLine();
				Console.WriteLine("The following assembly attributes are supported:");
				Console.WriteLine("  AssemblyVersion(\"0.0.0.<rev>\")");
				Console.WriteLine("  AssemblyFileVersion(\"0.0.0.<rev>\")");
				Console.WriteLine("  AssemblyInformationalVersion(\"... {commit} {date} {time} ...\")");
				Console.WriteLine();
				Console.WriteLine("The following placeholders are supported:");
				Console.WriteLine("  {!}                Prints ! if modified");
				Console.WriteLine("  {!:<text>}         Prints <text> if modified");
				Console.WriteLine("  {commit}           Prints commit revision number");
				Console.WriteLine("  {url}              Prints repository URL");
				Console.WriteLine("  {date}             Prints commit date as YYYYMMDD");
				Console.WriteLine("  {date:<format>}    Prints commit data with format:");
				Console.WriteLine("                       ymd-   YYYY-MM-DD");
				Console.WriteLine("  {time}             Prints commit time as HHMMSS");
				Console.WriteLine("  {time:<format>}    Prints commit time with format:");
				Console.WriteLine("                       hms    HHMMSS");
				Console.WriteLine("                       hms:   HH:MM:SS");
				Console.WriteLine("                       hm     HHMM");
				Console.WriteLine("                       hm:    HH:MM");
				Console.WriteLine("                       h      HH");
				Console.WriteLine("                       o      Time zone like +0100");
				Console.WriteLine("  {builddate...}     Prints build date, see {date} for formats");
				Console.WriteLine("  {buildtime...}     Prints build time, see {time} for formats");
				Console.WriteLine("  {xmin:<year>}");
				Console.WriteLine("  {xmin:<year>:<length>}");
				Console.WriteLine("                     Prints minutes since year <year>, length <length>");
				Console.WriteLine("                     in hexadecimal format");
				Console.WriteLine("  {bmin:<year>}");
				Console.WriteLine("  {bmin:<year>:<length>}");
				Console.WriteLine("                     Prints 10-minutes since year <year>, length <length>");
				Console.WriteLine("                     in base36 format");
				Console.WriteLine();
				Console.WriteLine("The following placeholders variants are available:");
				Console.WriteLine("  {utdate} and {uttime} print commit date/time in UTC.");
				Console.WriteLine("  {utbuilddate} and {utbuildtime} print build date/time in UTC.");
				Console.WriteLine("  {Xmin} and {Bmin} use uppercase letters.");
				Console.WriteLine();
				Console.WriteLine("Example for C#:");
				Console.WriteLine("  [assembly: AssemblyVersion(\"3.0.1.0\")]");
				Console.WriteLine("  [assembly: AssemblyInformationalVersion(\"MyApp r{commit}/{date}\")]");
				Console.WriteLine("Will be replaced with:");
				Console.WriteLine("  [assembly: AssemblyVersion(\"3.0.1.241\")]");
				Console.WriteLine("  [assembly: AssemblyInformationalVersion(\"MyApp r241/20130430\")]");
				Console.WriteLine();
				Console.WriteLine("Example for batch file:");
				Console.WriteLine("  SvnRevisionTool --format \"SET revid={commit}\" > %temp%\\revid.cmd");
				Console.WriteLine("Will generate file revid.cmd with contents:");
				Console.WriteLine("  SET revid=241");
				Console.WriteLine("(Write to a file outside the SVN working directory to avoid false modify.)");
				Console.WriteLine();
				Console.WriteLine("SVN client formats supported are 1.4, 1.5 and 1.6.");
				// TODO:
				//Console.WriteLine("Git (msysGit) must be installed in one of %ProgramFiles*%\\Git*.");
				//Console.WriteLine();
				Console.WriteLine("ATTENTION: Be sure not to have the AssemblyInfo file opened in the IDE while");
				Console.WriteLine("           building the project, or the version modifications will be ignored");
				Console.WriteLine("           by the compiler.");
			}
		}

		static void ProcessDirectory(string path, bool silent, bool findRoot)
		{
			bool directFile = false;
			string fileVersion = null;

			if (debugOutput)
				Console.Error.WriteLine("Processing directory " + path);

			if (findRoot)
			{
				// Scan parent directories to see the complete working copy
				path = Path.GetFullPath(path);
				// Remove a trailing slash, it would cause an error
				if (path.EndsWith(Path.DirectorySeparatorChar.ToString()))
				{
					path = path.Substring(0, path.Length - 1);
				}
				string parentPath = Path.GetDirectoryName(path);
				bool didFind = false;
				while (true)
				{
					if (string.IsNullOrEmpty(parentPath))
						break;
					if (Directory.Exists(Path.Combine(path, ".svn")))
						didFind = true;
					if (!Directory.Exists(Path.Combine(parentPath, ".svn")) && didFind)
						break;
					path = parentPath;
					parentPath = Path.GetDirectoryName(path);
				}
				if (debugOutput)
					Console.Error.WriteLine("Working copy root directory: " + path);
			}
			
			string svnSubdir = Path.Combine(path, ".svn");
			if (Directory.Exists(svnSubdir))
			{
				try
				{
					using (StreamReader sr = new StreamReader(Path.Combine(svnSubdir, "entries")))
					{
						string data = sr.ReadToEnd();

						if (data.StartsWith("<?xml"))
						{
							Console.Error.WriteLine("Error: Old XML file format is not implemented.");
						}
						else
						{
							// Text format
							int lineBreak = data.IndexOf('\n');
							if (lineBreak == -1)
							{
								lineBreak = data.Length;
							}
							fileVersion = data.Substring(0, lineBreak);
							if (data.Length > lineBreak + 1)
							{
								data = data.Substring(lineBreak + 1);
							}
							else
							{
								data = "";
							}

							if (fileVersion == "8")
							{
								// Format 1.4.x
								directFile = true;
								if (debugOutput)
									Console.Error.WriteLine("SVN 1.4 client format, directly reading the files.");
							}
							else if (fileVersion == "9")
							{
								// Format 1.5.x
								directFile = true;
								if (debugOutput)
									Console.Error.WriteLine("SVN 1.5 client format, directly reading the files.");
							}
							else if (fileVersion == "10")
							{
								// Format 1.6.x
								directFile = true;
								if (debugOutput)
									Console.Error.WriteLine("SVN 1.6 client format, directly reading the files.");
							}
							else if (fileVersion == "12")
							{
								// Format 1.7.x
								if (debugOutput)
									Console.Error.WriteLine("SVN 1.7 client format, using installed SVN command-line client.");
							}
							else
							{
								Console.Error.WriteLine("Warning: Unrecognised SVN version in " + Path.Combine(path, ".svn\\entries") + ": " + fileVersion);
							}

							if (directFile)
							{
								// Source: http://gerardlee.wordpress.com/2009/11/10/python-script-use-to-change-svn-working-copy-format/
								//  0 name
								//  1 kind
								//  2 revision
								//  3 url
								//  4 repos
								//  5 schedule
								//  6 text-time
								//  7 checksum
								//  8 committed-date
								//  9 committed-rev
								// 10 last-author
								// 11 has-props
								// 12 has-prop-mods
								// 13 cachable-props
								// 14 present-props
								// 15 conflict-old
								// 16 conflict-new
								// 17 conflict-wrk
								// 18 prop-reject-file
								// 19 copied
								// 20 copyfrom-url
								// 21 copyfrom-rev
								// 22 deleted
								// 23 absent
								// 24 incomplete
								// 25 uuid
								// 26 lock-token
								// 27 lock-owner
								// 28 lock-comment
								// 29 lock-creation-date
								// 30 changelist (since 1.5)
								// 31 keep-local (since 1.5)
								// 32 working-size
								// 33 depth (since 1.5)
								// 34 tree-conflicts (since 1.6)
								// 35 file-external (since 1.6)

								string[] sections = data.Split(new string[] { "\f\n" }, StringSplitOptions.None);

								foreach (string sectionData in sections)
								{
									string[] lines = sectionData.Split('\n');
									string filename = Path.Combine(path, lines[0]);

									if (lines.Length > 9)
									{
										// Some sections are shorter and contain no revision data, skip them
										string revLine = lines[9].Trim();
										if (revLine.Length > 0)
										{
											int entryRevision = int.Parse(revLine);
											if (!mixedRevisions && revision != 0 && entryRevision != revision)
											{
												mixedRevisions = true;
												if (debugOutput)
													Console.Error.WriteLine("Found mixed revision numbers (" + entryRevision + " for " + filename + " instead of " + revision + ") - no more notifications");
											}
											if (entryRevision > revision)
											{
												revision = entryRevision;
												if (debugOutput)
													Console.Error.WriteLine("Found newer revision number " + entryRevision + " for " + filename);
											}
										}
									}

									if (lines.Length > 3)
									{
										if (repositoryUrl == null && lines[3].Trim().Length > 0)
											repositoryUrl = lines[3].Trim();
									}

									if (lines.Length > 5)
									{
										if (lines[5].Trim() == "add")
										{
											isModified = true;
											if (debugOutput)
												Console.Error.WriteLine("Uncommitted added file: " + filename);
										}
										if (lines[5].Trim() == "delete")
										{
											isModified = true;
											if (debugOutput)
												Console.Error.WriteLine("Uncommitted deleted file: " + filename);
										}
									}

									if (lines.Length > 7)
									{
										// Compare file time
										DateTime svnFileTime;
										if (!isModified && DateTime.TryParse(lines[6], out svnFileTime))
										{
											FileInfo fi = new FileInfo(filename);
											if (fi.LastWriteTime.ToString("s") != svnFileTime.ToString("s"))
											{
												if (debugOutput)
													Console.Error.WriteLine("File is newer than SVN base: " + filename);

												// Compare checksum
												string svnChecksum = lines[7];
												FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read);
												byte[] fileChecksumBytes = System.Security.Cryptography.MD5.Create().ComputeHash(fs);
												fs.Close();
												string fileChecksum = "";
												foreach (byte b in fileChecksumBytes)
												{
													fileChecksum += b.ToString("x2");
												}
												if (fileChecksum != svnChecksum)
												{
													isModified = true;

													if (debugOutput)
														Console.Error.WriteLine("File is modified from SVN base: " + filename);
												}
											}
										}
									}
								}
							}
						}
					}
				}
				catch (IOException ex)
				{
					Console.Error.WriteLine("Warning: Cannot read file " + Path.Combine(path, ".svn\\entries") + ". " +
						ex.GetType().Name + ": " + ex.Message);
				}
				catch (FormatException)
				{
					Console.Error.WriteLine("Warning: File format error in " + Path.Combine(path, ".svn\\entries") + ".");
				}

				if (directFile)
				{
					foreach (string subdir in Directory.GetDirectories(path))
					{
						ProcessDirectory(subdir, silent, false);
					}
				}
			}
			else
			{
				if (debugOutput)
					Console.Error.WriteLine("Warning: .svn subdirectory does not exist, nothing to do here.");
			}

			if (!directFile)
			{
				if (fileVersion == "12")
				{
					// First try to use the installed git binary
					string svnExec = FindSvnBinary();
					if (svnExec == null)
					{
						if (!silent)
						{
							Console.Error.WriteLine("Error: Subversion not installed or not found.");
						}
						return;
					}

					string svnVersionExec = Regex.Replace(svnExec, @"\\svn.exe$", @"\svnversion.exe");
					if (!File.Exists(svnVersionExec))
					{
						Console.Error.WriteLine("Error: svnversion.exe not found.");
						return;
					}

					ProcessStartInfo psi = new ProcessStartInfo(svnVersionExec);
					psi.WorkingDirectory = path;
					psi.RedirectStandardOutput = true;
					if (silent)
					{
						psi.RedirectStandardError = true;
					}
					psi.UseShellExecute = false;
					Process p = Process.Start(psi);
					string line = null;
					while (!p.StandardOutput.EndOfStream)
					{
						line = p.StandardOutput.ReadLine();
						Match m = Regex.Match(line, @"^([0-9]+)");
						if (m.Success)
						{
							revision = int.Parse(m.Groups[1].Value);
							// TODO: revTime = DateTimeOffset.Parse(m.Groups[2].Value);
							revTime = DateTimeOffset.MinValue;
							p.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
							break;
						}
					}
					if (!p.WaitForExit(1000))
					{
						p.Kill();
					}

					if (revision == 0) return;   // Try no more

					psi = new ProcessStartInfo(svnExec, "status");
					psi.WorkingDirectory = path;
					psi.RedirectStandardOutput = true;
					if (silent)
					{
						psi.RedirectStandardError = true;
					}
					psi.UseShellExecute = false;
					p = Process.Start(psi);
					line = null;
					while (!p.StandardOutput.EndOfStream)
					{
						line = p.StandardOutput.ReadLine();
					}
					if (!p.WaitForExit(1000))
					{
						p.Kill();
					}
					isModified = !string.IsNullOrEmpty(line);
				}
			}
		}

		private static string FindSvnBinary()
		{
			string svn = null;
			RegistryKey key;

			// If TortoiseSVN has been installed with command-line binaries
			key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\TortoiseSVN");
			if (key != null)
			{
				object loc = key.GetValue("Directory");
				if (loc is string)
				{
					svn = Path.Combine((string) loc, @"bin\svn.exe");
					if (!File.Exists(svn)) svn = null;
				}
			}

			// Read registry uninstaller key
			if (svn == null)
			{
				key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client");
				if (key != null)
				{
					object loc = key.GetValue("UninstallString");
					if (loc is string)
					{
						svn = Path.Combine(Path.GetDirectoryName((string) loc), @"svn.exe");
						if (!File.Exists(svn)) svn = null;
					}
				}
			}
			if (svn == null)
			{
				key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1");
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						svn = Path.Combine((string) loc, @"svn.exe");
						if (!File.Exists(svn)) svn = null;
					}
				}
			}

			// Try 64-bit registry key
			if (svn == null && Is64Bit)
			{
				key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client");
				if (key != null)
				{
					object loc = key.GetValue("UninstallString");
					if (loc is string)
					{
						svn = Path.Combine(Path.GetDirectoryName((string) loc), @"svn.exe");
						if (!File.Exists(svn)) svn = null;
					}
				}
			}
			if (svn == null && Is64Bit)
			{
				key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1");
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						svn = Path.Combine((string) loc, @"svn.exe");
						if (!File.Exists(svn)) svn = null;
					}
				}
			}

			// Search program files directory
			if (svn == null)
			{
				foreach (string dir in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "*subversion*"))
				{
					svn = Path.Combine(dir, @"svn.exe");
					if (!File.Exists(svn)) svn = null;
				}
			}

			// Try 32-bit program files directory
			if (svn == null && Is64Bit)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFilesX86(), "*subversion*"))
				{
					svn = Path.Combine(dir, @"svn.exe");
					if (!File.Exists(svn)) svn = null;
				}
			}

			return svn;
		}

		private static string ProgramFilesX86()
		{
			if (Is64Bit)
			{
				return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			}
			return Environment.GetEnvironmentVariable("ProgramFiles");
		}

		private static bool Is64Bit
		{
			get
			{
				return IntPtr.Size == 8 ||
					!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
			}
		}
	}
}
