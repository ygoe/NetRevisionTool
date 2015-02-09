using System;
using System.Linq;
using NetRevisionTool.VcsProviders;

namespace NetRevisionTool
{
	internal class RevisionData
	{
		/// <summary>
		/// Gets or sets the VCS provider that provided the data in the current instance.
		/// </summary>
		public IVcsProvider VcsProvider { get; set; }

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
		/// Gets or sets a value indicating whether the working copy is modified.
		/// </summary>
		public bool IsModified { get; set; }

		/// <summary>
		/// Normalizes all data properties to prevent null values.
		/// </summary>
		public void Normalize()
		{
			if (CommitHash == null) CommitHash = "";
		}
	}
}
