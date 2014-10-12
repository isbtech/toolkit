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
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Text;
using System.Windows.Forms;

namespace Oxage
{
	/// <summary>
	/// A simple HTML to PNG/JPEG converter.
	/// </summary>
	public static class Program
	{
		public static void Main(string[] args)
		{
			//Show help if required arguments are missing
			if (args == null || args.Length < 1 || args[0] == "--help")
			{
				var builder = new StringBuilder();
				builder.AppendLine("Usage:");
				builder.AppendLine("  html2png <options> [input.html] [output.png]");
				builder.AppendLine("");
				builder.AppendLine("Options:");
				builder.AppendLine("  -w [width]    Capture area and image width in pixels");
				builder.AppendLine("  -h [height]   Capture area and image height in pixels");
				//builder.AppendLine("  -ua [user-agent]   Send User-Agent header");
				builder.AppendLine("");
				builder.AppendLine("Examples:");
				builder.AppendLine("  html2png test.html");
				builder.AppendLine("  html2png sample.html sample.png");
				builder.AppendLine("  html2png http://www.google.com/");
				builder.AppendLine("  html2png -w 1920 -h 1080 http://www.google.com/ google.png");
				string output = builder.ToString();

#if JPEG
				output = output.Replace("html2png", "html2jpg");
				output = output.Replace(".png", ".jpg");
#endif

				Console.Write(output);
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

			//Parameters
			int? width = null;
			int? height = null;
			string source = null;
			string imagePath = null;

			//Parse command line arguments
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				switch (arg)
				{
					case "-w":
						width = int.Parse(args[++i]);
						break;

					case "-h":
						height = int.Parse(args[++i]);
						break;

					default:
						if (source == null)
						{
							source = arg;
						}
						else if (imagePath == null)
						{
							imagePath = arg;
						}
						else
						{
							throw new Exception("Unknown argument: " + arg);
						}
						break;
				}
			}

			//Generate output image file name
			if (string.IsNullOrEmpty(imagePath))
			{
				string name = Path.GetFileName(source);
				if (string.IsNullOrEmpty(name))
				{
					//Random name in case the source is URL
					name = Guid.NewGuid().ToString("N");
				}

#if JPEG
				imagePath = name + ".jpg";
#else
				imagePath = name + ".png";
#endif
			}

			//Check the files
			var uri = default(Uri);
			if (!Uri.TryCreate(source, UriKind.Absolute, out uri))
			{
				var file = new FileInfo(source);

				//Check if file exists
				if (!file.Exists)
				{
					throw new FileNotFoundException("Input file does not exist! File: " + file.Name);
				}

				//Ensure that absolute path is used
				source = file.FullName;
			}
			
			//Ensure the output directory exists
			var info = new FileInfo(imagePath);
			if (!info.Directory.Exists)
			{
				info.Directory.Create();
			}

			//Finally generate the image
			Generate(width, height, source, imagePath);
		}

		public static void Generate(int? width, int? height, string source, string imagePath)
		{
			//Initialize HTML to image converter
			WebsiteToImage converter = new WebsiteToImage();
			converter.Width = width;
			converter.Height = height;

			//Load HTML and capture bitmap image
			using (Bitmap bitmap = converter.Generate(source))
			{
#if JPEG
				bitmap.Save(imagePath, ImageFormat.Jpeg);
#else
				bitmap.Save(imagePath, ImageFormat.Png);
#endif
			}
		}
	}

	/// <summary>
	/// Converts HTML to image
	/// </summary>
	/// <remarks>
	/// Reference: http://stackoverflow.com/questions/2715385/convert-webpage-to-image-from-asp-net
	/// </remarks>
	public class WebsiteToImage
	{
		private Bitmap bitmap;
		private string url;

		public int? Width;
		public int? Height;

		public Bitmap Generate(string url)
		{
			this.url = url;
			Thread thread = new Thread(generate);
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			return bitmap;
		}

		private void generate()
		{
			WebBrowser browser = new WebBrowser();
			browser.ScrollBarsEnabled = false;
			browser.ScriptErrorsSuppressed = true;
			browser.Navigate(url);
			browser.DocumentCompleted += WebBrowser_DocumentCompleted;
			while (browser.ReadyState != WebBrowserReadyState.Complete)
			{
				Application.DoEvents();
			}
			browser.Dispose();
		}

		private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			//Capture
			WebBrowser browser = sender as WebBrowser;

			int w = (this.Width != null ? this.Width.Value : browser.Document.Body.ScrollRectangle.Width);
			int h = (this.Height != null ? this.Height.Value : browser.Document.Body.ScrollRectangle.Bottom);

			browser.ClientSize = new Size(w, h);
			browser.ScrollBarsEnabled = false;

			bitmap = new Bitmap(w, h);
			browser.BringToFront();
			browser.DrawToBitmap(bitmap, browser.Bounds);
		}
	}
}
