using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("NetRevisionTool")]
[assembly: AssemblyTitle("NetRevisionTool")]
[assembly: AssemblyDescription("Injects the current VCS revision into a .NET assembly build.")]
[assembly: AssemblyCopyright("© 2011–2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("2.0")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("2.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]

// Version history:
//
// 2.0 (2015-02-08)
// * Created project, based on GitRevisionTool and SvnRevisionTool 1.8
