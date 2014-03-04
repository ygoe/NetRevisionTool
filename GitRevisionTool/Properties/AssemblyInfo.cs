using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Allgemeine Informationen über eine Assembly werden über die folgenden 
// Attribute gesteuert. Ändern Sie diese Attributwerte, um die Informationen zu ändern,
// die mit einer Assembly verknüpft sind.
[assembly: AssemblyTitle("GitRevisionTool")]
[assembly: AssemblyDescription("Prints out the current Git revision info of a working directory.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("GitRevisionTool")]
[assembly: AssemblyCopyright("© 2011-2014 Yves Goergen")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Durch Festlegen von ComVisible auf "false" werden die Typen in dieser Assembly unsichtbar 
// für COM-Komponenten. Wenn Sie auf einen Typ in dieser Assembly von 
// COM zugreifen müssen, legen Sie das ComVisible-Attribut für diesen Typ auf "true" fest.
[assembly: ComVisible(false)]

// Die folgende GUID bestimmt die ID der Typbibliothek, wenn dieses Projekt für COM verfügbar gemacht wird
[assembly: Guid("2658290a-df90-4fb1-a37c-7d9cb64dea26")]

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
