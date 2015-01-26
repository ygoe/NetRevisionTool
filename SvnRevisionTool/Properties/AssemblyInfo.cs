using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("SvnRevisionTool")]
[assembly: AssemblyTitle("SvnRevisionTool")]
[assembly: AssemblyDescription("Prints out the current Subversion revision info of a working directory.")]
[assembly: AssemblyCopyright("© 2011-2014 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly version, also used for Win32 file version resource.
// Must be a plain numeric version definition:
// 1. Major version number, should be increased with major new versions or rewrites of the application
// 2. Minor version number, should ne increased with minor feature changes or new features
// 3. Bugfix number, should be set or increased for bugfix releases of a previous version
// 4. Unused
[assembly: AssemblyVersion("1.7.3")]

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
// 1.7.3 (2015-01-26)
// * Added separate .NET 4.0 build, recommended for Windows 8/10 or all VS 2010+ projects
// * Reorganised AssemblyInfo.cs
// * Added icon (like in GitRevisionTool)
//
// 1.7.2 (2014-05-02)
// * Fixed PATH parsing for quoted entries
//
// 1.7 (2014-03-04)
// * Updated help message
// * Added support for Linux system environments
// * Also searching SVN executable in the PATH environment variable
// * Added missing output of commit date and time
//
// 1.6 (2014-01-12)
// * Added base28 version format as new bmin, renamed base36 format to b36min
//
// 1.5.1 (2013-06-20)
// * Fixed version 12 (SVN 1.7) detection with no line break
//
// 1.5 (2013-04-22+)
// * Added multi-project mode
//
// 1.4 (2013-04-11)
// * Backport from GitRevisionTool 1.4
