/******************************************************************************************
 * The MIT License (MIT)
 *
 * Copyright (c) 2014 oxage.net
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 ******************************************************************************************/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Oxage
{
	/// <summary>
	/// SHA1 checksum generator and validator.
	/// </summary>
	/// <example>
	/// Example of generated sample.sha1 using command "sha1 -f sample.cs > sample.sha1"
	/// Note that file name is not important but must end with .sha1 extension.
	/// 4aed9fef217b22371be0972f79b61a62 *sample.cs
	///
	/// Can contain list of files and relative paths
	/// 1b64a4bc0748d783c6c0853861189420 *src/weather.cs
	/// 03fba1627e3561cf7fe7dd5f0cc42e26 *src/wget.cs
	/// 26878ad719edd458b62cb6427b5ab2e2 *src/whois.cs
	/// </example>
	public class Program
	{
		public static void Main(string[] args)
		{
			//Print help message if the required arguments are not set
			if (args == null || args.Length == 0 || args[0] == "--help" || args.Length != 2)
			{
				Usage();
				return;
			}

			//Handle global exception
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				var error = e.ExceptionObject as Exception;
				var color = Console.ForegroundColor;
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(error.Message);
				Console.ForegroundColor = color;
				Environment.Exit(-1);
			};

			switch (args[0])
			{
				case "-c":
					//Generate chechum from plain text
					GenerateContentChecksum(args[1]);
					break;

				case "-f":
					//Generate checksum for a file
					GenerateFileChecksum(args[1]);
					break;

				case "-d":
					//Generate checksum for files in the directory
					GenerateDirectoryChecksum(args[1]);
					break;

				case "-v":
					//Verify checkum of a file or list of files
					Verify(args[1]);
					break;

				default:
					//Help message
					Usage();
					break;
			}
		}
		
		/// <summary>
		/// Prints help message to the console.
		/// </summary>
		public static void Usage()
		{
			Console.WriteLine("Usage:");
			Console.WriteLine("  sha1 -c [content]");
			Console.WriteLine("  sha1 -f [file]");
			Console.WriteLine("  sha1 -d [dir]");
			Console.WriteLine("  sha1 -v [sha1-file-to-verify]");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  sha1 -c \"Hello World\"");
			Console.WriteLine("  sha1 -f sample.txt > sample.sha1");
			Console.WriteLine("  sha1 -d d:\\share");
			Console.WriteLine("  sha1 -v sample.sha1");
		}

		/// <summary>
		/// Generates chechum from a string and prints output to the console.
		/// </summary>
		/// <param name="content">Plain text content.</param>
		public static void GenerateContentChecksum(string content)
		{
			using (var stream = new MemoryStream())
			{
				byte[] data = Encoding.UTF8.GetBytes(content);
				stream.Write(data, 0, data.Length);
				stream.Position = 0;
				string hash = SHA1(stream);
				Console.Write(hash);
			}
		}

		/// <summary>
		/// Generates checksum for a file and prints output to the console.
		/// </summary>
		/// <param name="path">Path to a file.</param>
		public static void GenerateFileChecksum(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentException("Path argument is missing!");
			}

			if (!File.Exists(path))
			{
				throw new FileNotFoundException("File does not exist!" + Environment.NewLine + "Path: " + path);
			}

			using (var stream = File.OpenRead(path))
			{
				string hash = SHA1(stream);
				WriteEntry(hash, path);
			}
		}

		/// <summary>
		/// Generates checksum for files in the directory
		/// </summary>
		/// <param name="directory"></param>
		public static void GenerateDirectoryChecksum(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				throw new ArgumentException("Path argument is missing!");
			}

			if (!Directory.Exists(directory))
			{
				throw new FileNotFoundException("Directory does not exist!" + Environment.NewLine + "Path: " + directory);
			}

			string[] files = Directory.GetFiles(directory);
			foreach (string file in files)
			{
				try
				{
					string path = file;
					using (var stream = File.OpenRead(path))
					{
						string hash = SHA1(stream);

						//Simplify path, e.g. ".\sha1.exe" -> "sha1.exe"
						if (path.StartsWith(@".\"))
						{
							path = path.Substring(2);
						}

						WriteEntry(hash, path);
					}
				}
				catch
				{
					//Warning: file is not listed, may be used by another process
				}
			}
		}

		/// <summary>
		/// Verifies checkum of a file or list of files and prints output to the console.
		/// </summary>
		/// <param name="path">Path to a .sha1 file.</param>
		public static void Verify(string path)
		{
			//Default console text color
			var color = Console.ForegroundColor;

			using (var stream = File.OpenRead(path))
			{
				using (var reader = new StreamReader(stream))
				{
					string line = null;
					while ((line = reader.ReadLine()) != null)
					{
						var match = Regex.Match(line, @"^(?<sha1>[\w\d]{32})\s\*(?<file>.+)", RegexOptions.IgnoreCase);
						if (match.Success)
						{
							string status = "    ";
							string sha1 = match.Groups["sha1"].Value;
							string file = match.Groups["file"].Value;

							if (File.Exists(file))
							{
								using (var fs = File.OpenRead(file))
								{
									//Compare the SHA1 of the file and .sha1 entry
									string checksum = SHA1(fs);
									bool ok = (checksum == sha1);
									status = ok ? " OK " : "FAIL";

									Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
								}
							}
							else
							{
								//File is missing
								status = "MISS";

								Console.ForegroundColor = ConsoleColor.Yellow;
							}
							
							//Print the result
							string report = string.Format("[{0}] {1}", status, file);
							Console.WriteLine(report);
							Console.ForegroundColor = color;
						}
					}
				}
			}

			//Restore the default color
			Console.ForegroundColor = color;
		}

		/// <summary>
		/// Writes an entry to the .sha1 list.
		/// </summary>
		/// <param name="sha1"></param>
		/// <param name="path"></param>
		public static void WriteEntry(string sha1, string path)
		{
			Console.WriteLine(sha1 + " *" + path);
		}

		/// <summary>
		/// Generates SHA1 checksum of a stream.
		/// </summary>
		/// <param name="stream"></param>
		/// <returns></returns>
		public static string SHA1(Stream stream)
		{
			var crypto = new SHA1CryptoServiceProvider();
			var bytes = crypto.ComputeHash(stream);
			var builder = new StringBuilder();
			foreach (byte b in bytes)
			{
				builder.Append(b.ToString("x2"));
			}
			return builder.ToString();
		}
	}
}