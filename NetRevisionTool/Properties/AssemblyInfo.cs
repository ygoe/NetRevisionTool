using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("NetRevisionTool")]
[assembly: AssemblyTitle("NetRevisionTool")]
[assembly: AssemblyDescription("Injects the current VCS revision into a .NET assembly build.")]
[assembly: AssemblyCopyright("© 2011–2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("2.3.1")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("2.3.1")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]

// Version history:
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
