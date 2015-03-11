using System;
using System.Linq;
using NetRevisionTool.VcsProviders;

namespace NetRevisionTool
{
	internal class RevisionData
	{
		#region Management properties

		/// <summary>
		/// Gets or sets the VCS provider that provided the data in the current instance.
		/// </summary>
		public IVcsProvider VcsProvider { get; set; }

		#endregion Management properties

		#region Revision data properties

		/// <summary>
		/// Gets or sets the commit hash of the currently checked out revision.
		/// </summary>
		public string CommitHash { get; set; }

		/// <summary>
		/// Gets or sets the revision number of the currently checked out revision.
		/// </summary>
		public int RevisionNumber { get; set; }

		/// <summary>
		/// Gets or sets the commit time of the currently checked out revision.
		/// </summary>
		public DateTimeOffset CommitTime { get; set; }

		/// <summary>
		/// Gets or sets the author time of the currently checked out revision.
		/// </summary>
		public DateTimeOffset AuthorTime { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the working copy is modified.
		/// </summary>
		public bool IsModified { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the working copy contains mixed revisions.
		/// </summary>
		public bool IsMixed { get; set; }

		/// <summary>
		/// Gets or sets the repository URL of the working directory.
		/// </summary>
		public string RepositoryUrl { get; set; }

		/// <summary>
		/// Gets or sets the committer name of the currently checked out revision.
		/// </summary>
		public string CommitterName { get; set; }

		/// <summary>
		/// Gets or sets the committer e-mail address of the currently checked out revision.
		/// </summary>
		public string CommitterEMail { get; set; }

		/// <summary>
		/// Gets or sets the author name of the currently checked out revision.
		/// </summary>
		public string AuthorName { get; set; }

		/// <summary>
		/// Gets or sets the author e-mail address of the currently checked out revision.
		/// </summary>
		public string AuthorEMail { get; set; }

		/// <summary>
		/// Gets or sets the branch currently checked out in the working directory.
		/// </summary>
		public string Branch { get; set; }

		#endregion Revision data properties

		#region Operations

		/// <summary>
		/// Normalizes all data properties to prevent null values.
		/// </summary>
		public void Normalize()
		{
			if (CommitHash == null) CommitHash = "";
			if (RepositoryUrl == null) RepositoryUrl = "";
			if (CommitterName == null) CommitterName = "";
			if (CommitterEMail == null) CommitterEMail = "";
			if (AuthorName == null) AuthorName = "";
			if (AuthorEMail == null) AuthorEMail = "";
			if (Branch == null) Branch = "";
		}

		/// <summary>
		/// Dumps the revision data is debug output is enabled.
		/// </summary>
		public void DumpData()
		{
			Program.ShowDebugMessage("Revision data:");
			Program.ShowDebugMessage("  AuthorEMail: " + AuthorEMail);
			Program.ShowDebugMessage("  AuthorName: " + AuthorName);
			Program.ShowDebugMessage("  AuthorTime: " + AuthorTime.ToString("yyyy-MM-dd HH:mm:ss K"));
			Program.ShowDebugMessage("  Branch: " + Branch);
			Program.ShowDebugMessage("  CommitHash: " + CommitHash);
			Program.ShowDebugMessage("  CommitterEMail: " + CommitterEMail);
			Program.ShowDebugMessage("  CommitterName: " + CommitterName);
			Program.ShowDebugMessage("  CommitTime: " + CommitTime.ToString("yyyy-MM-dd HH:mm:ss K"));
			Program.ShowDebugMessage("  IsMixed: " + IsMixed);
			Program.ShowDebugMessage("  IsModified: " + IsModified);
			Program.ShowDebugMessage("  RepositoryUrl: " + RepositoryUrl);
			Program.ShowDebugMessage("  RevisionNumber: " + RevisionNumber);
			Program.ShowDebugMessage("  VcsProvider: " + VcsProvider);
		}

		#endregion Operations
	}
}
