using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("NetRevisionTool")]
[assembly: AssemblyTitle("NetRevisionTool")]
[assembly: AssemblyDescription("Injects the current VCS revision into a .NET assembly build.")]
[assembly: AssemblyCopyright("© 2011–2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version.
// Must be a plain numeric version definition:
// 1. Major version number, should be increased with major new versions or rewrites of the application
// 2. Minor version number, should ne increased with minor feature changes or new features
// 3. Bugfix number, should be set or increased for bugfix releases of a previous version
// 4. Unused
[assembly: AssemblyVersion("2.0")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("2.0")]

#if DEBUG
[assembly: AssemblyConfiguration("PackedDebug")]
#else
[assembly: AssemblyConfiguration("PackedRelease")]
#endif

[assembly: ComVisible(false)]

// Version history:
//
// 2.0 (2015-02-xx)
// * Created new project
