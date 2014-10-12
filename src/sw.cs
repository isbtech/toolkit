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

namespace Oxage
{
	/// <summary>
	/// Simple console stopwatch/timer.
	/// </summary>
	public class Program
	{
		public static void Main(string[] args)
		{
			var program = new Program();

			//Usage information
			if (args.Length == 1 && args[0] == "--help")
			{
				Console.WriteLine("Stopwatch v1.0");
				Console.WriteLine("");
				Console.WriteLine("Usage:");
				Console.WriteLine("  sw [auto|manual]");
				Console.WriteLine("");
				Console.WriteLine("Keyboard:");
				Console.WriteLine("  space  Toggle start/stop");
				Console.WriteLine("  enter  Start next timer");
				Console.WriteLine("  x/esc  Close the stopwatch");
				Console.WriteLine("  r      Reset the timer");
				return;
			}

			//Initialize the stopwatch
			var sw = new Stopwatch();
			var start = DateTime.Now;

			if (args.Length == 0 || args[0] == "auto")
			{
				//Start automatically
				sw.Start();
			}
			else if (args[0] == "manual")
			{
				//Wait for user input, i.e. press the spacebar key
			}

			bool running = true;
			while (running)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey();
					switch (key.Key)
					{
						case ConsoleKey.Enter:
							//Go to next line
							start = DateTime.Now;
							Console.WriteLine();

							if ((key.Modifiers & ConsoleModifiers.Shift) > 0)
							{
								sw.Reset();
							}
							break;

						case ConsoleKey.Spacebar:
							//Toggle start/stop
							if (sw.IsRunning)
							{
								sw.Stop();
							}
							else
							{
								sw.Start();
							}
							break;

						case ConsoleKey.R:
							sw.Reset();
							start = DateTime.Now;

							//Clear the line
							Console.CursorLeft = 0;
							Console.Write("                              ");
							Console.CursorLeft = 0;
							break;

						case ConsoleKey.X:
						case ConsoleKey.Escape:
							//Close the application
							sw.Stop();
							running = false;
							break;
					}
				}

				//Print the current state to the console
				Console.CursorLeft = 0;
				Console.Write("[{0}] {1}", start.ToString("HH:mm:ss"), sw.Elapsed);
			}
		}
	}
}