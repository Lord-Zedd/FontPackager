using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FontPackager.Classes
{
	/// <summary>
	/// Handles reading Xbox CE cache files.
	/// </summary>
	public static class TagIO
	{
		/// <summary>
		/// Reads font tags from an Xbox cache file belonging to Halo CE, Stubbs The Zombie, or Shadowrun.
		/// </summary>
		/// <param name="path">The path to the Xbox cache file to pull fonts from.</param>
		public static Tuple<IOError, List<BlamFont>> Read(string path)
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

				List<BlamCharacter> chars = new List<BlamCharacter>();

				List<ushort> unics = new List<ushort>();
				List<int> dataoffsets = new List<int>();

				for (int c = 0; c < charscount; c++)
				{
					ushort ch = br.ReadUInt16();

					BlamCharacter bc = new BlamCharacter(ch);

					bc.DisplayWidth = br.ReadUInt16();

					short width = br.ReadInt16();
					short height = br.ReadInt16();
					if (width < 0)
						width = 0;
					if (height < 0)
						height = 0;
					bc.Width = (ushort)width;
					bc.Height = (ushort)height;
					bc.OriginX = br.ReadInt16();
					bc.OriginY = br.ReadInt16();

					short unk = br.ReadInt16();
					fs.Position += 2;
					int offset = br.ReadInt32();

					chars.Add(bc);
					dataoffsets.Add(offset);
					unics.Add(ch);

				}

				for (int c = 0; c < chars.Count; c++)
				{
					int size = 0;
					if (chars.Last() == chars[c])
						size = datasize - dataoffsets[c];
					else
						size = dataoffsets[c + 1] - dataoffsets[c];

					byte[] dater = new byte[size];

					Array.Copy(data, dataoffsets[c], dater, 0, size);

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

					font.Characters.Add(chars[c]);
				}

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
	}
}
