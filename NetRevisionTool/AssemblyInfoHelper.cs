using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NetRevisionTool
{
	internal class AssemblyInfoHelper
	{
		#region Private data

		private string fileName;
		private string attrStart;
		private string attrEnd;
		private string[] lines;
		private string revisionFormat;

		#endregion Private data

		#region Constructor

		/// <summary>
		/// Initialises a new instance of the <see cref="AssemblyInfoHelper"/> class.
		/// </summary>
		/// <param name="path">The project directory to operate in.</param>
		/// <param name="throwOnMissingFile">Indicates whether an exception is thrown if the AssemblyInfo file was not found.</param>
		public AssemblyInfoHelper(string path, bool throwOnMissingFile)
		{
			FindAssemblyInfoFile(path);
			if (fileName == null && throwOnMissingFile)
			{
				throw new ConsoleException("AssemblyInfo file not found in \"" + path + "\".", ExitCodes.FileNotFound);
			}
			if (fileName != null)
			{
				AnalyseFile();
			}
		}

		#endregion Constructor

		#region Public properties

		/// <summary>
		/// Gets a value indicating whether the AssemblyInfo file was found.
		/// </summary>
		public bool FileExists
		{
			get { return fileName != null; }
		}

		#endregion Public properties

		#region Public methods

		/// <summary>
		/// Patches the file and injects the revision data.
		/// </summary>
		/// <param name="fallbackFormat">The fallback format if none is defined in the file.</param>
		/// <param name="data">The revision data to use for resolving formats.</param>
		/// <param name="simpleAttributes">Indicates whether simple version attributes are processed.</param>
		/// <param name="informationalAttribute">Indicates whether the AssemblyInformationalVersion attribute is processed.</param>
		/// <param name="revOnly">Indicates whether only the last number is replaced by the revision number.</param>
		public void PatchFile(string fallbackFormat, RevisionData data, bool simpleAttributes, bool informationalAttribute, bool revOnly)
		{
			Program.ShowDebugMessage("Patching file \"" + fileName + "\"…");
			string backupFileName = CreateBackup();

			// Read the backup file. If the backup was created earlier, it still contains the source
			// file while the regular file may have been resolved but not restored before. By
			// reading the former source file, we get the correct result and can heal the situation
			// with the next restore run.
			ReadFileLines(backupFileName);

			// Find the revision format for this file
			revisionFormat = FindRevisionFormat();
			if (revisionFormat == null)
			{
				// Nothing defined in this file. Use whatever was specified on the command line or
				// found in any of the projects in the solution.
				revisionFormat = fallbackFormat;
			}
			else
			{
				Program.ShowDebugMessage("The file defines a revision format: " + revisionFormat);
			}
			if (revisionFormat == null)
			{
				// If we don't have a revision format, there's nothing to replace in this file.
				return;
			}
			var rf = new RevisionFormat();
			rf.RevisionData = data;

			// Process all lines in the file
			ResolveAllLines(rf, simpleAttributes, informationalAttribute, revOnly);

			// Write back all lines to the file
			WriteFileLines();
		}

		/// <summary>
		/// Restores the file from a backup.
		/// </summary>
		public void RestoreFile()
		{
			RestoreBackup();
		}

		/// <summary>
		/// Gets the revision format as defined in the file.
		/// </summary>
		/// <returns>The revision format, or null if none is defined.</returns>
		public string GetRevisionFormat()
		{
			// Prefer the backup file if it exists
			string fileToRead = GetBackupFileName();
			if (!File.Exists(fileToRead))
			{
				fileToRead = fileName;
			}

			ReadFileLines(fileToRead);
			return FindRevisionFormat();
		}

		#endregion Public methods

		#region Analysis

		/// <summary>
		/// Analyses the file and detects language-specific values.
		/// </summary>
		private void AnalyseFile()
		{
			switch (Path.GetExtension(fileName).ToLower())
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
					throw new ConsoleException("Unsupported AssemblyInfo file extension: " + Path.GetExtension(fileName).ToLower(), ExitCodes.UnsupportedLanguage);
			}
		}

		/// <summary>
		/// Finds the AssemblyInfo file in the specified directory.
		/// </summary>
		/// <param name="path">The directory to search in.</param>
		private void FindAssemblyInfoFile(string path)
		{
			fileName = Path.Combine(path, "Properties", "AssemblyInfo.cs");
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "My Project", "AssemblyInfo.vb");
			}
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "AssemblyInfo.cs");
			}
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "AssemblyInfo.vb");
			}
			if (!File.Exists(fileName))
			{
				fileName = null;
			}
		}

		/// <summary>
		/// Finds the specified revision format from the file.
		/// </summary>
		/// <returns>The revision format, or null if it was not found.</returns>
		private string FindRevisionFormat()
		{
			foreach (string line in lines)
			{
				Match match = Regex.Match(
					line,
					@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyInformationalVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
					RegexOptions.IgnoreCase);
				if (match.Success)
				{
					return match.Groups[2].Value;
				}
			}
			return null;
		}

		#endregion Analysis

		#region File access

		/// <summary>
		/// Gets the name of the backup file for the current file.
		/// </summary>
		/// <returns>The backup file name.</returns>
		private string GetBackupFileName()
		{
			return fileName + ".bak";
		}

		/// <summary>
		/// Creates a backup of the file if it does not already exist.
		/// </summary>
		/// <returns>The name of the backup file.</returns>
		private string CreateBackup()
		{
			string backup = GetBackupFileName();
			if (!File.Exists(backup))
			{
				File.Copy(fileName, backup);
				Program.ShowDebugMessage("Created backup to \"" + Path.GetFileName(backup) + "\".");
			}
			else
			{
				Program.ShowDebugMessage("Backup \"" + Path.GetFileName(backup) + "\" already exists, skipping.", 2);
			}
			return backup;
		}

		/// <summary>
		/// Restores the file from a backup if it exists.
		/// </summary>
		private void RestoreBackup()
		{
			string backup = GetBackupFileName();
			if (File.Exists(backup))
			{
				File.Delete(fileName);
				File.Move(backup, fileName);
				Program.ShowDebugMessage("Restored backup file \"" + backup + "\".");
			}
			else
			{
				Program.ShowDebugMessage("Backup file \"" + backup + "\" does not exist, skipping.", 2);
			}
		}

		/// <summary>
		/// Reads all lines of a file.
		/// </summary>
		/// <param name="readFileName">The name of the file to read.</param>
		private void ReadFileLines(string readFileName)
		{
			List<string> linesList = new List<string>();
			using (StreamReader sr = new StreamReader(readFileName, Encoding.Default, true))
			{
				// Read all lines from source file into lines buffer
				while (!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					linesList.Add(line);
				}
			}
			lines = linesList.ToArray();
		}

		/// <summary>
		/// Writes all lines into the file.
		/// </summary>
		private void WriteFileLines()
		{
			using (StreamWriter sw = new StreamWriter(fileName, false, Encoding.UTF8))
			{
				foreach (string line in lines)
				{
					sw.WriteLine(line);
				}
			}
		}

		#endregion File access

		#region Resolving

		/// <summary>
		/// Resolves all attributes in the file.
		/// </summary>
		/// <param name="rf">The revision format for the file.</param>
		/// <param name="simpleAttributes">Indicates whether simple version attributes are processed.</param>
		/// <param name="informationalAttribute">Indicates whether the AssemblyInformationalVersion attribute is processed.</param>
		/// <param name="revOnly">Indicates whether only the last number is replaced by the revision number.</param>
		private void ResolveAllLines(RevisionFormat rf, bool simpleAttributes, bool informationalAttribute, bool revOnly)
		{
			// Preparing a truncated dotted-numeric version if we may need it
			string truncVersion = null;
			if (simpleAttributes && !revOnly)
			{
				string revisionId = rf.Resolve(revisionFormat);
				truncVersion = Regex.Replace(revisionId, @"[^0-9.].*$", "");
				if (!truncVersion.Contains("."))
					throw new ConsoleException("Revision ID cannot be truncated to dotted-numeric: " + revisionId, ExitCodes.NoNumericVersion);
			}

			// Checking the revision number if we may need it
			int revNum = rf.RevisionData.RevisionNumber;
			if (revOnly)
			{
				if (revNum == 0)
				{
					Program.ShowDebugMessage("Revision number is 0. Did you really mean to use /revonly?", 2);
				}
				if (revNum > UInt16.MaxValue)
				{
					throw new ConsoleException("Revision number " + revNum + " is greater than " + UInt16.MaxValue + " and cannot be used here. Consider using the offset option.", ExitCodes.RevNumTooLarge);
				}
			}

			// Process all lines
			Match match;
			for (int i = 0; i < lines.Length; i++)
			{
				if (revOnly)
				{
					// Replace the fourth part of AssemblyVersion and AssemblyFileVersion with the
					// revision number. If less parts are currently specified, zeros are inserted.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyVersion\s*\(\s*""[0-9]+)(\.[0-9]+)?(\.[0-9]+)?(\.[0-9]+)?(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] =
							match.Groups[1].Value +
							(match.Groups[2].Success ? match.Groups[2].Value : ".0") +
							(match.Groups[3].Success ? match.Groups[3].Value : ".0") +
							"." + revNum +
							match.Groups[5].Value;
						Program.ShowDebugMessage("Found AssemblyVersion attribute for revision number only.", 1);
					}
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyFileVersion\s*\(\s*""[0-9]+)(\.[0-9]+)?(\.[0-9]+)?(\.[0-9]+)?(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] =
							match.Groups[1].Value +
							(match.Groups[2].Success ? match.Groups[2].Value : ".0") +
							(match.Groups[3].Success ? match.Groups[3].Value : ".0") +
							"." + revNum +
							match.Groups[5].Value;
						Program.ShowDebugMessage("Found AssemblyFileVersion attribute for revision number only.", 1);
					}
				}

				if (simpleAttributes && !revOnly)
				{
					// Replace the entire version in AssemblyVersion and AssemblyFileVersion with
					// the truncated dotted-numeric version.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] = match.Groups[1].Value + truncVersion + match.Groups[3].Value;
						Program.ShowDebugMessage("Found AssemblyVersion attribute.", 1);
						Program.ShowDebugMessage("  Replaced \"" + match.Groups[2].Value + "\" with \"" + truncVersion + "\".");
					}
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyFileVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] = match.Groups[1].Value + truncVersion + match.Groups[3].Value;
						Program.ShowDebugMessage("Found AssemblyFileVersion attribute.", 1);
						Program.ShowDebugMessage("  Replaced \"" + match.Groups[2].Value + "\" with \"" + truncVersion + "\".");
					}
				}

				if (informationalAttribute && !revOnly)
				{
					// Replace the entire value of AssemblyInformationalVersion with the resolved
					// string of what was already there. This ignores the fallback format, should
					// one be given on the command line.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyInformationalVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						string revisionId = rf.Resolve(match.Groups[2].Value);
						lines[i] = match.Groups[1].Value + revisionId + match.Groups[3].Value;
						Program.ShowDebugMessage("Found AssemblyInformationalVersion attribute.", 1);
						Program.ShowDebugMessage("  Replaced \"" + match.Groups[2].Value + "\" with \"" + revisionId + "\".");
					}
				}
			}
		}

		#endregion Resolving
	}
}
