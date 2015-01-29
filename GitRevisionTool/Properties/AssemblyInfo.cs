using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("GitRevisionTool")]
[assembly: AssemblyTitle("GitRevisionTool")]
[assembly: AssemblyDescription("Prints out the current Git revision info of a working directory.")]
[assembly: AssemblyCopyright("© 2011-2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly version, also used for Win32 file version resource.
// Must be a plain numeric version definition:
// 1. Major version number, should be increased with major new versions or rewrites of the application
// 2. Minor version number, should ne increased with minor feature changes or new features
// 3. Bugfix number, should be set or increased for bugfix releases of a previous version
// 4. Unused
[assembly: AssemblyVersion("1.7.4")]

#if NET20
#if DEBUG
[assembly: AssemblyConfiguration("Debug, NET20")]
#else
[assembly: AssemblyConfiguration("Release, NET20")]
#endif
#else
#if DEBUG
[assembly: AssemblyConfiguration("Debug, NET40")]
#else
[assembly: AssemblyConfiguration("Release, NET40")]
#endif
#endif

[assembly: ComVisible(false)]

// Version history:
//
// 1.7.4 (2015-01-29)
// * Added decimal 15-minutes version format as {dmin} to support strictly numeric versions
//
// 1.7.3 (2015-01-26)
// * Added separate .NET 4.0 build, recommended for Windows 8/10 or all VS 2010+ projects
// * Reorganised AssemblyInfo.cs
//
// 1.7.2 (2014-05-02)
// * Fixed PATH parsing for quoted entries
//
// 1.7.1 (2014-03-08)
// * Added icon
//
// 1.7 (2014-03-04)
// * Updated help message
// * Added support for Linux system environments
// * Also searching Git executable in the PATH environment variable
//
// 1.6 (2014-01-12)
// * Added base28 version format as new bmin, renamed base36 format to b36min
//
// 1.5.1 (2013-11-28)
// * Ignoring projects on restore if there is nothing to restore
//
// 1.5 (2013-05-23)
// * Added multi-project mode (ported from SvnRevisionTool 1.5)
//
// 1.4 (2012-11-26)
// * Added {bmin} base36 format with 10-minute intervals for higher compression of the time values
//   (4 bmin digits are good for 30 years, would need 6 digits with xmin)
// * Added {ut...} variants for commit/build date/time formats in UTC instead of local time
// * Added command line options to decode xmin and bmin values to a readable time (UTC and local)
// * Added command line option to check the current and upcoming bmin values (in case the text is unwanted)
// * Fixed {xmin} and {bmin} time zone handling, it is now using UTC
//
// 1.3 (2012-09-07)
// * Added {builddate} and {buildtime} placeholders to insert the build time (i.e. current time)
//
// 1.2.2 (2012-05-06)
// * Fixed {commit} format length parsing to allow 1-4 as value
// * Small help text update and typo fixed
//
// 1.2.1 (2012-01-31)
// * Be silent with the --ignore-missing parameter set to avoid VS build errors
//
// 1.2 (2012-01-30)
// * Added parameter --ignore-missing to ignore missing Git binary or working directory
// * Made the <path> parameter actually work again (was ignored before)
// * Kind of fixed {xmin} output for negative values (giving minus-prefixed values now)
//
// 1.1 (2012-01-28)
// * Git binary is first searched in the location from the uninstaller registry key
// * New format option {xmin} for compressed high-resolution times
//
// 1.0.1 (2012-01-04)
// * Git binary is searched in several directories (%ProgramFiles*%\Git*\bin\git.exe)
//
// 1.0 (2011-12-14) Initial version
// * Created GitRevisionTool based on SvnRevisionTool, using installed msysGit
