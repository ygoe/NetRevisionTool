using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Unclassified.Util;

namespace NetRevisionTool.VcsProviders
{
	internal class SubversionProvider : IVcsProvider
	{
		#region Private data

		private string svnExeName = Environment.OSVersion.Platform == PlatformID.Unix ? "svn" : "svn.exe";
		private string svnversionExeName = Environment.OSVersion.Platform == PlatformID.Unix ? "svnversion" : "svnversion.exe";
		private string svnExec;
		private string svnversionExec;

		#endregion Private data

		#region Overridden methods

		public override string ToString()
		{
			return "Subversion VCS provider";
		}

		#endregion Overridden methods

		#region IVcsProvider members

		public string Name
		{
			get { return "svn"; }
		}

		public bool CheckEnvironment()
		{
			Program.ShowDebugMessage("Subversion environment check…");
			svnExec = FindSvnBinary();
			if (svnExec == null)
			{
				Program.ShowDebugMessage("  svn executable not found.", 2);
				return false;
			}

			svnversionExec = Path.Combine(Path.GetDirectoryName(svnExec), svnversionExeName);
			if (!File.Exists(svnversionExec))
			{
				Program.ShowDebugMessage("  svnversion executable not found.", 2);
				return false;
			}
			return true;
		}

		public bool CheckDirectory(string path, out string rootPath)
		{
			// Scan directory tree upwards for the .svn directory
			Program.ShowDebugMessage("Checking directory tree for Subversion working directory…");
			do
			{
				Program.ShowDebugMessage("  Testing: " + path);
				if (Directory.Exists(Path.Combine(path, ".svn")))
				{
					Program.ShowDebugMessage("  Found " + path, 1);
					rootPath = path;
					return true;
				}
				path = Path.GetDirectoryName(path);
			}
			while (!string.IsNullOrEmpty(path));

			// Nothing found
			Program.ShowDebugMessage("Not a Subversion working directory.", 2);
			rootPath = null;
			return false;
		}

		public RevisionData ProcessDirectory(string path)
		{
			// Initialise data
			RevisionData data = new RevisionData
			{
				VcsProvider = this
			};

			// svn assumes case-sensitive path names on Windows, which is... bad.
			string fixedPath = PathUtil.GetExactPath(path);
			if (fixedPath != path)
			{
				Program.ShowDebugMessage("Corrected path to: " + fixedPath, 2);
			}
			path = fixedPath;

			// Get revision number
			Program.ShowDebugMessage("Executing: svnversion");
			Program.ShowDebugMessage("  WorkingDirectory: " + path);
			ProcessStartInfo psi = new ProcessStartInfo(svnversionExec);
			psi.WorkingDirectory = path;
			psi.RedirectStandardOutput = true;
			psi.UseShellExecute = false;
			Process p = Process.Start(psi);
			string line = null;
			while (!p.StandardOutput.EndOfStream)
			{
				line = p.StandardOutput.ReadLine();
				Program.ShowDebugMessage(line, 4);
				// Possible output:
				// 1234          Revision 1234
				// 1100:1234     Mixed revisions 1100 to 1234
				// 1234M         Revision 1234, modified
				// 1100:1234MP   Mixed revisions 1100 to 1234, modified and partial
				Match m = Regex.Match(line, @"^([0-9]+:)?([0-9]+)");
				if (m.Success)
				{
					data.IsMixed = m.Groups[1].Success;
					data.RevisionNumber = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
					break;
				}
			}
			p.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
			if (!p.WaitForExit(1000))
			{
				p.Kill();
			}

			if (data.RevisionNumber == 0) return data;   // Try no more

			Program.ShowDebugMessage("Executing: svn status");
			Program.ShowDebugMessage("  WorkingDirectory: " + path);
			psi = new ProcessStartInfo(svnExec, "status");
			psi.WorkingDirectory = path;
			psi.RedirectStandardOutput = true;
			psi.UseShellExecute = false;
			p = Process.Start(psi);
			line = null;
			while (!p.StandardOutput.EndOfStream)
			{
				line = p.StandardOutput.ReadLine();
				Program.ShowDebugMessage(line, 4);
			}
			if (!p.WaitForExit(1000))
			{
				p.Kill();
			}
			data.IsModified = !string.IsNullOrEmpty(line);

			Program.ShowDebugMessage("Executing: svn info --revision " + data.RevisionNumber);
			Program.ShowDebugMessage("  WorkingDirectory: " + path);
			psi = new ProcessStartInfo(svnExec, "info --revision " + data.RevisionNumber);
			psi.WorkingDirectory = path;
			psi.RedirectStandardOutput = true;
			psi.UseShellExecute = false;
			psi.StandardOutputEncoding = Encoding.Default;
			p = Process.Start(psi);
			line = null;
			string workingCopyRootPath = null;
			while (!p.StandardOutput.EndOfStream)
			{
				line = p.StandardOutput.ReadLine();
				Program.ShowDebugMessage(line, 4);
				// WARNING: This is the info about the commit that has been *last updated to* in the
				//          specified *subdirectory* of the working directory. The revision number
				//          printed here belongs to that commit, but does not necessarily match the
				//          revision number determined above by 'svnversion'.
				//          If you need consistent data on the commit other than the revision
				//          number, be sure to always update the entire working directory and set
				//          the VCS path to its root.
				Match m = Regex.Match(line, @"^Working Copy Root Path: (.+)");
				if (m.Success)
				{
					workingCopyRootPath = m.Groups[1].Value.Trim();
				}
				// Try to be smart and detect the branch from the relative path. This should work
				// fine if the standard SVN repository tree is used.
				m = Regex.Match(line, @"^Relative URL: \^(.+)");
				if (m.Success)
				{
					data.Branch = m.Groups[1].Value.Trim().TrimStart('/');
					if (data.Branch.StartsWith("branches/", StringComparison.Ordinal))
					{
						data.Branch = data.Branch.Substring(9);
					}

					// Cut off the current subdirectory
					if (workingCopyRootPath != null &&
						path.StartsWith(workingCopyRootPath, StringComparison.OrdinalIgnoreCase))
					{
						int subdirLength = path.Length - workingCopyRootPath.Length;
						data.Branch = data.Branch.Substring(0, data.Branch.Length - subdirLength);
					}
				}
				// Use "Repository Root" because "URL" is only the URL where the working directory
				// was checked out from. This can be a subdirectory of the repository if only a part
				// of it was checked out, like "/trunk" or a branch.
				m = Regex.Match(line, @"^Repository Root: (.+)");
				if (m.Success)
				{
					data.RepositoryUrl = m.Groups[1].Value.Trim();
				}
				m = Regex.Match(line, @"^Last Changed Author: (.+)");
				if (m.Success)
				{
					data.CommitterName = m.Groups[1].Value.Trim();
				}
				m = Regex.Match(line, @"^Last Changed Date: ([0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} [+-][0-9]{4})");
				if (m.Success)
				{
					data.CommitTime = DateTimeOffset.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
				}
			}
			if (!p.WaitForExit(1000))
			{
				p.Kill();
			}
			return data;
		}

		#endregion IVcsProvider members

		#region Private helper methods

		private string FindSvnBinary()
		{
			string svn = null;
			string keyPath;
			RegistryKey key;

			// Try the PATH environment variable
			if (svn == null)
			{
				string pathEnv = Environment.GetEnvironmentVariable("PATH");
				foreach (string _dir in pathEnv.Split(Path.PathSeparator))
				{
					string dir = _dir;
					if (dir.StartsWith("\"") && dir.EndsWith("\""))
					{
						// Strip quotes (no Path.PathSeparator supported in quoted directories though)
						dir = dir.Substring(1, dir.Length - 2);
					}
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Program.ShowDebugMessage("Found " + svnExeName + " in \"" + dir + "\" via %PATH%", 1);
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
			}

			// If TortoiseSVN has been installed with command-line binaries
			if (svn == null)
			{
				keyPath = @"Software\TortoiseSVN";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("Directory");
					if (loc is string)
					{
						svn = Path.Combine((string)loc, Path.Combine(@"bin", svnExeName));
						if (!File.Exists(svn))
						{
							svn = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + svnExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\Directory", 1);
						}
					}
				}
			}

			// Read registry uninstaller key
			if (svn == null)
			{
				keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("UninstallString");
					if (loc is string)
					{
						svn = Path.Combine(Path.GetDirectoryName((string)loc), svnExeName);
						if (!File.Exists(svn))
						{
							svn = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + svnExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\UninstallString", 1);
						}
					}
				}
			}
			if (svn == null)
			{
				keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						svn = Path.Combine((string)loc, svnExeName);
						if (!File.Exists(svn))
						{
							svn = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + svnExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Try 64-bit registry keys
			if (svn == null && Is64Bit)
			{
				keyPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("UninstallString");
					if (loc is string)
					{
						svn = Path.Combine(Path.GetDirectoryName((string)loc), svnExeName);
						if (!File.Exists(svn))
						{
							svn = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + svnExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\UninstallString", 1);
						}
					}
				}
			}
			if (svn == null && Is64Bit)
			{
				keyPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						svn = Path.Combine((string)loc, svnExeName);
						if (!File.Exists(svn))
						{
							svn = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + svnExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Search program files directory
			if (svn == null)
			{
				foreach (string dir in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "*subversion*"))
				{
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Program.ShowDebugMessage("Found " + svnExeName + " in \"" + dir + "\" via %ProgramFiles%\\*subversion*", 1);
						break;
					}
					svn = Path.Combine(dir, "bin", svnExeName);
					if (File.Exists(svn))
					{
						Program.ShowDebugMessage("Found " + svnExeName + " in \"" + dir + "\" via %ProgramFiles%\\*subversion*\\bin", 1);
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
			}

			// Try 32-bit program files directory
			if (svn == null && Is64Bit)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFilesX86(), "*subversion*"))
				{
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Program.ShowDebugMessage("Found " + svnExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\*subversion*", 1);
						break;
					}
					svn = Path.Combine(dir, "bin", svnExeName);
					if (File.Exists(svn))
					{
						Program.ShowDebugMessage("Found " + svnExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\*subversion*\\bin", 1);
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
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
					!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
			}
		}

		#endregion Private helper methods
	}
}
