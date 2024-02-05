using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FontPackager.Classes
{
	/// <summary>
	/// Handles reading Halo CE tags, from either an Xbox cache or tag files.
	/// </summary>
	public static class TagIO
	{
		/// <summary>
		/// Reads font tags from an Xbox cache file belonging to Halo CE, Stubbs The Zombie, or Shadowrun.
		/// </summary>
		/// <param name="path">The path to the Xbox cache file to pull fonts from.</param>
		public static Tuple<IOError, List<BlamFont>> ReadCacheFile(string path)
		{
			List<BlamFont> fonts = new List<BlamFont>();

			FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fs);

			fs.Position = 4;
			int version = br.ReadInt32();

			if (version != 5)
			{
				br.Dispose();
				return new Tuple<IOError, List<BlamFont>>
					(IOError.BadVersion, null);
			}

			fs.Position = 0x800;

			byte compressedcheck = br.ReadByte();
			string tempdecompressedfile = null;

			if (compressedcheck == 0x78)
			{
				fs.Position = 0;
				byte[] header = br.ReadBytes(0x800);

				tempdecompressedfile = Path.GetTempFileName();

				using (FileStream ts = new FileStream(tempdecompressedfile, FileMode.Open, FileAccess.ReadWrite))
				{
					ts.Write(header, 0, header.Length);

					using (DeflateStream ds = new DeflateStream(fs, CompressionMode.Decompress))
					{
						fs.Position = 0x802;

						ds.CopyTo(ts);
					}	
				}

				br.Dispose();
				fs = new FileStream(tempdecompressedfile, FileMode.Open, FileAccess.Read);
				br = new BinaryReader(fs);
			}

			fs.Position = 0x10;
			int taglist = br.ReadInt32();

			fs.Position = taglist;
			int memtags = br.ReadInt32();

			fs.Position = taglist + 0xC;
			int tagcount = br.ReadInt32();

			int tagstart = (int)(fs.Position + 0x14);

			uint magic = (uint)(memtags - tagstart);

			for (int t = 0; t < tagcount; t++)
			{
				int entrybase = tagstart + t * 0x20;
				fs.Position = entrybase;

				fs.Position = entrybase;
				int shortnameint = br.ReadInt32();

				fs.Position = entrybase + 0x10;
				int stringaddr = br.ReadInt32();

				fs.Position = entrybase + 0x14;
				int address = br.ReadInt32();

				byte[] shortnamebytes = BitConverter.GetBytes(shortnameint);
				Array.Reverse(shortnamebytes);
				string shortname = Encoding.ASCII.GetString(shortnamebytes);

				if (shortname != "font")
					continue;

				fs.Position = stringaddr - (int)magic;
				string tagname = br.ReadStringToNull();


				int tagbase = (address - (int)magic);
				fs.Position = tagbase + 4;

				BlamFont font = new BlamFont(Path.GetFileName(tagname));

				font.AscendHeight = br.ReadInt16();
				font.DescendHeight = br.ReadInt16();
				font.LeadHeight = br.ReadInt16();
				font.LeadWidth = br.ReadInt16();

				fs.Position = tagbase + 0x7C;

				int charscount = br.ReadInt32();
				uint charsaddr = br.ReadUInt32();

				fs.Position += 0x4;

				int datasize = br.ReadInt32();
				fs.Position += 8;
				uint dataaddr = br.ReadUInt32();

				fs.Position = (dataaddr - (int)magic);
				byte[] data = br.ReadBytes(datasize);

				fs.Position = (charsaddr - (int)magic);

				font.Characters.AddRange(ReadCharacters(br, data, charscount));

				fonts.Add(font);
			}

			br.Dispose();

			if (tempdecompressedfile != null & File.Exists(tempdecompressedfile))
				File.Delete(tempdecompressedfile);

			if (fonts.Count == 0)
				return new Tuple<IOError, List<BlamFont>>
					(IOError.Empty, null);

			return new Tuple<IOError, List<BlamFont>>
				(IOError.None, fonts);
		}

		/// <summary>
		/// Reads font tags from Custom Edition/MCC.
		/// </summary>
		/// <param name="path">The path to the font tag file to read from.</param>
		public static Tuple<IOError, BlamFont> ReadTag(string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Open))
			{
				using (BigEndianReader br = new BigEndianReader(fs))
				{
					br.BaseStream.Position = 0x24;

					var tagGroup = br.ReadUInt32();
					if (tagGroup != 0x666F6E74)
					{
						return new Tuple<IOError, BlamFont>
							(IOError.BadVersion, null);
					}

					BlamFont font = new BlamFont(Path.GetFileNameWithoutExtension(path));

					br.BaseStream.Position = 0x40;

					uint flags = br.ReadUInt32();

					font.AscendHeight = br.ReadInt16();
					font.DescendHeight = br.ReadInt16();
					font.LeadHeight = br.ReadInt16();
					font.LeadWidth = br.ReadInt16();

					//character table appears to be dynamically created and not stored in the tag? lets skip

					br.BaseStream.Position = 0xBC;

					int CharacterCount = br.ReadInt32();
					br.BaseStream.Position += 8;

					int CompressedSize = br.ReadInt32();
					br.BaseStream.Position += 0x10;

					int charblockstart = (int)br.BaseStream.Position;
					int charblocklength = CharacterCount * 0x14;

					br.BaseStream.Position = charblockstart + charblocklength;

					byte[] data = br.ReadBytes(CompressedSize);

					br.BaseStream.Position = charblockstart;

					font.Characters.AddRange(ReadCharacters(br, data, CharacterCount));

					return new Tuple<IOError, BlamFont>
						(IOError.None, font);
				}
			}
		}

		/// <summary>
		/// Writes out a font tag for Custom Edition/MCC.
		/// </summary>
		/// <param name="font">The font to write.</param>
		/// <param name="path">The path that the tag will be written to.</param>
		public static void WriteTag(BlamFont font, string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Create))
			{
				using (BigEndianWriter bw = new BigEndianWriter(fs))
				{
					bw.BaseStream.Position = 0x24;

					bw.Write((uint)0x666F6E74);//"font"

					bw.BaseStream.Position += 4;//crc but apparently not used

					bw.Write(0x40);//header size

					bw.BaseStream.Position += 8;

					bw.Write((short)1);
					bw.Write((byte)0);
					bw.Write((byte)255);

					bw.Write((uint)0x626C616D);//"blam"

					bw.Write(0);//flags
					bw.Write(font.AscendHeight);
					bw.Write(font.DescendHeight);
					bw.Write(font.LeadHeight);
					bw.Write(font.LeadWidth);

					bw.BaseStream.Position += 0x24;//pad

					bw.BaseStream.Position += 0xC;//table block, not written to tag files?

					//ununsed tag references
					for (int i = 0; i < 4; i++)
					{
						bw.Write((uint)0x666F6E74);
						bw.Write(0);
						bw.Write(0);
						bw.Write(0xFFFFFFFF);
					}

					bw.Write(font.CharacterCount);
					bw.Write(0);
					bw.Write(0);

					var characterdatafield = bw.BaseStream.Position;

					bw.BaseStream.Position += 0x14;//character data but skipping for now

					using (MemoryStream ms = new MemoryStream())
					{
						using (BigEndianWriter cw = new BigEndianWriter(ms))
						{
							foreach (BlamCharacter ch in font.Characters)
							{
								bw.Write(ch.UnicIndex);
								bw.Write((ushort)ch.DisplayWidth);
								bw.Write(ch.Width);
								bw.Write(ch.Height);
								bw.Write(ch.OriginX);
								bw.Write(ch.OriginY);
								bw.Write((ushort)0xFFFF);//hardware index, haven't seen it not -1
								bw.Write((ushort)0);
								bw.Write((int)ms.Position);

								//only alpha channel with no compression
								for (int i = 0; i < ch.DecompressedSize; i+= 4)
								{
									cw.Write(ch.DecompressedData[i + 3]);
								}
							}

							bw.Write(ms.ToArray());

							bw.BaseStream.Position = characterdatafield;

							bw.Write((uint)ms.Length);
						}
					}
				}
			}
		}

		private static List<BlamCharacter> ReadCharacters(BinaryReader br, byte[] data, int characterCount)
		{
			List<BlamCharacter> chars = new List<BlamCharacter>();

			List<ushort> unics = new List<ushort>();
			List<int> dataOffsets = new List<int>();

			for (int c = 0; c < characterCount; c++)
			{
				ushort ch = br.ReadUInt16();

				BlamCharacter bc = new BlamCharacter(ch);

				bc.DisplayWidth = br.ReadUInt16();

				short width = br.ReadInt16();
				short height = br.ReadInt16();

				if (width < 0) width = 0;
				if (height < 0) height = 0;

				bc.Width = (ushort)width;
				bc.Height = (ushort)height;
				bc.OriginX = br.ReadInt16();
				bc.OriginY = br.ReadInt16();

				short hardwareIndex = br.ReadInt16();//unused?
				br.BaseStream.Position += 2;
				int dataOffset = br.ReadInt32();

				chars.Add(bc);
				dataOffsets.Add(dataOffset);
				unics.Add(ch);
			}

			for (int c = 0; c < chars.Count; c++)
			{
				int size = 0;
				if (chars.Last() == chars[c])
					size = data.Length - dataOffsets[c];
				else
					size = dataOffsets[c + 1] - dataOffsets[c];

				byte[] dater = new byte[size];

				Array.Copy(data, dataOffsets[c], dater, 0, size);

				byte[] expanded = new byte[size * 4];
				for (int b = 0; b < dater.Length; b++)
				{
					int offset = b * 4;

					expanded[offset + 0] =
					expanded[offset + 1] =
					expanded[offset + 2] = 0xFF;
					expanded[offset + 3] = dater[b];
				}

				chars[c].DecompressedData = expanded;
				CharacterTools.CompressData(chars[c]);
			}

			return chars;
		}

	}
}
