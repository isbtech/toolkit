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
using System.Globalization;
using System.Threading;

namespace Oxage
{
	/// <summary>
	/// Runs a specific command every n seconds. Inspired by unix "watch" command: http://www.linfo.org/watch.html
	/// </summary>
	public class Program
	{
		public static void Main(string[] args)
		{
			var program = new Program();

			//Usage information
			if (args.Length == 0 || args[0] == "--help")
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("  watch [options] \"command [arg1] [arg2]...\"");
				Console.WriteLine("");
				Console.WriteLine("Options:");
				Console.WriteLine("  -n secs  Number of seconds to repeat the command");
				Console.WriteLine("");
				Console.WriteLine("Example:");
				Console.WriteLine("  watch -n 2 \"ps -A | grep httpd\"");
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

			//Default values
			int interval = 2000;
			string command = null;

			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				switch (arg)
				{
					case "-n":
						var seconds = double.Parse(args[++i]);
						interval = (int)(seconds * 1000);
						break;

					default:
						command = arg;
						i = args.Length;
						break;
				}
			}

			if (interval < 100)
			{
				throw new Exception("Interval must be at least 0.1 second!");
			}

			if (string.IsNullOrEmpty(command))
			{
				throw new Exception("Command is missing!");
			}

			bool running = true;
			while (running)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey();
					switch (key.Key)
					{
						case ConsoleKey.Escape:
							running = false;
							break;
					}
				}

				string seconds = ((double)interval / 1000).ToString("N1", CultureInfo.InvariantCulture);
				string time = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);

				Console.Clear();
				Console.Write("Every {0}s: {1}", seconds, command);
				Console.CursorLeft = Console.BufferWidth - time.Length;
				Console.WriteLine(time);

				Process process = new Process();

				var info = new ProcessStartInfo();
				info.Arguments = "/C " + command;
				info.FileName = "cmd.exe";
				info.UseShellExecute = false;
				info.CreateNoWindow = true;
				//info.WorkingDirectory = System.IO.Path.GetDirectoryName();

				info.RedirectStandardOutput = true;
				info.RedirectStandardError = true;

				process.EnableRaisingEvents = false;
				process.StartInfo = info;
				process.Start();

				process.OutputDataReceived += (sender, e) => Console.Out.WriteLine(e.Data);
				process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);

				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				process.WaitForExit();
				process.Dispose();

				Thread.Sleep(interval);
			}
		}
	}
}