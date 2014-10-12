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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;

namespace Oxage
{
	/// <summary>
	/// AES crypto utility.
	/// </summary>
	public class Program
	{
		public static void Main(string[] args)
		{
			if (args == null || args.Length == 0 || args[0] == "--help")
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("  aes [options] [output]");
				Console.WriteLine();
				Console.WriteLine("Options:");
				Console.WriteLine("  -e [file]       Encrypt");
				Console.WriteLine("  -d [file]       Decrypt");
				Console.WriteLine("  -s [salt]       Salt");
				Console.WriteLine("  -p [password]   Password (prompt if not specified)");
				Console.WriteLine("  -r [file]       Scramble and remove file");
				Console.WriteLine("  -c              Direct console output (plain text only)");
				Console.WriteLine();
				Console.WriteLine("Examples:");
				Console.WriteLine("  aes -e file.txt file.cpt");
				Console.WriteLine("  aes -r file.cpt");
				Console.WriteLine("  aes -d file.cpt -s hello -p world file.txt");
				Console.WriteLine("  aes -d file.aes -p world -c | grep -t 10 gmail.com"); //grep -t 10 means toleance +/- 10 lines; this command is safe, output in console, stored in RAM only and cleared?
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

			//Collect arguments
			bool print = false;
			string method = null;
			string salt = "aesspice";
			string password = null;
			string inputPath = null;
			string outputPath = null;
			for (int i = 0; i < args.Length; i++)
			{
				switch (args[i])
				{
					case "-e":
						if (!string.IsNullOrEmpty(method))
						{
							throw new Exception("Only one of the following can be set: -e, -d, -r");
						}
						method = "encrypt";
						inputPath = args[++i];
						break;

					case "-d":
						if (!string.IsNullOrEmpty(method))
						{
							throw new Exception("Only one of the following can be set: -e, -d, -r");
						}
						method = "decrypt";
						inputPath = args[++i];
						break;

					case "-r":
						if (!string.IsNullOrEmpty(method))
						{
							throw new Exception("Only one of the following can be set: -e, -d, -r");
						}
						method = "remove";
						inputPath = args[++i];
						break;

					case "-p":
						password = args[++i];
						break;

					case "-s":
						salt = args[++i];
						break;

					case "-c":
						print = true;
						break;

					default:
						//Check if last argument but not an option parameter
						if (i == args.Length - 1 && !args[i].StartsWith("-"))
						{
							outputPath = args[args.Length - 1];
						}
						break;
				}
			}

			if (!IsPipedInput && string.IsNullOrEmpty(inputPath))
			{
				throw new Exception("Input file is missing!");
			}

			if (!IsPipedInput && !File.Exists(inputPath))
			{
				throw new Exception("File does not exist! Path: " + inputPath);
			}

			if (string.IsNullOrEmpty(password) && method != "remove")
			{
				Console.Write("Password: ");
				password = ReadLineMasked();
				Console.WriteLine();
				if (string.IsNullOrEmpty(password))
				{
					throw new Exception("Password is missing!");
				}

				//Confirm only when encrypting
				if (method == "encrypt")
				{
					Console.Write("Confirm: ");
					string confirm = ReadLineMasked();
					Console.WriteLine();
					if (password != confirm)
					{
						throw new Exception("Password does not match with confirmation!");
					}
				}
			}

			switch (method)
			{
				case "encrypt":
					{
						byte[] data = (IsPipedInput ? GetPipedData() : File.ReadAllBytes(inputPath));
						byte[] output = Encrypt(data, salt, password);
						if (!string.IsNullOrEmpty(outputPath))
						{
							File.WriteAllBytes(outputPath, output);
						}
					}
					break;

				case "decrypt":
					{
						byte[] data = File.ReadAllBytes(inputPath);
						byte[] output = Decrypt(data, salt, password);
						if (!string.IsNullOrEmpty(outputPath))
						{
							File.WriteAllBytes(outputPath, output);
						}
						if (print)
						{
							foreach (byte b in output)
							{
								Console.Write((char)b);
							}
						}
					}
					break;

				case "remove":
					if (File.Exists(outputPath))
					{
						//TODO: Check if SSD disk and set the flag
						//TODO: Remove directly, no recycle bin
						var info = new FileInfo(inputPath);

						//Get random data
						var random = new Random();
						byte[] data = new byte[(int)info.Length];
						random.NextBytes(data);

						//Replace original bytes with random bytes
						File.WriteAllBytes(inputPath, data);
						File.Delete(outputPath);
					}
					break;

				default:
					throw new Exception("Method not supported!");
			}
		}

		#region Console
		/// <summary>
		/// Masks password input in the console.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// Reference: http://stackoverflow.com/questions/3404421/password-masking-console-application
		/// </remarks>
		public static string ReadLineMasked()
		{
			string result = "";
			bool running = true;
			while (running)
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				switch (key.Key)
				{
					case ConsoleKey.Backspace:
						if (result.Length > 0)
						{
							result = result.Remove(result.Length - 1);
						}
						Console.Write("\b \b");
						break;

					case ConsoleKey.Enter:
						running = false;
						break;

					case ConsoleKey.Escape:
						Environment.Exit(0);
						break;

					default:
						result += key.KeyChar;
						Console.Write("*");
						break;
				}
			}

			return result;
		}

		/// <summary>
		/// Gets a value indicating whether some piped data is waiting in the buffer.
		/// </summary>
		public static bool IsPipedInput
		{
			get
			{
				try
				{
					bool isKey = Console.KeyAvailable;
					return false;
				}
				catch
				{
					return true;
				}
			}
		}

		/// <summary>
		/// Reads data from the piped input buffer.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// Reference: http://stackoverflow.com/questions/199528/c-sharp-console-receive-input-with-pipe
		/// </remarks>
		public static byte[] GetPipedData()
		{
			using (var stream = new MemoryStream())
			{
				using (var writer = new BinaryWriter(stream))
				{
					while (Console.In.Peek() != -1)
					{
						int length = 0;
						char[] buffer = new char[1024];
						while ((length = Console.In.Read(buffer, 0, 1024)) != 0)
						{
							writer.Write(buffer);
						}
					}

					return stream.ToArray();
				}
			}
		}
		#endregion

		#region Cryptography
		/// <summary>
		/// Encrypts a string using AES algorithm.
		/// </summary>
		/// <param name="input">String to be encrypted.</param>
		/// <param name="salt">Value to salt a password.</param>
		/// <param name="password">Actual password to encrypt the data.</param>
		/// <returns>Returns encrypted and base64 encoded string.</returns>
		/// <remarks>
		/// Reference:
		/// http://forums.silverlight.net/forums/p/14449/193866.aspx
		/// </remarks>
		public static byte[] Encrypt(byte[] input, string salt, string password)
		{
			// Test data
			byte[] saltBytes = UTF8Encoding.UTF8.GetBytes(salt);

			// Our symmetric encryption algorithm
			var aes = new AesManaged();

			// We're using the PBKDF2 standard for password-based key generation
			var rfc = new Rfc2898DeriveBytes(password, saltBytes);

			// Setting our parameters
			aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
			aes.KeySize = aes.LegalKeySizes[0].MaxSize;

			aes.Key = rfc.GetBytes(aes.KeySize / 8);
			aes.IV = rfc.GetBytes(aes.BlockSize / 8);

			// Encryption
			ICryptoTransform encryptTransf = aes.CreateEncryptor();

			// Output stream, can be also a FileStream
			using (MemoryStream encryptStream = new MemoryStream())
			{
				using (CryptoStream encryptor = new CryptoStream(encryptStream, encryptTransf, CryptoStreamMode.Write))
				{
					encryptor.Write(input, 0, input.Length);
					encryptor.Flush();
					encryptor.Close();

					// Showing our encrypted content
					byte[] encryptBytes = encryptStream.ToArray();
					return encryptBytes;
				}
			}
		}

		/// <summary>
		/// Decrypts a string using AES algorithm.
		/// </summary>
		/// <param name="input">Base64 encoded string to be decoded and decrypted.</param>
		/// <param name="salt">Value to salt a password.</param>
		/// <param name="password">Actual password to decrypt the data.</param>
		/// <returns>Returns decrypted string.</returns>
		/// <remarks>
		/// Reference:
		/// http://forums.silverlight.net/forums/p/14449/193866.aspx
		/// </remarks>
		public static byte[] Decrypt(byte[] input, string salt, string password)
		{
			byte[] saltBytes = Encoding.UTF8.GetBytes(salt);

			// Our symmetric encryption algorithm
			var aes = new AesManaged();

			// We're using the PBKDF2 standard for password-based key generation
			var rfc = new Rfc2898DeriveBytes(password, saltBytes);

			// Setting our parameters
			aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
			aes.KeySize = aes.LegalKeySizes[0].MaxSize;
			aes.Key = rfc.GetBytes(aes.KeySize / 8);
			aes.IV = rfc.GetBytes(aes.BlockSize / 8);

			// Now, decryption
			ICryptoTransform decryptTrans = aes.CreateDecryptor();

			// Output stream, can be also a FileStream
			using (MemoryStream decryptStream = new MemoryStream())
			{
				using (CryptoStream decryptor = new CryptoStream(decryptStream, decryptTrans, CryptoStreamMode.Write))
				{
					decryptor.Write(input, 0, input.Length);
					decryptor.Flush();
					decryptor.Close();

					// Showing our decrypted content
					byte[] decryptBytes = decryptStream.ToArray();
					return decryptBytes;
				}
			}
		}
		#endregion
	}
}