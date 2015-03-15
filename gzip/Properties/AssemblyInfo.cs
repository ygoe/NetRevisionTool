using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("gzip")]
[assembly: AssemblyTitle("gzip")]
[assembly: AssemblyDescription("Compresses a file with GZip.")]
[assembly: AssemblyCopyright("© 2015 Yves Goergen")]
[assembly: AssemblyCompany("unclassified software development")]

// Assembly identity version. Must be a dotted-numeric version.
[assembly: AssemblyVersion("1.0")]

// Repeat for Win32 file version resource because the assembly version is expanded to 4 parts.
[assembly: AssemblyFileVersion("1.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: ComVisible(false)]
