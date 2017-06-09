using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct(".NET Revision Tool")]
[assembly: AssemblyTitle(".NET Revision Tool")]
[assembly: AssemblyDescription("Injects the current VCS revision into a .NET assembly build.")]
[assembly: AssemblyCopyright("© 2011–2017 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("2.6.3")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("2.6.3")]

// Indicate the build configuration
#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

// Other attributes
[assembly: ComVisible(false)]

// Version history:
//
// 2.6.2 (2017-04-06)
// * Updated environment variables for GitLab CI runner v9.0
//
// 2.6.1 (2017-01-04)
// * Finding git.exe from SmartGit local installations
//
// 2.6 (2016-12-02)
// * Finding git.exe from SourceTree and Tower local installations
// * Added {mname} placeholder resolving to the machine name
// * Added /echo option to display the resolved informational version string
//
// 2.5.2 (2016-08-10)
// * Detect GitLab CI runner and try to determine branch name from environment variables if necessary
//
// 2.5.1 (2016-06-14)
// * Fixed console redirection detection for "nul"
//
// 2.5 (2016-05-16)
// * Added /removetagv option to make version number tag names useful
// * Added copyright year resolving, opt-out with /nocopyright option
//
// 2.4 (2015-12-21)
// * Added Git tag support
//
// 2.3.3 (2015-12-01)
// * Optimised detection of Git for Windows 2.x
//
// 2.3.2 (2015-07-28)
// * Added global exception handler
// * Retrying to write file on IOException (observed if VS2015 is still open with the project)
//
// 2.3.1 (2015-04-21)
// * Fix: All Git directories are considered modified
//
// 2.3 (2015-04-14)
// * Revision number generation for Git repositories using --first-parent
// * Removed unused shared code to reduce executable file size
//
// 2.2 (2015-04-02)
// * /revonly can be used with a revision format that produces an integer number
//
// 2.1 (2015-03-11)
// * New revision data fields (author time/name/mail, committer name/mail, repo URL, branch)
// * Truncated version number check
// * New hours time scheme
// * Added /root and /rejectmix parameters
//
// 2.0.1 (2015-02-09)
// * Fix: Relative path specified on command line can't find parent directory.
// * Fix: Crash if no commit hash is available, like for SVN.
// * Fix: SVN requires exact actual casing of the working directory path like on Unix.
//
// 2.0 (2015-02-08)
// * Created project, based on GitRevisionTool and SvnRevisionTool 1.8
