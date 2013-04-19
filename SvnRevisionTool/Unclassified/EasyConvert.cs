using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;

namespace Unclassified
{
	public class EasyConvert
	{
		public static string[] SplitQuoted(string str)
		{
			string[] rawChunks = str.Split(' ');
			List<string> chunks = new List<string>();
			bool inStr = false;
			foreach (string chunk in rawChunks)
			{
				if (!inStr && chunk.StartsWith("\"") && chunk.EndsWith("\""))
				{
					chunks.Add(chunk.Substring(1, chunk.Length - 2));
				}
				else if (!inStr && chunk.StartsWith("\""))
				{
					inStr = true;
					chunks.Add(chunk.Substring(1));
				}
				else if (inStr && chunk.EndsWith("\""))
				{
					inStr = false;
					chunks[chunks.Count - 1] += " " + chunk.Substring(0, chunk.Length - 1);
				}
				else if (!inStr)
				{
					chunks.Add(chunk);
				}
				else if (inStr)
				{
					chunks[chunks.Count - 1] += " " + chunk;
				}
			}
			return chunks.ToArray();
		}
	}
}
