using System;
using System.Diagnostics;
using System.Globalization;
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

		public RevisionData ProcessDirectory(string path, string repoRoot)
		{
            string repositoryRoot = "";

            if (String.IsNullOrWhiteSpace(repoRoot) == false)
            {
                repositoryRoot = Path.GetFileName(repoRoot);
            }

            // Initialise data
            RevisionData data = new RevisionData
            {
                VcsProvider = this,
                RepoRootFolder = repositoryRoot
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
						data.CommitTime = DateTimeOffset.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
						data.AuthorTime = DateTimeOffset.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
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

				if ((data.Branch == "HEAD" || data.Branch.StartsWith("heads/")) &&
					Environment.GetEnvironmentVariable("CI_SERVER") == "yes")
				{
					// GitLab runner uses detached HEAD so the normal Git command will always return
					// "HEAD" instead of the actual branch name.

					// "HEAD" is reported by default with GitLab CI runner.
					// "heads/*" is reported if an explicit 'git checkout -B' command has been issued.

					// Use GitLab CI provided environment variables instead if the available data is
					// plausible.
					if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME")) &&
						string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_COMMIT_TAG")))
					{
						// GitLab v9
						Program.ShowDebugMessage("Reading branch name from CI environment variable: CI_COMMIT_REF_NAME");
						data.Branch = Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
					}
					else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_BUILD_REF_NAME")) &&
						string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_BUILD_TAG")))
					{
						// GitLab v8
						Program.ShowDebugMessage("Reading branch name from CI environment variable: CI_BUILD_REF_NAME");
						data.Branch = Environment.GetEnvironmentVariable("CI_BUILD_REF_NAME");
					}
					else
					{
						Program.ShowDebugMessage("No branch name available in CI environment");
						data.Branch = "";
					}
				}

				// Query the most recent matching tag
				if (!string.IsNullOrWhiteSpace(Program.TagMatch) || Program.TagMatch == null)
				{
					string tagMatchOption = "";
					if (Program.TagMatch != null)
					{
						tagMatchOption = " --match \"" + Program.TagMatch + "\"";
					}
					Program.ShowDebugMessage("Executing: git describe --tags --first-parent --long" + tagMatchOption);
					Program.ShowDebugMessage("  WorkingDirectory: " + path);
					psi = new ProcessStartInfo(gitExec, "describe --tags --first-parent --long" + tagMatchOption);
					psi.WorkingDirectory = path;
					psi.RedirectStandardOutput = true;
					psi.RedirectStandardError = true;
					psi.StandardOutputEncoding = Encoding.Default;
					psi.UseShellExecute = false;
					p = Process.Start(psi);
					line = null;
					if (!p.StandardOutput.EndOfStream)
					{
						line = p.StandardOutput.ReadLine();
						Program.ShowDebugMessage(line, 4);
						line = line.Trim();
						Match m = Regex.Match(line, @"^(.*)-([0-9]+)-g[0-9a-fA-F]+$");
						if (m.Success)
						{
							data.Tag = m.Groups[1].Value.Trim();
							data.CommitsAfterTag = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
						}
					}
					p.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
					if (!p.WaitForExit(1000))
					{
						p.Kill();
					}
				}

				// Query the linear revision number of the current branch (first parent)
				Program.ShowDebugMessage("Executing: git rev-list --first-parent --count HEAD");
				Program.ShowDebugMessage("  WorkingDirectory: " + path);
				psi = new ProcessStartInfo(gitExec, "rev-list --first-parent --count HEAD");
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
					int revNum;
					if (int.TryParse(line.Trim(), out revNum))
					{
						data.RevisionNumber = revNum;
					}
					else
					{
						Program.ShowDebugMessage("Revision count could not be parsed", 2);
					}
				}
				p.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
				if (!p.WaitForExit(1000))
				{
					p.Kill();
				}
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
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %PATH%", 1);
						break;
					}
				}
			}

			// Read registry uninstaller key
			if (git == null)
			{
				keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						string testPath = Path.Combine((string)loc, Path.Combine("bin", gitExeName));
						if (File.Exists(testPath))
						{
							git = testPath;
							Program.ShowDebugMessage("Found " + gitExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Try 64-bit registry key
			if (git == null && Is64Bit)
			{
				keyPath = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				key = Registry.LocalMachine.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						string testPath = Path.Combine((string)loc, Path.Combine("bin", gitExeName));
						if (File.Exists(testPath))
						{
							git = testPath;
							Program.ShowDebugMessage("Found " + gitExeName + " in \"" + (string)loc + "\" via HKLM\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Try user profile key (since Git for Windows 2.x)
			if (git == null)
			{
				keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				key = Registry.CurrentUser.OpenSubKey(keyPath);
				if (key != null)
				{
					object loc = key.GetValue("InstallLocation");
					if (loc is string)
					{
						string testPath = Path.Combine((string)loc, Path.Combine("bin", gitExeName));
						if (File.Exists(testPath))
						{
							git = testPath;
							Program.ShowDebugMessage("Found " + gitExeName + " in \"" + (string)loc + "\" via HKCU\\" + keyPath + "\\InstallLocation", 1);
						}
					}
				}
			}

			// Search program files directory
			if (git == null)
			{
				foreach (string dir in Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "git*"))
				{
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles%\\git*", 1);
						break;
					}
					testPath = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
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
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\git*", 1);
						break;
					}
					testPath = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\" via %ProgramFiles(x86)%\\git*\\bin", 1);
						break;
					}
				}
			}

			// Try Atlassian SourceTree local directory
			if (git == null)
			{
				string dir = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\Atlassian\SourceTree\git_local\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\"", 1);
				}
			}

			// Try Tower local directory
			if (git == null)
			{
				string dir = Environment.ExpandEnvironmentVariables(ProgramFilesX86() + @"\fournova\Tower\vendor\Git\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\"", 1);
				}
			}

			// Try SmartGit local directory
			if (git == null)
			{
				string dir = Environment.ExpandEnvironmentVariables(ProgramFilesX86() + @"\SmartGit\git\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Program.ShowDebugMessage("Found " + gitExeName + " in \"" + dir + "\"", 1);
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
					!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
			}
		}

		#endregion Private helper methods
	}
}
