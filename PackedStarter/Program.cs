using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace PackedStarter
{
	internal class Program
	{
		private static int Main(string[] args)
		{
			// Get embedded compressed resource stream and decompress data
			Stream byteStream = new MemoryStream();
			using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PackedStarter.NetRevisionTool.exe.gz"))
			using (GZipStream zip = new GZipStream(resourceStream, CompressionMode.Decompress, true))
			{
				zip.CopyTo(byteStream);
			}

			// Copy decompressed stream to an array
			byte[] bytes = new byte[byteStream.Length];
			byteStream.Seek(0, SeekOrigin.Begin);
			byteStream.Read(bytes, 0, bytes.Length);
			byteStream.Dispose();

			// Load embedded assembly
			Assembly assembly = Assembly.Load(bytes);

			// Find and invoke Program.Main method
			object returnValue = assembly.EntryPoint.Invoke(null, new object[] { args });

			// Pass on return code
			if (returnValue is int)
			{
				return (int) returnValue;
			}
			return 0;
		}
	}
}
