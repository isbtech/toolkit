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
using System.Runtime.InteropServices;

namespace Oxage
{
	/// <summary>
	/// A simple icon extractor.
	/// </summary>
	public class Program
	{
		public const uint SHGFI_ICON = 0x100;
		public const uint SHGFI_LARGEICON = 0x0; //Large icon
		public const uint SHGFI_SMALLICON = 0x1; //Small icon

		public static void Main(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				Console.WriteLine("Usage: geticon [file]");
				return;
			}

			//ExtractUsingWinAPI(args[0]);
			ExtractUsingIcon(args[0]);
		}

		public static void GetFileTypeAndIcon()
		{
			//TODO: icon by mime type
			//Reference: http://www.codeproject.com/KB/cs/GetFileTypeAndIcon.aspx
			throw new NotImplementedException();
		}

		/// <summary>
		/// Extracts the icon using System.Drawing.Icon
		/// </summary>
		/// <param name="file"></param>
		/// <remarks>
		/// Reference:
		/// http://msdn.microsoft.com/en-us/library/ms404308.aspx
		/// </remarks>
		public static void ExtractUsingIcon(string file)
		{
			System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(file);
			using (FileStream stream = File.OpenWrite(Path.GetFileName(file) + ".ico"))
			{
				icon.Save(stream);
			}
		}

		/// <summary>
		/// Extracts the icon using Win32 API.
		/// </summary>
		/// <param name="strFile"></param>
		/// <remarks>
		/// References:
		/// http://support.microsoft.com/kb/319350
		/// http://support.microsoft.com/kb/319340
		/// http://social.msdn.microsoft.com/Forums/en/netfxbcl/thread/60610aff-2dfd-4d52-9c4d-638d514100d0
		/// </remarks>
		public static void ExtractUsingWinAPI(string strFile)
		{
			Win32.SHFILEINFO shinfo = new Win32.SHFILEINFO();
			IntPtr hImgSmall = Win32.SHGetFileInfo(strFile, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON);

			//The icon is returned in the hIcon member of the shinfo struct
			System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
			using (FileStream stream = File.OpenWrite(Path.GetFileName(strFile) + ".ico"))
			{
				icon.Save(stream);
			}
		}
	}

	public class Win32
	{
		public const int SHGFI_DISPLAYNAME = 0x200;
		public const int SHGFI_EXETYPE = 0x2000;
		public const int SHGFI_SYSICONINDEX = 0x4000; //System icon index
		public const int SHGFI_LARGEICON = 0x0; //Large icon
		public const int SHGFI_SMALLICON = 0x1; //Small icon
		public const int ILD_TRANSPARENT = 0x1; //Display transparent
		public const int SHGFI_SHELLICONSIZE = 0x4;
		public const int SHGFI_TYPENAME = 0x400;
		public const int BASIC_SHGFI_FLAGS = SHGFI_TYPENAME | SHGFI_SHELLICONSIZE | SHGFI_SYSICONINDEX | SHGFI_DISPLAYNAME | SHGFI_EXETYPE;

		public const int MAX_PATH = 260;

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		public struct SHFILEINFO
		{
			public IntPtr hIcon;
			public int iIcon;
			public uint dwAttributes;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string szDisplayName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
			public string szTypeName;
		};

		[DllImport("comctl32.dll", SetLastError = true)]
		public static extern bool ImageList_DrawEx(IntPtr himl, int i, IntPtr hdcDst, int x, int y, int dx, int dy, int rgbBk, int rgbFg, int fStyle);

		[DllImport("shell32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
	}
}