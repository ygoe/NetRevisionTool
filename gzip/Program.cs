using System;
using System.IO;
using System.IO.Compression;

namespace gzip
{
	public class Program
	{
		public static int Main(string[] args)
		{
			string inputFile;
			string outputFile;

			// First argument: input file
			if (args.Length >= 1)
			{
				inputFile = args[0];
				outputFile = inputFile + ".gz";
			}
			else
			{
				Console.Error.WriteLine("No input file specified.");
				return 1;
			}

			// Second argument: output file (optional, default: .gz suffix)
			if (args.Length >= 2)
			{
				outputFile = args[1];
			}

			// Compress file
			try
			{
				using (FileStream inputStream = File.OpenRead(inputFile))
				using (FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
				using (GZipStream zip = new GZipStream(outputStream, CompressionMode.Compress))
				{
					inputStream.CopyTo(zip);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("ERROR: " + ex.ToString());
				return 2;
			}
			return 0;
		}
	}
}
