using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NetRevisionTool.VcsProviders;
using Unclassified.Util;

namespace NetRevisionTool
{
	internal static class Program
	{
		#region Private data

		/// <summary>
		/// Indicates whether debug information should be displayed on the console.
		/// </summary>
		private static bool showDebugOutput;

		#endregion Private data

		#region Public properties

		/// <summary>
		/// Gets the pattern of tag names to match. Null indicates that all tags are accepted. An
		/// empty string indicates that no tag info shall be collected at all.
		/// </summary>
		public static string TagMatch { get; private set; }

		/// <summary>
		/// Gets a value indicating whether a leading "v" followed by a digit will be removed from
		/// the tag name.
		/// </summary>
		public static bool RemoveTagV { get; private set; }

		#endregion Public properties

		#region Main control flow

		/// <summary>
		/// Application entry point.
		/// </summary>
		/// <param name="args">Command line arguments. This parameter is not used.</param>
		/// <returns>Process return code.</returns>
		public static int Main(string[] args)
		{
			try
			{
				// Give us some room in the debugger (the window usually only has 80 columns here)
				if (Debugger.IsAttached)
				{
					Console.WindowWidth = Math.Max(Console.WindowWidth, Math.Min(120, Console.LargestWindowWidth));
				}

				// Fix console things
				Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
				ConsoleHelper.FixEncoding();
				ConsoleHelper.TryEnableUnicode();

				// Run the actual program
				MainWrapper();

				ConsoleHelper.WaitIfDebug();
				return (int)ExitCodes.NoError;
			}
			catch (ConsoleException ex)
			{
				return ConsoleHelper.ExitError(ex.Message, (int)ex.ExitCode);
			}
			catch (Exception ex)
			{
				ShowDebugMessage("Exception details:\n" + ex.ToString(), 3);
				return ConsoleHelper.ExitError("Unhandled exception: " + ex.Message + " (" + ex.GetType().Name + ")", 100);
			}
		}

		/// <summary>
		/// Wrapped main program, uses <see cref="ConsoleException"/> as return code in case of
		/// error and does not wait at the end.
		/// </summary>
		private static void MainWrapper()
		{
			CommandLineHelper cmdLine = new CommandLineHelper();
			var showHelpOption = cmdLine.RegisterOption("help").Alias("h", "?");
			var showVersionOption = cmdLine.RegisterOption("version").Alias("ver");
			var debugOption = cmdLine.RegisterOption("debug");
			var patchAssemblyInfoOption = cmdLine.RegisterOption("patch");
			var restorePatchedFilesOption = cmdLine.RegisterOption("restore");
			var simpleAttributeOption = cmdLine.RegisterOption("simple");
			var informationalAttributeOption = cmdLine.RegisterOption("info");
			var allAttributesOption = cmdLine.RegisterOption("all");
			var formatOption = cmdLine.RegisterOption("format", 1);
			var revisionOnlyOption = cmdLine.RegisterOption("revonly");
			var requireVcsOption = cmdLine.RegisterOption("require", 1);
			var rejectModifiedOption = cmdLine.RegisterOption("rejectmod").Alias("rejectmodified");
			var rejectMixedOption = cmdLine.RegisterOption("rejectmix").Alias("rejectmixed");
			var tagMatchOption = cmdLine.RegisterOption("tagmatch", 1);
			var removeTagVOption = cmdLine.RegisterOption("removetagv");
			var multiProjectOption = cmdLine.RegisterOption("multi");
			var scanRootOption = cmdLine.RegisterOption("root");
			var decodeRevisionOption = cmdLine.RegisterOption("decode", 1);
			var predictRevisionsOption = cmdLine.RegisterOption("predict");

			try
			{
				//cmdLine.ReadArgs(Environment.CommandLine, true);   // Alternative split method, should have the same result
				cmdLine.Parse();
				showDebugOutput = debugOption.IsSet;
				if (showDebugOutput)
				{
					ShowDebugMessage(
						"Command line: " +
							Environment.GetCommandLineArgs()
								.Select(s => "[" + s + "]")
								.Aggregate((a, b) => a + " " + b));
				}
			}
			catch (Exception ex)
			{
				throw new ConsoleException(ex.Message, ExitCodes.CmdLineError);
			}

			// Handle simple text output options
			if (showHelpOption.IsSet)
			{
				ShowHelp();
				return;
			}
			if (showVersionOption.IsSet)
			{
				ShowVersion();
				return;
			}

			// Check for environment variable from PowerShell build framework.
			// If psbuild has set this variable, it is using NetRevisionTool itself in multi-project
			// mode and pre/postbuild actions in individual projects should not do anything on their
			// own.
			if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SuppressNetRevisionTool")))
			{
				ShowDebugMessage("SuppressNetRevisionTool environment variable is set. Quitting…");
				return;
			}

			// Find all directories
			string path = GetWorkPath(cmdLine);
			string[] projectDirs = null;
			if (multiProjectOption.IsSet)
			{
				// Read solution file and collect all projects
				projectDirs = GetProjectsFromSolution(path);

				// From now on, work with the solution directory as default path to get the revision of
				if (Path.GetExtension(path).ToLowerInvariant() == ".sln")
				{
					path = Path.GetDirectoryName(path);
				}
			}
			else
			{
				if (!Directory.Exists(path))
					throw new ConsoleException("The specified project directory does not exist.", ExitCodes.FileNotFound);

				projectDirs = new[] { path };
			}

			// Restoring doesn't need more info, do it now
			if (restorePatchedFilesOption.IsSet)
			{
				// Restore AssemblyInfo file(s)
				foreach (string projectDir in projectDirs)
				{
					var aih = new AssemblyInfoHelper(projectDir, true);
					aih.RestoreFile();
				}
				return;
			}

			// Setup public data
			if (tagMatchOption.IsSet)
			{
				TagMatch = tagMatchOption.Value;
			}
			RemoveTagV = removeTagVOption.IsSet;

			// Analyse working directory
			RevisionData data = ProcessDirectory(path, scanRootOption.IsSet, requireVcsOption.Value);
			data.Normalize();

			// Check for required VCS
			if (requireVcsOption.IsSet)
			{
				if (data.VcsProvider == null ||
					!data.VcsProvider.Name.Equals(requireVcsOption.Value, StringComparison.OrdinalIgnoreCase))
				{
					throw new ConsoleException("Required VCS \"" + requireVcsOption.Value + "\" not present.", ExitCodes.RequiredVcs);
				}
			}

			// Check for reject modifications/mixed revisions
			if (rejectModifiedOption.IsSet && data.IsModified)
			{
				throw new ConsoleException("The working directory contains uncommitted modifications.", ExitCodes.RejectModified);
			}
			if (rejectMixedOption.IsSet && data.IsMixed)
			{
				throw new ConsoleException("The working directory contains mixed revisions.", ExitCodes.RejectMixed);
			}

			// Determine revision ID format, in case we need one here
			string format = null;
			if (formatOption.IsSet && !string.IsNullOrWhiteSpace(formatOption.Value))
			{
				// Take from command-line option
				format = formatOption.Value;
				ShowDebugMessage("Format specified: " + format);
			}
			else
			{
				// None or empty specified. Search in AssemblyInfo file(s) in the project(s)
				ShowDebugMessage("No format specified, searching AssemblyInfo source file.");
				AssemblyInfoHelper aih = null;
				foreach (string projectDir in projectDirs)
				{
					aih = new AssemblyInfoHelper(projectDir, false);
					if (aih.FileExists)
					{
						format = aih.GetRevisionFormat();
						if (format != null)
						{
							if (projectDirs.Length > 1)
							{
								ShowDebugMessage("Found format in project \"" + projectDir + "\".");
							}
							break;
						}
					}
					else
					{
						ShowDebugMessage("  AssemblyInfo source file not found.", 2);
					}
				}
				if (format != null)
				{
					ShowDebugMessage("Found format: " + format);
				}
			}

			if (format == null)
			{
				if (data.RevisionNumber > 0)
				{
					ShowDebugMessage("No format available, using default format for revision number.");
					format = "{revnum}";
				}
				else if (!string.IsNullOrEmpty(data.CommitHash) && !Regex.IsMatch(data.CommitHash, "^0+$"))
				{
					ShowDebugMessage("No format available, using default format for commit hash.");
					format = "{chash:8}";
				}
				else
				{
					ShowDebugMessage("No format available, using empty format.");
					format = "";
				}
			}

			if (decodeRevisionOption.IsSet)
			{
				// Decode specified revision ID
				RevisionFormat.ShowDecode(format, decodeRevisionOption.Value);
			}
			else if (predictRevisionsOption.IsSet)
			{
				// Predict next revision IDs
				RevisionFormat.PredictValue(format);
			}
			else if (patchAssemblyInfoOption.IsSet)
			{
				// Patch AssemblyInfo file(s)
				bool noAttrSet = !simpleAttributeOption.IsSet && !informationalAttributeOption.IsSet;
				bool simpleAttributes = simpleAttributeOption.IsSet || noAttrSet;
				bool informationalAttribute = informationalAttributeOption.IsSet || noAttrSet;

				foreach (string projectDir in projectDirs)
				{
					var aih = new AssemblyInfoHelper(projectDir, true);
					aih.PatchFile(format, data, simpleAttributes, informationalAttribute, revisionOnlyOption.IsSet);
				}
			}
			else
			{
				// Just display revision ID
				var rf = new RevisionFormat();
				rf.RevisionData = data;
				Console.WriteLine(rf.Resolve(format));
			}
		}

		#endregion Main control flow

		#region Debug output

		/// <summary>
		/// Shows a debug message on the console if debug messages are enabled.
		/// </summary>
		/// <param name="text">The text to display.</param>
		/// <param name="severity">0 (trace message), 1 (success), 2 (warning), 3 (error), 4 (raw output).</param>
		public static void ShowDebugMessage(string text, int severity = 0)
		{
			if (showDebugOutput)
			{
				var color = Console.ForegroundColor;
				switch (severity)
				{
					case 1:
						Console.ForegroundColor = ConsoleColor.DarkGreen;
						break;
					case 2:
						Console.ForegroundColor = ConsoleColor.DarkYellow;
						break;
					case 3:
						Console.ForegroundColor = ConsoleColor.DarkRed;
						break;
					case 4:
						Console.ForegroundColor = ConsoleColor.DarkCyan;
						break;
					default:
						Console.ForegroundColor = ConsoleColor.DarkGray;
						break;
				}
				Console.Error.WriteLine("» " + text.TrimEnd());
				Console.ForegroundColor = color;
			}
		}

		#endregion Debug output

		#region Directory handling

		/// <summary>
		/// Gets the work path from the command line or the current directory.
		/// </summary>
		/// <param name="cmdLine">Command line.</param>
		/// <returns>The work path.</returns>
		private static string GetWorkPath(CommandLineHelper cmdLine)
		{
			string path = Environment.CurrentDirectory;
			if (cmdLine.FreeArguments.Length > 0)
			{
				// Trim quotes, they can appear for mysterious reasons from VS/MSBuild
				path = cmdLine.FreeArguments[0].Trim(' ', '"');
				if (!Path.IsPathRooted(path))
				{
					path = Path.GetFullPath(path);
				}
				// Remove all trailing directory separators to make it safer
				path = path.TrimEnd('/', '\\');
			}
			else
			{
				ShowDebugMessage("No path specified, using current directory.");
			}
			ShowDebugMessage("Input directory: " + path);
			return path;
		}

		/// <summary>
		/// Processes the specified directory with a suitable VCS provider.
		/// </summary>
		/// <param name="path">The directory to process.</param>
		/// <param name="scanRoot">true if the working directory root shall be scanned instead of <paramref name="path"/>.</param>
		/// <param name="requiredVcs">The required VCS name, or null if any VCS is acceptable.</param>
		/// <returns>Data about the revision. If no provider was able to process the directory,
		///   dummy data is returned.</returns>
		private static RevisionData ProcessDirectory(string path, bool scanRoot, string requiredVcs)
		{
			RevisionData data = null;

			// Try to process the directory with all available VCS providers
			ShowDebugMessage("Processing directory…");
			foreach (IVcsProvider provider in GetVcsProviders())
			{
				ShowDebugMessage("Found VCS provider: " + provider);

				if (!string.IsNullOrEmpty(requiredVcs) &&
					!provider.Name.Equals(requiredVcs, StringComparison.OrdinalIgnoreCase))
				{
					ShowDebugMessage("Provider is not what is required, skipping.");
					continue;
				}

				if (provider.CheckEnvironment())
				{
					ShowDebugMessage("Provider can be executed in this environment.", 1);
					string rootPath;
					if (provider.CheckDirectory(path, out rootPath))
					{
						ShowDebugMessage("Provider can process this directory.", 1);
						if (scanRoot)
						{
							ShowDebugMessage("Root directory will be scanned.", 0);
							path = rootPath;
						}
						data = provider.ProcessDirectory(path);
						break;
					}
				}
			}

			if (data == null)
			{
				// No provider could process the directory, return dummy data
				ShowDebugMessage("No provider used, returning dummy data.", 2);
				data = new RevisionData
				{
					CommitHash = "0000000000000000000000000000000000000000",
					CommitTime = DateTimeOffset.Now,
					IsModified = false,
					RevisionNumber = 0
				};
			}

			data.DumpData();
			return data;
		}

		/// <summary>
		/// Reads a solution file and returns all contained projects.
		/// </summary>
		/// <param name="solutionFileName">The file name of the solution file to read.</param>
		/// <returns>An array of all projects in the solution.</returns>
		private static string[] GetProjectsFromSolution(string solutionFileName)
		{
			if (Directory.Exists(solutionFileName))
			{
				// We only have a directory, not a solution file.
				// Take a .sln file in this directory if there is only one.
				var fileNames = Directory.GetFiles(solutionFileName, "*.sln");
				if (fileNames.Length != 1)
					throw new ConsoleException("There are no or multiple solutions in this directory. Please specify the full .sln file name.", ExitCodes.NotASolution);
				solutionFileName = fileNames[0];
			}

			if (!solutionFileName.ToLowerInvariant().EndsWith(".sln"))
				throw new ConsoleException("Specified file name is not a solution.", ExitCodes.NotASolution);
			if (!File.Exists(solutionFileName))
				throw new ConsoleException("Specified solution file does not exist.", ExitCodes.FileNotFound);

			// Scan the solution file for projects and add them to the list
			string solutionDir = Path.GetDirectoryName(solutionFileName);
			List<string> projectDirs = new List<string>();
			ShowDebugMessage("Scanning solution file \"" + solutionFileName + "\" for projects…");
			using (StreamReader sr = new StreamReader(solutionFileName))
			{
				while (!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					Match match = Regex.Match(line, @"^Project\(.+\) = "".+"", ""(.+\.(?:csproj|vbproj))""");
					if (match.Success)
					{
						string projectPath = Path.Combine(solutionDir, match.Groups[1].Value);
						string projectDir = Path.GetDirectoryName(projectPath);
						ShowDebugMessage("  Found project in \"" + projectDir + "\".");
						projectDirs.Add(projectDir);
					}
				}
			}

			// Verify read data
			if (projectDirs.Count == 0)
				throw new ConsoleException("Specified solution file contains no projects.", ExitCodes.NoProjects);
			return projectDirs.ToArray();
		}

		#endregion Directory handling

		#region VCS providers support

		/// <summary>
		/// Searches all VCS providers implemented in the current assembly and creates an instance
		/// of each type.
		/// </summary>
		/// <returns>An array containing all created VCS provider instances.</returns>
		private static IVcsProvider[] GetVcsProviders()
		{
			List<IVcsProvider> providers = new List<IVcsProvider>();

			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				if (!type.IsInterface &&
					typeof(IVcsProvider).IsAssignableFrom(type))
				{
					IVcsProvider provider = (IVcsProvider)Activator.CreateInstance(type);
					providers.Add(provider);
				}
			}
			return providers.ToArray();
		}

		#endregion VCS providers support

		#region Simple text output options

		/// <summary>
		/// Prints the contents of the embedded file Manual.txt to the console.
		/// </summary>
		private static void ShowHelp()
		{
			// Get embedded compressed resource stream and decompress data
			using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetRevisionTool.Manual.txt.gz"))
			using (GZipStream zip = new GZipStream(resourceStream, CompressionMode.Decompress, true))
			using (MemoryStream manualStream = new MemoryStream())
			{
				zip.CopyTo(manualStream);
				manualStream.Seek(0, SeekOrigin.Begin);

				using (var sr = new StreamReader(manualStream))
				{
					string content = sr.ReadToEnd();
					ConsoleHelper.WriteWrappedFormatted(content, FormatText, true);
				}
			}

			//Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NetRevisionTool.Manual.txt");
			//using (var sr = new StreamReader(resStream))
			//{
			//    string content = sr.ReadToEnd();
			//    ConsoleHelper.WriteWrappedFormatted(content, FormatText, true);
			//}
		}

		/// <summary>
		/// Shows the application version and copyright.
		/// </summary>
		private static void ShowVersion()
		{
			string productName = "";
			string productVersion = "";
			string productDescription = "";
			string productCopyright = "";

			object[] customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productName = ((AssemblyProductAttribute)customAttributes[0]).Product;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productVersion = ((AssemblyFileVersionAttribute)customAttributes[0]).Version;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productDescription = ((AssemblyDescriptionAttribute)customAttributes[0]).Description;
			}
			customAttributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
			if (customAttributes != null && customAttributes.Length > 0)
			{
				productCopyright = ((AssemblyCopyrightAttribute)customAttributes[0]).Copyright;
			}

			Console.WriteLine(productName + " " + productVersion);
			ConsoleHelper.WriteWrapped(productDescription);
			ConsoleHelper.WriteWrapped(productCopyright);
		}

		private static bool FormatText(char ch)
		{
			switch (ch)
			{
				case '♦': Console.ForegroundColor = ConsoleColor.White; return false;
				case '♣': Console.ForegroundColor = ConsoleColor.Gray; return false;
			}
			return true;
		}

		#endregion Simple text output options
	}
}
