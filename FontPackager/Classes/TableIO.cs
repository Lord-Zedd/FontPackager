using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FontPackager.Classes
{
	/// <summary>
	/// Handles reading and writing of font_table files and their extensionless fonts.
	/// </summary>
	public static class TableIO
	{
		const uint FontVersionLoose = 0xF0000001;

		/// <summary>
		/// Reads an H2-era extensionless font file.
		/// </summary>
		/// <param name="path">The path to the file to be read.</param>
		public static Tuple<IOError, FileFormat, BlamFont> ReadLooseFile(string path)
		{
			List<int> CharacterPointers = new List<int>();
			List<BlamCharacter> CharacterEntries = new List<BlamCharacter>();

			using (FileStream fs = new FileStream(path, FileMode.Open))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					br.BaseStream.Position = 0x200;

					FileFormat fmt = FileFormat.Table;

					var fversion = br.ReadUInt32();
					if (fversion != FontVersionLoose)
					{
						return new Tuple<IOError, FileFormat, BlamFont>
							(IOError.BadVersion, 0, null);
					}

					BlamFont font = new BlamFont(Path.GetFileName(path)); //will need updates when we get h2 mcc

					font.AscendHeight = br.ReadInt16();
					font.DescendHeight = br.ReadInt16();
					font.LeadHeight = br.ReadInt16();
					font.LeadWidth = br.ReadInt16();
					int CharacterCount = br.ReadInt32();
					font.Unknown7 = br.ReadInt32();
					font.Unknown8 = br.ReadInt32();
					int CompressedSize = br.ReadInt32();
					int DecompressedSize = br.ReadInt32();

					int paircount = br.ReadInt32();

					for (int i = 0; i < paircount; i++)
						font.KerningPairs.Add(new KerningPair(br.ReadByte(), br.ReadByte(), br.ReadInt16()));

					font.KerningPairs = font.KerningPairs.OrderBy(x => x.Character).ThenBy(x => x.TargetCharacter).ToList();

					int unk = br.ReadInt32();
					int unk2 = br.ReadInt32();

					font.UnknownL1 = br.ReadInt32();
					font.UnknownL2 = br.ReadInt32();

					int characterdataoffset = 0x40400 + (CharacterCount * 0x10);

					fs.Position = characterdataoffset;
					byte[] data = br.ReadBytes((int)fs.Length - characterdataoffset);

					for (int i = 0; i < CharacterCount; i++)
					{
						BlamCharacter bc = new BlamCharacter(0);

						fs.Position = 0x40400 + (i * 0x10);

						int datalength = 0;
						if (fmt.HasFlag(FileFormat.x64Char))
						{
							bc.DisplayWidth = br.ReadUInt32();
							datalength = br.ReadInt32();
						}
						else
						{
							bc.DisplayWidth = br.ReadUInt16();
							datalength = br.ReadUInt16();
						}

						bc.Width = br.ReadUInt16();
						bc.Height = br.ReadUInt16();
						bc.OriginX = br.ReadInt16();
						bc.OriginY = br.ReadInt16();

						uint dataoffset = br.ReadUInt32();
						byte[] chardata = new byte[datalength];
						Array.Copy(data, dataoffset - characterdataoffset, chardata, 0, datalength);

						bc.CompressedData = chardata;

						CharacterEntries.Add(bc);
					}

					int firstpointer = -1;

					for (int i = 0; i < 65536; i++)
					{
						br.BaseStream.Position = 0x400 + (i * 4);

						int charindex = br.ReadInt32();

						if (i == 0)
							firstpointer = charindex;
						else
						{
							if (charindex == firstpointer)
								continue;
							else
								CharacterPointers.Add(charindex);
						}

						CharacterEntries[charindex].UnicIndex = (ushort)i;

						font.Characters.Add(CharacterEntries[charindex]);

					}

					return new Tuple<IOError, FileFormat, BlamFont>
							(IOError.None, fmt, font);
				}
			}
		}

		/// <summary>
		/// Collects and reads all H2-era extensionless font files from a directory.
		/// </summary>
		/// <param name="path">The path to a folder, font_table.txt, or individual font file.</param>
		public static Tuple<IOError, List<BlamFont>> ReadDirectory(string path)
		{
			List<BlamFont> fonts = new List<BlamFont>();

			string[] files = Directory.GetFiles(path);

			foreach (string file in files)
			{
				if (!Path.HasExtension(file))
				{
					var res = ReadLooseFile(file);
					if (res.Item1 == IOError.None)
					{
						fonts.Add(res.Item3);
					}
				}
			}

			if (fonts.Count == 0)
				return new Tuple<IOError, List<BlamFont>>
							(IOError.Empty, null);


			return new Tuple<IOError, List<BlamFont>>
						(IOError.None, fonts);
		}

		/// <summary>
		/// Collects and reads all H2-era extensionless font files and ordering from a font_table.txt file.
		/// </summary>
		/// <param name="path">The path to a folder, font_table.txt, or individual font file.</param>
		public static Tuple<IOError, FileFormat, List<BlamFont>, List<int>> ReadTable(string path)
		{
			List<BlamFont> fonts = new List<BlamFont>();
			List<int> orders = new List<int>();
			FileFormat fmt = 0;

			List<string> readfiles = new List<string>();

			string[] listfonts = File.ReadAllLines(path);
			string basepath = Path.GetDirectoryName(path);

			for (int i = 0; i < listfonts.Length; i++)
			{
				if (readfiles.Contains(listfonts[i]))
				{
					orders.Add(readfiles.IndexOf(listfonts[i]));
					continue;
				}

				if (File.Exists(basepath + "\\" + listfonts[i]))
				{
					var res = ReadLooseFile(basepath + "\\" + listfonts[i]);
					if (res.Item1 == IOError.None)
					{
						orders.Add(fonts.Count);
						fonts.Add(res.Item3);
						if (fmt == 0)
							fmt = res.Item2;
					}
					else if (res.Item1 == IOError.BadVersion)
						return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
							(IOError.BadVersion, 0, null, null);

					readfiles.Add(listfonts[i]);
				}
			}

			if (fonts.Count == 0)
				return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
							(IOError.Empty, 0, null, null);


			return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
						(IOError.None, fmt, fonts, orders);
		}

		/// <summary>
		/// Creates or overwrites an individual H2-era extensionless font file.
		/// </summary>
		/// <param name="font">The font to write.</param>
		/// <param name="path">The path to the font file to be created.</param>
		/// <param name="format">The target format.</param>
		public static void WriteLooseFile(BlamFont font, string path, FileFormat format)
		{
			int compsize = 0;
			int uncompsize = 0;

			byte[] TailOutput;
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					byte[] indextable = new byte[0x40000];
					MemoryStream ims = new MemoryStream(indextable);
					BinaryWriter ibw = new BinaryWriter(ims);

					MemoryStream cms = new MemoryStream();
					BinaryWriter cbw = new BinaryWriter(cms);

					MemoryStream dms = new MemoryStream();
					BinaryWriter dbw = new BinaryWriter(dms);

					int datastart = 0x40400 + (font.Characters.Count * 0x10);

					for (int i = 0; i < font.Characters.Count; i++)
					{
						BlamCharacter bc = font.Characters[i];

						compsize += bc.CompressedSize;
						uncompsize += bc.DecompressedSize / 4;//8bpp

						ims.Position = (bc.UnicIndex * 4);
						ibw.Write(i);

						if (format.HasFlag(FileFormat.x64Char))
						{
							cbw.Write(bc.DisplayWidth);
							cbw.Write(bc.CompressedSize);
						}
						else
						{
							cbw.Write((short)bc.DisplayWidth);
							cbw.Write((ushort)bc.CompressedSize);
						}
						
						cbw.Write(bc.Width);
						cbw.Write(bc.Height);
						cbw.Write(bc.OriginX);
						cbw.Write(bc.OriginY);
						cbw.Write((int)(datastart + dms.Length));

						dbw.Write(bc.CompressedData);
					}

					bw.Write(ims.ToArray());
					bw.Write(cms.ToArray());
					bw.Write(dms.ToArray());

					ibw.Dispose();
					cbw.Dispose();
					dbw.Dispose();

					TailOutput = ms.ToArray();

				}
			}

			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					fs.Position = 0x200;

					bw.Write(FontVersionLoose);

					bw.Write(font.AscendHeight);
					bw.Write(font.DescendHeight);
					bw.Write(font.LeadWidth);
					bw.Write(font.LeadHeight);
					bw.Write(font.Characters.Count);
					bw.Write(font.Unknown7);
					bw.Write(font.Unknown8);
					bw.Write(compsize);
					bw.Write(uncompsize);
					bw.Write(font.KerningPairs.Count);

					foreach(KerningPair kp in font.KerningPairs)
					{
						bw.Write(kp.Character);
						bw.Write(kp.TargetCharacter);
						bw.Write(kp.Value);
					}

					fs.Position += 8;
					bw.Write(font.UnknownL1);
					bw.Write(font.UnknownL2);

					fs.Position = 0x400;
					bw.Write(TailOutput);
				}
			}

		}

		/// <summary>
		/// Creates or overwrites a font_table.txt collection, and associated font files.
		/// </summary>
		/// <param name="fonts">The fonts to write.</param>
		/// <param name="orders">The engine order to write to font_table.txt.</param>
		/// <param name="path">The path to the font_table.txt to be written. (Fonts save to the same directory)</param>
		/// <param name="format">The target format.</param>
		public static void WriteTable(List<BlamFont> fonts, List<int> orders, string path, FileFormat format)
		{
			string dir = Path.GetDirectoryName(path);

			foreach(BlamFont f in fonts)
				WriteLooseFile(f, dir + "\\" +  f.SanitizedName, format);

			using (StringWriter sw = new StringWriter())
			{
				for (int i = 0; i < 12; i++)
				{
					if (orders[i] == -1 || orders[i] > fonts.Count)
						sw.WriteLine();
					else
						sw.WriteLine(fonts[orders[i]].SanitizedName);
				}
				
				File.WriteAllText(path, sw.ToString());
			}
		}

	}
}
