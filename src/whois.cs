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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Oxage
{
	public class Program
	{
		public static void Main(string[] args)
		{
			if (args == null || args.Length == 0 || args[0] == "--help")
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("  whois <options> [domain]");
				Console.WriteLine();
				Console.WriteLine("Options:");
				Console.WriteLine("  -w <server>    Set the whois server");
				Console.WriteLine("  -i <list.txt>  Query a list of domains, with -s only");
				Console.WriteLine("  -s             Simple output: free or taken");
				Console.WriteLine("  -l             Display whois resolvers");
				Console.WriteLine();
				Console.WriteLine("Examples:");
				Console.WriteLine("  whois -l");
				Console.WriteLine("  whois google.com");
				Console.WriteLine("  whois -s -i domainlist.txt");
				Console.WriteLine("  whois -w whois.iana.org google.com");
				Console.WriteLine("  whois -w whois.godaddy.com google.com");
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

			string list = null;
			string server = null;
			string domain = null;
			string command = "whois";

			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				switch (arg)
				{
					case "-s":
						//server = "whois.verisign-grs.com";
						command = "simple";
						break;

					case "-l":
						command = "list";
						break;

					case "-i":
						list = args[++i];
						break;

					case "-w":
						server = args[++i];
						break;

					default:
						if (domain == null)
						{
							domain = args[i];
						}
						else
						{
							throw new Exception("Unknown argument: " + args[i]);
						}
						break;
				}
			}

			switch (command)
			{
				case "simple":
					{
						var domains = new List<string>();
						if (!string.IsNullOrEmpty(list))
						{
							//List of domains
							domains = File.ReadAllLines(list).ToList();
						}
						else
						{
							//Single domain query
							domains.Add(domain);
						}

						//Default console text color
						var color = Console.ForegroundColor;

						foreach (string d in domains)
						{
							if (server == null)
							{
								server = Whois.GetResolver(domain);
							}

							var result = Whois.Query(server, "domain " + d) ?? "";
							bool? free = Whois.IsFree(result, server, d); //Interpret result

							var highlight = color;
							string status = null;
							if (free == true)
							{
								status = "FREE ";
								highlight = ConsoleColor.Green;
							}
							else if (free == false)
							{
								status = "TAKEN";
								highlight = ConsoleColor.Red;
							}
							else
							{
								status = "?????";
								highlight = ConsoleColor.Yellow;
							}

							string message = string.Format("[{0}] {1}", status, d);

							Console.ForegroundColor = highlight;
							Console.WriteLine(message);
							Console.ForegroundColor = color;
						}
					}
					break;

				case "list":
					//Print a list of build-in whois resolvers
					var dictionary = Whois.RootZoneDatabase;
					foreach (var name in dictionary.Keys)
					{
						string wserver = dictionary[name];
						if (!name.StartsWith(".xn--") && !string.IsNullOrEmpty(wserver))
						{
							Console.WriteLine("{0,-24} {1}", name, wserver);
						}
					}
					break;

				case "whois":
					if (domain == null)
					{
						throw new Exception("Domain is missing!");
					}

					if (server == null)
					{
						server = Whois.GetResolver(domain);
					}

					var output = Whois.Query(server, /* "domain " + */ domain) ?? "";
					Console.WriteLine(output);
					break;

				default:
					throw new NotImplementedException("Command not implemented: " + command);
			}
		}
	}

	public class Whois
	{
		/// <summary>
		/// Queries a whois server.
		/// </summary>
		/// <param name="host">Server host name, e.g. whois.arin.net</param>
		/// <param name="query">Query to process, i.e. domain name</param>
		/// <returns>String result from the server</returns>
		public static string Query(string host, string query)
		{
			var response = new StringBuilder();

			Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			//socket.Connect(new IPEndPoint(Dns.Resolve(host).AddressList[0], 43));
			socket.Connect(new IPEndPoint(Dns.GetHostEntry(host).AddressList[0], 43));
			socket.Send(Encoding.ASCII.GetBytes(query + "\r\n"));

			byte[] buffer = new byte[1024];
			int size = socket.Receive(buffer);
			while (size > 0)
			{
				response.Append(Encoding.ASCII.GetString(buffer, 0, size));
				size = socket.Receive(buffer);
			}

			if (socket.Connected)
			{
				socket.Disconnect(false);
			}

			socket.Shutdown(SocketShutdown.Both);
			socket.Close();

			return response.ToString();
		}

		public static string GetResolver(string domain)
		{
			string tld = null;
			string server = null;

			var match = Regex.Match(domain.ToLower(), @"^.*(?<tld>\.\w+)\s*$");
			if (match.Success)
			{
				tld = match.Groups["tld"].Value;
				server = Whois.RootZoneDatabase[tld];
			}

			if (server == null)
			{
				throw new Exception("No whois server found for TLD " + tld);
			}

			return server;
		}

		public static bool? IsFree(string result, string server, string domain)
		{
			switch (server)
			{
				case "whois.verisign-grs.com":
					if (result.Contains("No match for domain \"" + domain.ToUpper() + "\""))
					{
						//Domain is available
						return true;
					}
					else if (result.Contains("Registrar:"))
					{
						//Domain is already registered
						return false;
					}
					break;
			}

			//Status unknown
			return null;
		}

		/// <summary>
		/// Root Zone Database: http://www.iana.org/domains/root/db
		/// </summary>
		public static readonly Dictionary<string, string> RootZoneDatabase = new Dictionary<string, string>()
		{
			{ ".ac", "whois.nic.ac" },
			{ ".academy", "whois.donuts.co" },
			{ ".accountants", "whois.donuts.co" },
			{ ".active", "whois.afilias-srs.net" },
			{ ".actor", "whois.unitedtld.com" },
			{ ".ad", null },
			{ ".ae", "whois.aeda.net.ae" },
			{ ".aero", "whois.aero" },
			{ ".af", "whois.nic.af" },
			{ ".ag", "whois.nic.ag" },
			{ ".agency", "whois.donuts.co" },
			{ ".ai", "whois.ai" },
			{ ".airforce", "whois.unitedtld.com" },
			{ ".al", null },
			{ ".allfinanz", "whois.ksregistry.net" },
			{ ".alsace", "whois-alsace.nic.fr" },
			{ ".am", "whois.amnic.net" },
			{ ".an", null },
			{ ".ao", null },
			{ ".aq", null },
			{ ".ar", null },
			{ ".archi", "whois.ksregistry.net" },
			{ ".army", "whois.rightside.co" },
			{ ".arpa", "whois.iana.org" },
			{ ".as", "whois.nic.as" },
			{ ".asia", "whois.nic.asia" },
			{ ".associates", "whois.donuts.co" },
			{ ".at", "whois.nic.at" },
			{ ".attorney", "whois.rightside.co" },
			{ ".au", "whois.audns.net.au" },
			{ ".auction", "whois.unitedtld.com" },
			{ ".audio", "whois.uniregistry.net" },
			{ ".autos", "whois.afilias-srs.net" },
			{ ".aw", "whois.nic.aw" },
			{ ".ax", "whois.ax" },
			{ ".axa", null },
			{ ".az", null },
			{ ".ba", null },
			{ ".bar", "whois.nic.bar" },
			{ ".bargains", "whois.donuts.co" },
			{ ".bayern", "whois-dub.mm-registry.com" },
			{ ".bb", null },
			{ ".bd", null },
			{ ".be", "whois.dns.be" },
			{ ".beer", "whois-dub.mm-registry.com" },
			{ ".berlin", "whois.nic.berlin" },
			{ ".best", "whois.nic.best" },
			{ ".bf", null },
			{ ".bg", "whois.register.bg" },
			{ ".bh", null },
			{ ".bi", "whois1.nic.bi" },
			{ ".bid", null },
			{ ".bike", "whois.donuts.co" },
			{ ".bio", "whois.ksregistry.net" },
			{ ".biz", "whois.biz" },
			{ ".bj", "whois.nic.bj" },
			{ ".bl", null },
			{ ".black", "whois.afilias.net" },
			{ ".blackfriday", "whois.uniregistry.net" },
			{ ".blue", "whois.afilias.net" },
			{ ".bm", null },
			{ ".bmw", "whois.ksregistry.net" },
			{ ".bn", "whois.bn" },
			{ ".bnpparibas", "whois.afilias-srs.net" },
			{ ".bo", "whois.nic.bo" },
			{ ".boo", "domain-registry-whois.l.google.com" },
			{ ".boutique", "whois.donuts.co" },
			{ ".bq", null },
			{ ".br", "whois.registro.br" },
			{ ".brussels", "whois.nic.brussels" },
			{ ".bs", null },
			{ ".bt", null },
			{ ".budapest", "whois-dub.mm-registry.com" },
			{ ".build", "whois.nic.build" },
			{ ".builders", "whois.donuts.co" },
			{ ".business", "whois.donuts.co" },
			{ ".buzz", null },
			{ ".bv", null },
			{ ".bw", "whois.nic.net.bw" },
			{ ".by", "whois.cctld.by" },
			{ ".bz", null },
			{ ".bzh", "whois-bzh.nic.fr" },
			{ ".ca", "whois.cira.ca" },
			{ ".cab", "whois.donuts.co" },
			{ ".cal", "domain-registry-whois.l.google.com" },
			{ ".camera", "whois.donuts.co" },
			{ ".camp", "whois.donuts.co" },
			{ ".cancerresearch", "whois.nic.cancerresearch" },
			{ ".capetown", "capetown-whois.registry.net.za" },
			{ ".capital", "whois.donuts.co" },
			{ ".caravan", null },
			{ ".cards", "whois.donuts.co" },
			{ ".care", "whois.donuts.co" },
			{ ".career", "whois.nic.career" },
			{ ".careers", "whois.donuts.co" },
			{ ".casa", "whois-dub.mm-registry.com" },
			{ ".cash", "whois.donuts.co" },
			{ ".cat", "whois.cat" },
			{ ".catering", "whois.donuts.co" },
			{ ".cc", "ccwhois.verisign-grs.com" },
			{ ".cd", null },
			{ ".center", "whois.donuts.co" },
			{ ".ceo", "whois.nic.ceo" },
			{ ".cern", "whois.afilias-srs.net" },
			{ ".cf", "whois.dot.cf" },
			{ ".cg", null },
			{ ".ch", "whois.nic.ch" },
			{ ".channel", "domain-registry-whois.l.google.com" },
			{ ".cheap", "whois.donuts.co" },
			{ ".christmas", "whois.uniregistry.net" },
			{ ".chrome", "domain-registry-whois.l.google.com" },
			{ ".church", "whois.donuts.co" },
			{ ".ci", "whois.nic.ci" },
			{ ".citic", null },
			{ ".city", "whois.donuts.co" },
			{ ".ck", null },
			{ ".cl", "whois.nic.cl" },
			{ ".claims", "whois.donuts.co" },
			{ ".cleaning", "whois.donuts.co" },
			{ ".click", "whois.uniregistry.net" },
			{ ".clinic", "whois.donuts.co" },
			{ ".clothing", "whois.donuts.co" },
			{ ".club", "whois.nic.club" },
			{ ".cm", null },
			{ ".cn", "whois.cnnic.cn" },
			{ ".co", "whois.nic.co" },
			{ ".codes", "whois.donuts.co" },
			{ ".coffee", "whois.donuts.co" },
			{ ".college", "whois.centralnic.com" },
			{ ".cologne", "whois-fe1.pdt.cologne.tango.knipp.de" },
			{ ".com", "whois.verisign-grs.com" },
			{ ".community", "whois.donuts.co" },
			{ ".company", "whois.donuts.co" },
			{ ".computer", "whois.donuts.co" },
			{ ".condos", "whois.donuts.co" },
			{ ".construction", "whois.donuts.co" },
			{ ".consulting", "whois.unitedtld.com" },
			{ ".contractors", "whois.donuts.co" },
			{ ".cooking", "whois-dub.mm-registry.com" },
			{ ".cool", "whois.donuts.co" },
			{ ".coop", "whois.nic.coop" },
			{ ".country", "whois-dub.mm-registry.com" },
			{ ".cr", null },
			{ ".credit", "whois.donuts.co" },
			{ ".creditcard", "whois.donuts.co" },
			{ ".cruises", "whois.donuts.co" },
			{ ".cu", null },
			{ ".cuisinella", "whois.nic.cuisinella" },
			{ ".cv", null },
			{ ".cw", null },
			{ ".cx", "whois.nic.cx" },
			{ ".cy", null },
			{ ".cymru", "whois.nic.cymru" },
			{ ".cz", "whois.nic.cz" },
			{ ".dad", "domain-registry-whois.l.google.com" },
			{ ".dance", "whois.unitedtld.com" },
			{ ".dating", "whois.donuts.co" },
			{ ".day", "domain-registry-whois.l.google.com" },
			{ ".de", "whois.denic.de" },
			{ ".deals", "whois.donuts.co" },
			{ ".degree", "whois.rightside.co" },
			{ ".democrat", "whois.unitedtld.com" },
			{ ".dental", "whois.donuts.co" },
			{ ".dentist", "whois.rightside.co" },
			{ ".desi", "whois.ksregistry.net" },
			{ ".diamonds", "whois.donuts.co" },
			{ ".diet", "whois.uniregistry.net" },
			{ ".digital", "whois.donuts.co" },
			{ ".direct", "whois.donuts.co" },
			{ ".directory", "whois.donuts.co" },
			{ ".discount", "whois.donuts.co" },
			{ ".dj", null },
			{ ".dk", "whois.dk-hostmaster.dk" },
			{ ".dm", "whois.nic.dm" },
			{ ".dnp", null },
			{ ".do", null },
			{ ".domains", "whois.donuts.co" },
			{ ".durban", "durban-whois.registry.net.za" },
			{ ".dvag", "whois.ksregistry.net" },
			{ ".dz", "whois.nic.dz" },
			{ ".eat", "domain-registry-whois.l.google.com" },
			{ ".ec", "whois.nic.ec" },
			{ ".edu", "whois.educause.edu" },
			{ ".education", "whois.donuts.co" },
			{ ".ee", "whois.tld.ee" },
			{ ".eg", null },
			{ ".eh", null },
			{ ".email", "whois.donuts.co" },
			{ ".engineer", "whois.rightside.co" },
			{ ".engineering", "whois.donuts.co" },
			{ ".enterprises", "whois.donuts.co" },
			{ ".equipment", "whois.donuts.co" },
			{ ".er", null },
			{ ".es", "whois.nic.es" },
			{ ".esq", "domain-registry-whois.l.google.com" },
			{ ".estate", "whois.donuts.co" },
			{ ".et", null },
			{ ".eu", "whois.eu" },
			{ ".eus", "whois.eus.coreregistry.net" },
			{ ".events", "whois.donuts.co" },
			{ ".exchange", "whois.donuts.co" },
			{ ".expert", "whois.donuts.co" },
			{ ".exposed", "whois.donuts.co" },
			{ ".fail", "whois.donuts.co" },
			{ ".farm", "whois.donuts.co" },
			{ ".feedback", "whois.centralnic.com" },
			{ ".fi", "whois.fi" },
			{ ".finance", "whois.donuts.co" },
			{ ".financial", "whois.donuts.co" },
			{ ".fish", "whois.donuts.co" },
			{ ".fishing", "whois-dub.mm-registry.com" },
			{ ".fitness", "whois.donuts.co" },
			{ ".fj", null },
			{ ".fk", null },
			{ ".flights", "whois.donuts.co" },
			{ ".florist", "whois.donuts.co" },
			{ ".fly", "domain-registry-whois.l.google.com" },
			{ ".fm", null },
			{ ".fo", "whois.nic.fo" },
			{ ".foo", "domain-registry-whois.l.google.com" },
			{ ".forsale", "whois.unitedtld.com" },
			{ ".foundation", "whois.donuts.co" },
			{ ".fr", "whois.nic.fr" },
			{ ".frl", "whois.nic.frl" },
			{ ".frogans", "whois-frogans.nic.fr" },
			{ ".fund", "whois.donuts.co" },
			{ ".furniture", "whois.donuts.co" },
			{ ".futbol", "whois.unitedtld.com" },
			{ ".ga", null },
			{ ".gal", "whois.gal.coreregistry.net" },
			{ ".gallery", "whois.donuts.co" },
			{ ".gb", null },
			{ ".gbiz", "domain-registry-whois.l.google.com" },
			{ ".gd", "whois.nic.gd" },
			{ ".ge", null },
			{ ".gent", "whois.nic.gent" },
			{ ".gf", null },
			{ ".gg", "whois.gg" },
			{ ".gh", null },
			{ ".gi", "whois2.afilias-grs.net" },
			{ ".gift", "whois.uniregistry.net" },
			{ ".gifts", "whois.donuts.co" },
			{ ".gives", "whois.rightside.co" },
			{ ".gl", "whois.nic.gl" },
			{ ".glass", "whois.donuts.co" },
			{ ".gle", "domain-registry-whois.l.google.com" },
			{ ".global", "whois.afilias-srs.net" },
			{ ".globo", "whois.gtlds.nic.br" },
			{ ".gm", null },
			{ ".gmail", "domain-registry-whois.l.google.com" },
			{ ".gmo", null },
			{ ".gmx", "whois-fe1.gmx.tango.knipp.de" },
			{ ".gn", null },
			{ ".google", "domain-registry-whois.l.google.com" },
			{ ".gop", "whois-cl01.mm-registry.com" },
			{ ".gov", "whois.dotgov.gov" },
			{ ".gp", null },
			{ ".gq", "whois.dominio.gq" },
			{ ".gr", null },
			{ ".graphics", "whois.donuts.co" },
			{ ".gratis", "whois.donuts.co" },
			{ ".green", "whois.afilias.net" },
			{ ".gripe", "whois.donuts.co" },
			{ ".gs", "whois.nic.gs" },
			{ ".gt", null },
			{ ".gu", null },
			{ ".guide", "whois.donuts.co" },
			{ ".guitars", "whois.uniregistry.net" },
			{ ".guru", "whois.donuts.co" },
			{ ".gw", null },
			{ ".gy", "whois.registry.gy" },
			{ ".hamburg", "whois.nic.hamburg" },
			{ ".haus", "whois.unitedtld.com" },
			{ ".healthcare", "whois.donuts.co" },
			{ ".help", "whois.uniregistry.net" },
			{ ".here", "domain-registry-whois.l.google.com" },
			{ ".hiphop", "whois.uniregistry.net" },
			{ ".hiv", "whois.afilias-srs.net" },
			{ ".hk", "whois.hkirc.hk" },
			{ ".hm", null },
			{ ".hn", "whois.nic.hn" },
			{ ".holdings", "whois.donuts.co" },
			{ ".holiday", "whois.donuts.co" },
			{ ".homes", "whois.afilias-srs.net" },
			{ ".horse", "whois-dub.mm-registry.com" },
			{ ".host", "whois.nic.host" },
			{ ".hosting", "whois.uniregistry.net" },
			{ ".house", "whois.donuts.co" },
			{ ".how", "domain-registry-whois.l.google.com" },
			{ ".hr", "whois.dns.hr" },
			{ ".ht", "whois.nic.ht" },
			{ ".hu", "whois.nic.hu" },
			{ ".ibm", "whois.nic.ibm" },
			{ ".id", "whois.pandi.or.id" },
			{ ".ie", "whois.domainregistry.ie" },
			{ ".il", "whois.isoc.org.il" },
			{ ".im", "whois.nic.im" },
			{ ".immo", "whois.donuts.co" },
			{ ".immobilien", "whois.unitedtld.com" },
			{ ".in", "whois.inregistry.net" },
			{ ".industries", "whois.donuts.co" },
			{ ".info", "whois.afilias.net" },
			{ ".ing", "domain-registry-whois.l.google.com" },
			{ ".ink", "whois.centralnic.com" },
			{ ".institute", "whois.donuts.co" },
			{ ".insure", "whois.donuts.co" },
			{ ".int", "whois.iana.org" },
			{ ".international", "whois.donuts.co" },
			{ ".investments", "whois.donuts.co" },
			{ ".io", "whois.nic.io" },
			{ ".iq", "whois.cmc.iq" },
			{ ".ir", "whois.nic.ir" },
			{ ".is", "whois.isnic.is" },
			{ ".it", "whois.nic.it" },
			{ ".je", "whois.je" },
			{ ".jetzt", null },
			{ ".jm", null },
			{ ".jo", null },
			{ ".jobs", "jobswhois.verisign-grs.com" },
			{ ".joburg", "joburg-whois.registry.net.za" },
			{ ".jp", "whois.jprs.jp" },
			{ ".juegos", "whois.uniregistry.net" },
			{ ".kaufen", "whois.unitedtld.com" },
			{ ".ke", "whois.kenic.or.ke" },
			{ ".kg", "whois.domain.kg" },
			{ ".kh", null },
			{ ".ki", "whois.nic.ki" },
			{ ".kim", "whois.afilias.net" },
			{ ".kitchen", "whois.donuts.co" },
			{ ".kiwi", "whois.nic.kiwi" },
			{ ".km", null },
			{ ".kn", null },
			{ ".koeln", "whois-fe1.pdt.koeln.tango.knipp.de" },
			{ ".kp", null },
			{ ".kr", "whois.kr" },
			{ ".krd", "whois.aridnrs.net.au" },
			{ ".kred", null },
			{ ".kw", null },
			{ ".ky", null },
			{ ".kz", "whois.nic.kz" },
			{ ".la", "whois.nic.la" },
			{ ".lacaixa", "whois.nic.lacaixa" },
			{ ".land", "whois.donuts.co" },
			{ ".lawyer", "whois.rightside.co" },
			{ ".lb", null },
			{ ".lc", null },
			{ ".lease", "whois.donuts.co" },
			{ ".lgbt", "whois.afilias.net" },
			{ ".li", "whois.nic.li" },
			{ ".life", "whois.donuts.co" },
			{ ".lighting", "whois.donuts.co" },
			{ ".limited", "whois.donuts.co" },
			{ ".limo", "whois.donuts.co" },
			{ ".link", "whois.uniregistry.net" },
			{ ".lk", null },
			{ ".loans", "whois.donuts.co" },
			{ ".london", "whois-lon.mm-registry.com" },
			{ ".lotto", "whois.afilias.net" },
			{ ".lr", null },
			{ ".ls", null },
			{ ".lt", "whois.domreg.lt" },
			{ ".ltda", "whois.afilias-srs.net" },
			{ ".lu", "whois.dns.lu" },
			{ ".luxe", "whois-dub.mm-registry.com" },
			{ ".luxury", "whois.nic.luxury" },
			{ ".lv", "whois.nic.lv" },
			{ ".ly", "whois.nic.ly" },
			{ ".ma", "whois.iam.net.ma" },
			{ ".maison", "whois.donuts.co" },
			{ ".management", "whois.donuts.co" },
			{ ".mango", "whois.mango.coreregistry.net" },
			{ ".market", "whois.rightside.co" },
			{ ".marketing", "whois.donuts.co" },
			{ ".mc", null },
			{ ".md", "whois.nic.md" },
			{ ".me", "whois.nic.me" },
			{ ".media", "whois.donuts.co" },
			{ ".meet", "whois.afilias.net" },
			{ ".melbourne", "whois.aridnrs.net.au" },
			{ ".meme", "domain-registry-whois.l.google.com" },
			{ ".menu", "whois.nic.menu" },
			{ ".mf", null },
			{ ".mg", "whois.nic.mg" },
			{ ".mh", null },
			{ ".miami", "whois-dub.mm-registry.com" },
			{ ".mil", null },
			{ ".mini", "whois.ksregistry.net" },
			{ ".mk", "whois.marnet.mk" },
			{ ".ml", "whois.dot.ml" },
			{ ".mm", null },
			{ ".mn", "whois.nic.mn" },
			{ ".mo", "whois.monic.mo" },
			{ ".mobi", "whois.dotmobiregistry.net" },
			{ ".moda", "whois.unitedtld.com" },
			{ ".moe", null },
			{ ".monash", "whois.nic.monash" },
			{ ".mortgage", "whois.rightside.co" },
			{ ".moscow", "whois.nic.moscow" },
			{ ".motorcycles", "whois.afilias-srs.net" },
			{ ".mov", "domain-registry-whois.l.google.com" },
			{ ".mp", "whois.nic.mp" },
			{ ".mq", null },
			{ ".mr", null },
			{ ".ms", "whois.nic.ms" },
			{ ".mt", null },
			{ ".mu", "whois.nic.mu" },
			{ ".museum", "whois.museum" },
			{ ".mv", null },
			{ ".mw", null },
			{ ".mx", "whois.mx" },
			{ ".my", "whois.mynic.my" },
			{ ".mz", "whois.nic.mz" },
			{ ".na", "whois.na-nic.com.na" },
			{ ".nagoya", null },
			{ ".name", "whois.nic.name" },
			{ ".navy", "whois.rightside.co" },
			{ ".nc", "whois.nc" },
			{ ".ne", null },
			{ ".net", "whois.verisign-grs.com" },
			{ ".network", "whois.donuts.co" },
			{ ".neustar", null },
			{ ".new", "domain-registry-whois.l.google.com" },
			{ ".nexus", "domain-registry-whois.l.google.com" },
			{ ".nf", "whois.nic.nf" },
			{ ".ng", "whois.nic.net.ng" },
			{ ".ngo", "whois.publicinterestregistry.net" },
			{ ".nhk", null },
			{ ".ni", null },
			{ ".ninja", "whois.unitedtld.com" },
			{ ".nl", "whois.domain-registry.nl" },
			{ ".no", "whois.norid.no" },
			{ ".np", null },
			{ ".nr", null },
			{ ".nra", "whois.afilias-srs.net" },
			{ ".nrw", "whois.nic.nrw" },
			{ ".nu", "whois.iis.nu" },
			{ ".nyc", null },
			{ ".nz", "whois.srs.net.nz" },
			{ ".okinawa", null },
			{ ".om", "whois.registry.om" },
			{ ".ong", "whois.publicinterestregistry.net" },
			{ ".onl", "whois.afilias-srs.net" },
			{ ".ooo", "whois.nic.ooo" },
			{ ".org", "whois.pir.org" },
			{ ".organic", "whois.afilias.net" },
			{ ".otsuka", null },
			{ ".ovh", "whois-ovh.nic.fr" },
			{ ".pa", null },
			{ ".paris", "whois-paris.nic.fr" },
			{ ".partners", "whois.donuts.co" },
			{ ".parts", "whois.donuts.co" },
			{ ".pe", "kero.yachay.pe" },
			{ ".pf", "whois.registry.pf" },
			{ ".pg", null },
			{ ".ph", null },
			{ ".pharmacy", null },
			{ ".photo", "whois.uniregistry.net" },
			{ ".photography", "whois.donuts.co" },
			{ ".photos", "whois.donuts.co" },
			{ ".physio", "whois.nic.physio" },
			{ ".pics", "whois.uniregistry.net" },
			{ ".pictures", "whois.donuts.co" },
			{ ".pink", "whois.afilias.net" },
			{ ".pizza", "whois.donuts.co" },
			{ ".pk", null },
			{ ".pl", "whois.dns.pl" },
			{ ".place", "whois.donuts.co" },
			{ ".plumbing", "whois.donuts.co" },
			{ ".pm", "whois.nic.pm" },
			{ ".pn", null },
			{ ".pohl", "whois.ksregistry.net" },
			{ ".post", "whois.dotpostregistry.net" },
			{ ".pr", "whois.nic.pr" },
			{ ".praxi", null },
			{ ".press", "whois.nic.press" },
			{ ".pro", "whois.dotproregistry.net" },
			{ ".prod", "domain-registry-whois.l.google.com" },
			{ ".productions", "whois.donuts.co" },
			{ ".prof", "domain-registry-whois.l.google.com" },
			{ ".properties", "whois.donuts.co" },
			{ ".property", "whois.uniregistry.net" },
			{ ".ps", null },
			{ ".pt", "whois.dns.pt" },
			{ ".pub", "whois.unitedtld.com" },
			{ ".pw", "whois.nic.pw" },
			{ ".py", null },
			{ ".qa", "whois.registry.qa" },
			{ ".qpon", null },
			{ ".quebec", "whois.quebec.rs.corenic.net" },
			{ ".re", "whois.nic.re" },
			{ ".realtor", null },
			{ ".recipes", "whois.donuts.co" },
			{ ".red", "whois.afilias.net" },
			{ ".rehab", "whois.rightside.co" },
			{ ".reise", "whois.nic.reise" },
			{ ".reisen", "whois.donuts.co" },
			{ ".ren", null },
			{ ".rentals", "whois.donuts.co" },
			{ ".repair", "whois.donuts.co" },
			{ ".report", "whois.donuts.co" },
			{ ".republican", "whois.rightside.co" },
			{ ".rest", "whois.centralnic.com" },
			{ ".restaurant", "whois.donuts.co" },
			{ ".reviews", "whois.unitedtld.com" },
			{ ".rich", "whois.afilias-srs.net" },
			{ ".rio", "whois.gtlds.nic.br" },
			{ ".ro", "whois.rotld.ro" },
			{ ".rocks", "whois.unitedtld.com" },
			{ ".rodeo", "whois-dub.mm-registry.com" },
			{ ".rs", "whois.rnids.rs" },
			{ ".rsvp", "domain-registry-whois.l.google.com" },
			{ ".ru", "whois.tcinet.ru" },
			{ ".ruhr", "whois.nic.ruhr" },
			{ ".rw", null },
			{ ".ryukyu", null },
			{ ".sa", "whois.nic.net.sa" },
			{ ".saarland", "whois.ksregistry.net" },
			{ ".sarl", "whois.donuts.co" },
			{ ".sb", "whois.nic.net.sb" },
			{ ".sc", "whois2.afilias-grs.net" },
			{ ".sca", "whois.nic.sca" },
			{ ".scb", "whois.nic.scb" },
			{ ".schmidt", "whois.nic.schmidt" },
			{ ".schule", "whois.donuts.co" },
			{ ".scot", "whois.scot.coreregistry.net" },
			{ ".sd", null },
			{ ".se", "whois.iis.se" },
			{ ".services", "whois.donuts.co" },
			{ ".sexy", "whois.uniregistry.net" },
			{ ".sg", "whois.sgnic.sg" },
			{ ".sh", "whois.nic.sh" },
			{ ".shiksha", "whois.afilias.net" },
			{ ".shoes", "whois.donuts.co" },
			{ ".si", "whois.arnes.si" },
			{ ".singles", "whois.donuts.co" },
			{ ".sj", null },
			{ ".sk", "whois.sk-nic.sk" },
			{ ".sl", null },
			{ ".sm", "whois.nic.sm" },
			{ ".sn", "whois.nic.sn" },
			{ ".so", "whois.nic.so" },
			{ ".social", "whois.unitedtld.com" },
			{ ".software", "whois.rightside.co" },
			{ ".sohu", null },
			{ ".solar", "whois.donuts.co" },
			{ ".solutions", "whois.donuts.co" },
			{ ".soy", "domain-registry-whois.l.google.com" },
			{ ".space", "whois.nic.space" },
			{ ".spiegel", "whois.ksregistry.net" },
			{ ".sr", null },
			{ ".ss", null },
			{ ".st", "whois.nic.st" },
			{ ".su", "whois.tcinet.ru" },
			{ ".supplies", "whois.donuts.co" },
			{ ".supply", "whois.donuts.co" },
			{ ".support", "whois.donuts.co" },
			{ ".surf", "whois-dub.mm-registry.com" },
			{ ".surgery", "whois.donuts.co" },
			{ ".suzuki", null },
			{ ".sv", null },
			{ ".sx", "whois.sx" },
			{ ".sy", "whois.tld.sy" },
			{ ".systems", "whois.donuts.co" },
			{ ".sz", null },
			{ ".tatar", "whois.nic.tatar" },
			{ ".tattoo", "whois.uniregistry.net" },
			{ ".tax", "whois.donuts.co" },
			{ ".tc", "whois.meridiantld.net" },
			{ ".td", null },
			{ ".technology", "whois.donuts.co" },
			{ ".tel", "whois.nic.tel" },
			{ ".tf", "whois.nic.tf" },
			{ ".tg", null },
			{ ".th", "whois.thnic.co.th" },
			{ ".tienda", "whois.donuts.co" },
			{ ".tips", "whois.donuts.co" },
			{ ".tirol", "whois.nic.tirol" },
			{ ".tj", null },
			{ ".tk", "whois.dot.tk" },
			{ ".tl", "whois.nic.tl" },
			{ ".tm", "whois.nic.tm" },
			{ ".tn", "whois.ati.tn" },
			{ ".to", "whois.tonic.to" },
			{ ".today", "whois.donuts.co" },
			{ ".tokyo", null },
			{ ".tools", "whois.donuts.co" },
			{ ".top", "whois.nic.top" },
			{ ".town", "whois.donuts.co" },
			{ ".toys", "whois.donuts.co" },
			{ ".tp", null },
			{ ".tr", "whois.nic.tr" },
			{ ".trade", null },
			{ ".training", "whois.donuts.co" },
			{ ".travel", "whois.nic.travel" },
			{ ".tt", null },
			{ ".tui", "whois.ksregistry.net" },
			{ ".tv", "tvwhois.verisign-grs.com" },
			{ ".tw", "whois.twnic.net.tw" },
			{ ".tz", "whois.tznic.or.tz" },
			{ ".ua", "whois.ua" },
			{ ".ug", "whois.co.ug" },
			{ ".uk", "whois.nic.uk" },
			{ ".um", null },
			{ ".university", "whois.donuts.co" },
			{ ".uno", null },
			{ ".uol", "whois.gtlds.nic.br" },
			{ ".us", "whois.nic.us" },
			{ ".uy", "whois.nic.org.uy" },
			{ ".uz", "whois.cctld.uz" },
			{ ".va", null },
			{ ".vacations", "whois.donuts.co" },
			{ ".vc", "whois2.afilias-grs.net" },
			{ ".ve", "whois.nic.ve" },
			{ ".vegas", "whois.afilias-srs.net" },
			{ ".ventures", "whois.donuts.co" },
			{ ".versicherung", "whois.nic.versicherung" },
			{ ".vet", "whois.rightside.co" },
			{ ".vg", "ccwhois.ksregistry.net" },
			{ ".vi", null },
			{ ".viajes", "whois.donuts.co" },
			{ ".villas", "whois.donuts.co" },
			{ ".vision", "whois.donuts.co" },
			{ ".vlaanderen", "whois.nic.vlaanderen" },
			{ ".vn", null },
			{ ".vodka", "whois-dub.mm-registry.com" },
			{ ".vote", "whois.afilias.net" },
			{ ".voting", "whois.voting.tld-box.at" },
			{ ".voto", "whois.afilias.net" },
			{ ".voyage", "whois.donuts.co" },
			{ ".vu", "vunic.vu" },
			{ ".wales", "whois.nic.wales" },
			{ ".wang", "whois.gtld.knet.cn" },
			{ ".watch", "whois.donuts.co" },
			{ ".webcam", null },
			{ ".website", "whois.nic.website" },
			{ ".wed", "whois.nic.wed" },
			{ ".wf", "whois.nic.wf" },
			{ ".whoswho", null },
			{ ".wien", "whois.nic.wien" },
			{ ".wiki", "whois.nic.wiki" },
			{ ".williamhill", null },
			{ ".wme", "whois.centralnic.com" },
			{ ".work", "whois-dub.mm-registry.com" },
			{ ".works", "whois.donuts.co" },
			{ ".world", "whois.donuts.co" },
			{ ".ws", "whois.website.ws" },
			{ ".wtc", "whois.nic.wtc" },
			{ ".wtf", "whois.donuts.co" },
			{ ".xn--0zwm56d", null },
			{ ".xn--11b5bs3a9aj6g", null },
			{ ".xn--1qqw23a", "whois.ngtld.cn" },
			{ ".xn--3bst00m", "whois.gtld.knet.cn" },
			{ ".xn--3ds443g", "whois.afilias-srs.net" },
			{ ".xn--3e0b707e", "whois.kr" },
			{ ".xn--45brj9c", null },
			{ ".xn--4gbrim", "whois.afilias-srs.net" },
			{ ".xn--54b7fta0cc", null },
			{ ".xn--55qw42g", "whois.conac.cn" },
			{ ".xn--55qx5d", "whois.ngtld.cn" },
			{ ".xn--6frz82g", "whois.afilias.net" },
			{ ".xn--6qq986b3xl", "whois.gtld.knet.cn" },
			{ ".xn--80adxhks", "whois.nic.xn--80adxhks" },
			{ ".xn--80akhbyknj4f", null },
			{ ".xn--80ao21a", "whois.nic.kz" },
			{ ".xn--80asehdb", "whois.online.rs.corenic.net" },
			{ ".xn--80aswg", "whois.site.rs.corenic.net" },
			{ ".xn--90a3ac", null },
			{ ".xn--90ais", null },
			{ ".xn--9t4b11yi5a", null },
			{ ".xn--c1avg", "whois.publicinterestregistry.net" },
			{ ".xn--cg4bki", "whois.kr" },
			{ ".xn--clchc0ea0b2g2a9gcd", "whois.sgnic.sg" },
			{ ".xn--czr694b", null },
			{ ".xn--czru2d", "whois.gtld.knet.cn" },
			{ ".xn--d1acj3b", "whois.nic.xn--d1acj3b" },
			{ ".xn--d1alf", null },
			{ ".xn--deba0ad", null },
			{ ".xn--fiq228c5hs", "whois.afilias-srs.net" },
			{ ".xn--fiq64b", "whois.gtld.knet.cn" },
			{ ".xn--fiqs8s", "cwhois.cnnic.cn" },
			{ ".xn--fiqz9s", "cwhois.cnnic.cn" },
			{ ".xn--fpcrj9c3d", null },
			{ ".xn--fzc2c9e2c", null },
			{ ".xn--g6w251d", null },
			{ ".xn--gecrj9c", null },
			{ ".xn--h2brj9c", null },
			{ ".xn--hgbk6aj7f53bba", null },
			{ ".xn--hlcj6aya9esc7a", null },
			{ ".xn--i1b6b1a6a2e", "whois.publicinterestregistry.net" },
			{ ".xn--io0a7i", "whois.ngtld.cn" },
			{ ".xn--j1amh", "whois.dotukr.com" },
			{ ".xn--j6w193g", "whois.hkirc.hk" },
			{ ".xn--jxalpdlp", null },
			{ ".xn--kgbechtv", null },
			{ ".xn--kprw13d", "whois.twnic.net.tw" },
			{ ".xn--kpry57d", "whois.twnic.net.tw" },
			{ ".xn--kput3i", "whois.afilias-srs.net" },
			{ ".xn--l1acc", null },
			{ ".xn--lgbbat1ad8j", "whois.nic.dz" },
			{ ".xn--mgb9awbf", "whois.registry.om" },
			{ ".xn--mgba3a4f16a", "whois.nic.ir" },
			{ ".xn--mgbaam7a8h", "whois.aeda.net.ae" },
			{ ".xn--mgbab2bd", "whois.bazaar.coreregistry.net" },
			{ ".xn--mgbai9azgqp6j", null },
			{ ".xn--mgbayh7gpa", null },
			{ ".xn--mgbbh1a71e", null },
			{ ".xn--mgbc0a9azcg", null },
			{ ".xn--mgberp4a5d4ar", "whois.nic.net.sa" },
			{ ".xn--mgbpl2fh", null },
			{ ".xn--mgbtx2b", null },
			{ ".xn--mgbx4cd0ab", "whois.mynic.my" },
			{ ".xn--ngbc5azd", "whois.nic.xn--ngbc5azd" },
			{ ".xn--node", null },
			{ ".xn--nqv7f", "whois.publicinterestregistry.net" },
			{ ".xn--nqv7fs00ema", "whois.publicinterestregistry.net" },
			{ ".xn--o3cw4h", "whois.thnic.co.th" },
			{ ".xn--ogbpf8fl", "whois.tld.sy" },
			{ ".xn--p1acf", "whois.nic.xn--p1acf" },
			{ ".xn--p1ai", "whois.tcinet.ru" },
			{ ".xn--pgbs0dh", null },
			{ ".xn--q9jyb4c", "domain-registry-whois.l.google.com" },
			{ ".xn--rhqv96g", null },
			{ ".xn--s9brj9c", null },
			{ ".xn--ses554g", null },
			{ ".xn--unup4y", "whois.donuts.co" },
			{ ".xn--vermgensberater-ctb", "whois.ksregistry.net" },
			{ ".xn--vermgensberatung-pwb", "whois.ksregistry.net" },
			{ ".xn--vhquv", "whois.donuts.co" },
			{ ".xn--wgbh1c", null },
			{ ".xn--wgbl6a", "whois.registry.qa" },
			{ ".xn--xhq521b", "whois.ngtld.cn" },
			{ ".xn--xkc2al3hye2a", null },
			{ ".xn--xkc2dl3a5ee0h", null },
			{ ".xn--yfro4i67o", "whois.sgnic.sg" },
			{ ".xn--ygbi2ammx", "whois.pnina.ps" },
			{ ".xn--zckzah", null },
			{ ".xn--zfr164b", "whois.conac.cn" },
			{ ".xxx", "whois.nic.xxx" },
			{ ".xyz", "whois.nic.xyz" },
			{ ".yachts", "whois.afilias-srs.net" },
			{ ".yandex", null },
			{ ".ye", null },
			{ ".yokohama", null },
			{ ".youtube", "domain-registry-whois.l.google.com" },
			{ ".yt", "whois.nic.yt" },
			{ ".za", null },
			{ ".zip", "domain-registry-whois.l.google.com" },
			{ ".zm", "whois.nic.zm" },
			{ ".zone", "whois.donuts.co" },
			{ ".zw", null }
		};
	}
}
