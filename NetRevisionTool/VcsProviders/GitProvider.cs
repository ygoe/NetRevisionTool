using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NetRevisionTool.VcsProviders
{
	internal class GitProvider : IVcsProvider
	{
		#region Private data

		private string gitExeName = Environment.OSVersion.Platform == PlatformID.Unix ? "git" : "git.exe";
		private string gitExec;

		#endregion Private data

		#region Overridden methods

		public override string ToString()
		{
			return "Git VCS provider";
		}

		#endregion Overridden methods

		#region IVcsProvider members

		public string Name
		{
			get { return "git"; }
		}

		public bool CheckEnvironment()
		{
			Program.ShowDebugMessage("Git environment check…");
			gitExec = FindGitBinary();
			if (gitExec == null)
			{
				Program.ShowDebugMessage("  git executable not found.", 2);
				return false;
			}
			return true;
		}

		public bool CheckDirectory(string path, out string rootPath)
		{
			// Scan directory tree upwards for the .git directory
			Program.ShowDebugMessage("Checking directory tree for Git working directory…");
			do
			{
				Program.ShowDebugMessage("  Testing: " + path);
				if (Directory.Exists(Path.Combine(path, ".git")))
				{
					Program.ShowDebugMessage("  Found " + path, 1);
					rootPath = path;
					return true;
				}
				path = Path.GetDirectoryName(path);
			}
			while (!string.IsNullOrEmpty(path));

			// Nothing found
			Program.ShowDebugMessage("Not a Git working directory.", 2);
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

			// Queries the commit hash and time from the latest log entry
			string gitLogFormat = "%H %ci %ai%n%cN%n%cE%n%aN%n%aE";
			Program.ShowDebugMessage("Executing: git log -n 1 --format=format:\"" + gitLogFormat + "\"");
			Program.ShowDebugMessage("  WorkingDirectory: " + path);
			ProcessStartInfo psi = new ProcessStartInfo(gitExec, "log -n 1 --format=format:\"" + gitLogFormat + "\"");
			psi.WorkingDirectory = path;
			psi.RedirectStandardOutput = true;
			psi.StandardOutputEncoding = Encoding.Default;
			psi.UseShellExecute = false;
			Process p = Process.Start(psi);
			string line = null;
			int lineCount = 0;
			while (!p.StandardOutput.EndOfStream)
			{
				line = p.StandardOutput.ReadLine();
				lineCount++;
				Program.ShowDebugMessage(line, 4);
				if (lineCount == 1)
				{
					Match m = Regex.Match(line, @"^([0-9a-fA-F]{40}) ([0-9-]{10} [0-9:]{8} [0-9+-]{5}) ([0-9-]{10} [0-9:]{8} [0-9+-]{5})");
					if (m.Success)
					{
						data.CommitHash = m.Groups[1].Value;
						data.CommitTime = DateTimeOffset.Parse(m.Groups[2].Value);
						data.AuthorTime = DateTimeOffset.Parse(m.Groups[3].Value);
					}
				}
				else if (lineCount == 2)
				{
					data.CommitterName = line.Trim();
				}
				else if (lineCount == 3)
				{
					data.CommitterEMail = line.Trim();
				}
				else if (lineCount == 4)
				{
					data.AuthorName = line.Trim();
				}
				else if (lineCount == 5)
				{
					data.AuthorEMail = line.Trim();
				}
			}
			if (!p.WaitForExit(1000))
			{
				p.Kill();
			}

			if (!string.IsNullOrEmpty(data.CommitHash))
			{
				// Query the working directory state
				Program.ShowDebugMessage("Executing: git status --porcelain");
				Program.ShowDebugMessage("  WorkingDirectory: " + path);
				psi = new ProcessStartInfo(gitExec, "status --porcelain");
				psi.WorkingDirectory = path;
				psi.RedirectStandardOutput = true;
				psi.StandardOutputEncoding = Encoding.Default;
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

				// Query the current branch
				Program.ShowDebugMessage("Executing: git rev-parse --abbrev-ref HEAD");
				Program.ShowDebugMessage("  WorkingDirectory: " + path);
				psi = new ProcessStartInfo(gitExec, "rev-parse --abbrev-ref HEAD");
				psi.WorkingDirectory = path;
				psi.RedirectStandardOutput = true;
				psi.StandardOutputEncoding = Encoding.Default;
				psi.UseShellExecute = false;
				p = Process.Start(psi);
				line = null;
				if (!p.StandardOutput.EndOfStream)
				{
					line = p.StandardOutput.ReadLine();
					Program.ShowDebugMessage(line, 4);
					data.Branch = line.Trim();
				}
				p.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
				if (!p.WaitForExit(1000))
				{
					p.Kill();
				}
				data.IsModified = !string.IsNullOrEmpty(line);
			}
			return data;
		}

		#endregion IVcsProvider members

		#region Private helper methods

		private string FindGitBinary()
		{
			string git = null;
			string keyPath;
			RegistryKey key;

			// Try the PATH environment variable
			if (git == null)
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
					git = Path.Combine(dir, gitExeName);
					if (File.Exists(git))
					{
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %PATH%", 1);
						break;
					}
				}
				if (!File.Exists(git)) git = null;
			}

			// Read registry uninstaller key
			if (git == null)
			{
				keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						git = Path.Combine((string) loc, Path.Combine("bin", gitExeName));
						if (!File.Exists(git))
						{
							git = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + gitExeName + " in \"" + (string) loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Try 64-bit registry key
			if (git == null && Is64Bit)
			{
				keyPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						git = Path.Combine((string) loc, Path.Combine("bin", gitExeName));
						if (!File.Exists(git))
						{
							git = null;
						}
						else
						{
							Program.ShowDebugMessage("Found " + gitExeName + " in \"" + (string) loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Search program files directory
			if (git == null)
			{
				foreach (string dir in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "git*"))
				{
					git = Path.Combine(dir, gitExeName);
					if (File.Exists(git))
					{
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles%\\git*", 1);
						break;
					}
					git = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(git))
					{
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles%\\git*\\bin", 1);
						break;
					}
				}
			}

			// Try 32-bit program files directory
			if (git == null && Is64Bit)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFilesX86(), "git*"))
				{
					git = Path.Combine(dir, gitExeName);
					if (File.Exists(git))
					{
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\git*", 1);
						break;
					}
					git = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(git))
					{
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\git*\\bin", 1);
						break;
					}
				}
			}
			return git;
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

		#endregion Private helper methods
	}
}
