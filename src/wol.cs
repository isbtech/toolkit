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
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Oxage
{
	/// <summary>
	/// Wake-on-LAN (WOL) utility
	/// </summary>
	public class Program
	{
		private static string ip; //"192.168.1.6"
		private static string mac; //"00:1A:A0:48:F1:9B"
		private static int interval = 5;
		private static bool ping = false;

		public static void Main(string[] args)
		{
			if (args == null || args.Length == 0 || args[0] == "--help")
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("  wol [options]");
				Console.WriteLine("");
				Console.WriteLine("Options:");
				Console.WriteLine("  -mac [xx:xx:xx:xx:xx:xx]  MAC address of a device to wake up");
				Console.WriteLine("  -ip [a.b.c.d]  IP address of a device to ping");
				Console.WriteLine("  -f [host]      Find MAC address by IP or hostname");
				Console.WriteLine("  -t [sec]       Time to wait between pings");
				Console.WriteLine("  -p             Send wake up and ping until alive");
				Console.WriteLine("");
				Console.WriteLine("Examples:");
				Console.WriteLine("  wol -f 192.168.1.1");
				Console.WriteLine("  wol -mac 00:1A:A0:48:F1:9B");
				Console.WriteLine("  wol -mac 00:1A:A0:48:F1:9B -ip 192.168.1.1 -p -t 5");
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

			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				switch (arg)
				{
					case "-ip":
						ip = args[++i];
						break;

					case "-mac":
						mac = args[++i];
						Wol.Wake(mac);
						return;

					case "-p":
						ping = true;
						break;

					case "-t":
						interval = int.Parse(args[++i]);
						break;

					case "-f":
						var arp = new Arp();
						Console.WriteLine(arp.GetMACAddress(args[++i]));
						return;

					default:
						throw new Exception("Unknown argument: " + arg);
				}
			}

			if (string.IsNullOrEmpty(mac))
			{
				throw new Exception("MAC address is missing!");
			}

			var array = GetMacArray(mac);

			if (ping)
			{
				if (string.IsNullOrEmpty(ip))
				{
					throw new Exception("IP address is missing!");
				}

				//Send wake up signals until the target device responds
				while (!Ping(ip))
				{
					WakeUp(array);
					Thread.Sleep(interval * 1000);
				}
			}
			else
			{
				//Just send a WOL wake up signal
				WakeUp(array);
			}
		}

		/// <summary>
		/// Sends a ping signal to an IP address.
		/// </summary>
		/// <param name="ip"></param>
		/// <returns></returns>
		/// <remarks>
		/// Reference:
		/// http://msdn.microsoft.com/en-us/library/system.net.networkinformation.pingreply.buffer%28v=vs.110%29.aspx
		/// </remarks>
		public static bool Ping(string ip)
		{
			var ping = new Ping();

			//Create a buffer of 32 bytes of data to be transmitted.
			byte[] data = Encoding.ASCII.GetBytes("................................");

			//Jump though 64 routing nodes tops, and don't fragment the packet
			var options = new PingOptions(64, true);

			//Send the ping
			var reply = ping.Send(ip, 3000, data, options);
			return reply.Status == IPStatus.Success;
		}

		/// <summary>
		/// Sends a Wake-on-LAN packet to the specified MAC address.
		/// </summary>
		/// <param name="mac">Physical MAC address to send WOL packet to.</param>
		public static void WakeUp(byte[] mac)
		{
			//WOL packet is sent over UDP 255.255.255.0:40000.
			var client = new UdpClient();
			client.Connect(IPAddress.Broadcast, 40000);

			//WOL packet contains a 6-bytes trailer and 16 times a 6-bytes sequence containing the MAC address.
			byte[] packet = new byte[17 * 6];

			//Trailer of 6 times 0xFF.
			for (int i = 0; i < 6; i++)
			{
				packet[i] = 0xFF;
			}

			//Body of magic packet contains 16 times the MAC address.
			for (int i = 1; i <= 16; i++)
			{
				for (int j = 0; j < 6; j++)
				{
					packet[i * 6 + j] = mac[j];
				}
			}

			//Send the WOL packet
			client.Send(packet, packet.Length);
		}

		/// <summary>
		/// Converts MAC string to byte array.
		/// </summary>
		public static byte[] GetMacArray(string mac)
		{
			if (string.IsNullOrEmpty(mac))
			{
				throw new ArgumentNullException("mac");
			}

			mac = mac.Replace("-", "");
			byte[] result = new byte[6];

			try
			{
				string[] tmp = mac.Split(':', '-');
				if (tmp.Length != 6)
				{
					tmp = mac.Split('.');
					if (tmp.Length == 3)
					{
						for (int i = 0; i < 3; i++)
						{
							result[i * 2] = byte.Parse(tmp[i].Substring(0, 2), NumberStyles.HexNumber);
							result[i * 2 + 1] = byte.Parse(tmp[i].Substring(2, 2), NumberStyles.HexNumber);
						}
					}
					else
					{
						for (int i = 0; i < 12; i += 2)
						{
							result[i / 2] = byte.Parse(mac.Substring(i, 2), NumberStyles.HexNumber);
						}
					}
				}
				else
				{
					for (int i = 0; i < 6; i++)
					{
						result[i] = byte.Parse(tmp[i], NumberStyles.HexNumber);
					}
				}
			}
			catch
			{
				throw new ArgumentException("Argument doesn't have the correct format: " + mac, "mac");
			}

			return result;
		}
	}

	internal static class Win32
	{
		[DllImport("iphlpapi.dll", ExactSpelling = true)]
		internal static extern int SendARP(int DestIP, int SrcIP, byte[] pMacAddr, ref uint PhyAddrLen);
	}

	public class Arp
	{
		public string GetMACAddress(IPAddress ipAddress)
		{
			byte[] addressBytes = ipAddress.GetAddressBytes();
			int address = BitConverter.ToInt32(addressBytes, 0);

			byte[] macAddr = new byte[6];
			uint macAddrLen = (uint)macAddr.Length;

			if (Win32.SendARP(address, 0, macAddr, ref macAddrLen) != 0)
			{
				return null;
			}

			StringBuilder macAddressString = new StringBuilder();
			for (int i = 0; i < macAddr.Length; i++)
			{
				if (macAddressString.Length > 0)
					macAddressString.Append(":");

				macAddressString.AppendFormat("{0:x2}", macAddr[i]);
			}

			return macAddressString.ToString();
		}

		public string GetMACAddress(string hostName)
		{
			IPHostEntry hostEntry = null;
			try
			{
				hostEntry = Dns.GetHostEntry(hostName);
			}
			catch
			{
				return null;
			}

			if (hostEntry.AddressList.Length == 0)
			{
				return null;
			}

			// Find the first address IPV4 address for that host
			IPAddress ipAddress = null;

			foreach (IPAddress ip in hostEntry.AddressList)
			{
				if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
				{
					ipAddress = ip;
					break;
				}
			}

			// If running on .net 3.5 you can do it with LINQ :)
			//IPAddress ipAddress = hostEntry.AddressList.First<IPAddress>(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

			return GetMACAddress(ipAddress);
		}
	}

	/// <summary>
	/// Class for sending Wake-on-LAN Magic Packets
	/// </summary>
	public static class Wol
	{
		/// <summary>
		/// Wake up the device by sending a 'magic' packet
		/// </summary>
		/// <param name="macAddress"></param>
		public static void Wake(string macAddress)
		{
			string[] byteStrings = macAddress.Split(':');

			byte[] bytes = new byte[6];

			for (int i = 0; i < 6; i++)
				bytes[i] = (byte)Int32.Parse(byteStrings[i], System.Globalization.NumberStyles.HexNumber);

			Wake(bytes);
		}

		/// <summary>
		/// Send a magic packet
		/// </summary>
		/// <param name="macAddress"></param>
		public static void Wake(byte[] macAddress)
		{
			if (macAddress == null)
			{
				throw new ArgumentNullException("macAddress", "MAC Address must be provided");
			}

			if (macAddress.Length != 6)
			{
				throw new ArgumentOutOfRangeException("macAddress", "MAC Address must have 6 bytes");
			}

			// A Wake on LAN magic packet contains a 6 byte header and
			// the MAC address of the target MAC address (6 bytes) 16 times
			byte[] wolPacket = new byte[17 * 6];

			MemoryStream ms = new MemoryStream(wolPacket, true);

			// Write the 6 byte 0xFF header
			for (int i = 0; i < 6; i++)
			{
				ms.WriteByte(0xFF);
			}

			// Write the MAC Address 16 times
			for (int i = 0; i < 16; i++)
			{
				ms.Write(macAddress, 0, macAddress.Length);
			}

			// Broadcast the magic packet
			UdpClient udp = new UdpClient();
			udp.Connect(IPAddress.Broadcast, 0);
			udp.Send(wolPacket, wolPacket.Length);
		}
	}
}
