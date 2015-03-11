using System;

namespace NetRevisionTool
{
	internal enum ExitCodes
	{
		NoError = 0,
		CmdLineError = 1,
		RequiredVcs = 2,
		InvalidScheme = 3,
		InvalidRevId = 4,
		RejectModified = 5,
		NotASolution = 6,
		FileNotFound = 7,
		NoProjects = 8,
		UnsupportedLanguage = 9,
		NoNumericVersion = 10,
		RevNumTooLarge = 11,
		RejectMixed = 12,
	}
}
