using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Allgemeine Informationen über eine Assembly werden über die folgenden 
// Attribute gesteuert. Ändern Sie diese Attributwerte, um die Informationen zu ändern,
// die mit einer Assembly verknüpft sind.
[assembly: AssemblyTitle("SvnRevisionTool")]
[assembly: AssemblyDescription("Prints out the current Subversion revision info of a working directory.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("SvnRevisionTool")]
[assembly: AssemblyCopyright("© 2011-2013 Yves Goergen")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Durch Festlegen von ComVisible auf "false" werden die Typen in dieser Assembly unsichtbar 
// für COM-Komponenten. Wenn Sie auf einen Typ in dieser Assembly von 
// COM zugreifen müssen, legen Sie das ComVisible-Attribut für diesen Typ auf "true" fest.
[assembly: ComVisible(false)]

// Die folgende GUID bestimmt die ID der Typbibliothek, wenn dieses Projekt für COM verfügbar gemacht wird
[assembly: Guid("469c2d2c-c352-40c2-9865-3cabd37fd506")]

// Versionsinformationen für eine Assembly bestehen aus den folgenden vier Werten:
//
//      Hauptversion
//      Nebenversion 
//      Buildnummer
//      Revision
//
// Sie können alle Werte angeben oder die standardmäßigen Build- und Revisionsnummern 
// übernehmen, indem Sie "*" eingeben:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.7")]
[assembly: AssemblyFileVersion("1.7")]

// Version history:
//
// 1.7 (...)
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
