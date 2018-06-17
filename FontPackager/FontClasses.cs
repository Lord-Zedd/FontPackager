using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace FontPackager
{
	class FontPackage
	{
		private MemoryStream ms;
		private int maxFontCount;
		private List<BlockRange> BlockRanges = new List<BlockRange>();
		private List<FontHeaderInfo> FontEntries = new List<FontHeaderInfo>();
		public List<int> OrderList = new List<int>();
		private List<Block> Blocks = new List<Block>();

		public List<PackageFont> Fonts = new List<PackageFont>();

		private int BlockRangesOffset;
		private int ChunkSize;

		/// <summary>
		/// Loads a font package from the given file.
		/// </summary>
		/// <returns>0 = success, 1 = bad header version, 2 = font count is zero</returns>
		public int Load(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open);
			BinaryReader br = new BinaryReader(fs);
			ms = new MemoryStream(br.ReadBytes((int)br.BaseStream.Length));
			fs.Close();
			br = new BinaryReader(ms);

			br.BaseStream.Position = 0;

			//Make sure this is a font package
			var fileMagic = br.ReadUInt32();
			if (fileMagic != 0xC0000003)
			{
				ms.Close();
				br.Close();
				return 1;
			}

			int fontCount = br.ReadInt32();
			if (fontCount <= 0)
			{
				ms.Close();
				br.Close();
				return 2;
			}

			maxFontCount = 16;

			//check to see if package belongs to Halo 4
			var maxcounthack = br.ReadInt32();
			if (maxcounthack == 1048)
				maxFontCount = 64;

			//get the font headers info
			br.BaseStream.Position -= 4;
			for (int i = 0; i < maxFontCount; i++)
			{
				FontEntries.Add(new FontHeaderInfo()
				{
					offset = br.ReadInt32(),
					size = br.ReadInt32(),
					startBlock = br.ReadInt16(),
					blockCount = br.ReadInt16()
				});
			}

			//read engine ordering
			for (int i = 0; i < maxFontCount; i++)
				OrderList.Add(br.ReadInt32());

			int headerTableOffset = br.ReadInt32();
			int headerTableSize = br.ReadInt32();
			BlockRangesOffset = br.ReadInt32();
			int blockCount = br.ReadInt32();

			br.BaseStream.Position = BlockRangesOffset;

			for (int i = 0; i < blockCount; i++)
			{
				var startchar = br.ReadUInt16();
				var startfont = br.ReadInt16();

				var endchar = br.ReadUInt16();
				var endfont = br.ReadInt16();

				charDatum start = new charDatum() { characterCode = startchar, fontIndex = startfont };
				charDatum end = new charDatum() { characterCode = endchar, fontIndex = endfont };

				BlockRange range = new BlockRange() { startIndex = start, endIndex = end };

				BlockRanges.Add(range);
			}

			//read data blocks

			//set position for hax to check for h4b
			br.BaseStream.Position = 0x8000;

			ChunkSize = 0x8000;

			if (maxFontCount == 64 & br.ReadInt16() != 8)//blocks seem to have a short value of 8 as some header or version
				ChunkSize = 0xC000;

			for (int i = 0; i < blockCount; i++)
			{
				Block dataBlock = new Block();
				dataBlock.Read(br, ChunkSize + (i * ChunkSize));

				Blocks.Add(dataBlock);
			}

			//read font headers and find its characters
			for (int i = 0; i < fontCount; i++)
			{
				var tempfont = new PackageFont();
				br.BaseStream.Position = FontEntries[i].offset;
				tempfont.ReadHeader(br);

				for (int bl = FontEntries[i].startBlock; bl < (FontEntries[i].startBlock + FontEntries[i].blockCount); bl++)
				{
					for (int ch = 0; ch < Blocks[bl].charCount; ch++)
					{
						if (Blocks[bl].charTable[ch].index.fontIndex == i)
						{
							var tempchar = new FontCharacter(Blocks[bl].charTable[ch].index.characterCode, Blocks[bl].charData[ch]);

							tempfont.Characters.Add(tempchar);
						}
					}
				}
				Fonts.Add(tempfont);
			}

			br.Close();
			ms.Close();
			return 0;
		}

		public int LoadH2(string filename)
		{
			int result = 1;
			if (filename.EndsWith(".txt"))
			{
				string[] listfonts = File.ReadAllLines(filename);
				listfonts = listfonts.Distinct().ToArray();
				string basepath = filename.Substring(0, filename.LastIndexOf("\\") + 1);

				foreach (string name in listfonts)
				{
					if (File.Exists(basepath + name))
					{
						result = LoadFile(basepath + name);
					}
				}

				if (Fonts.Count == 0)
					return 2;
				result = 0;
			}
			else
				result = LoadFile(filename);

			return result;
		}

		public int LoadH2Folder(string folder)
		{
			int result = 1;

			string[] files = Directory.GetFiles(folder);

			for (int i = 0; i < files.Count(); i++)
			{
				if (!Path.HasExtension(files[i]))
					result = LoadFile(files[i]);
			}

			if (Fonts.Count == 0)
				return 1;

			return 0;
		}

		private int LoadFile(string filename)
		{
			List<int> CharacterPointers = new List<int>();
			List<CharacterData> CharacterEntries = new List<CharacterData>();

			List<int> kernlist = new List<int>();

			FileStream fs = new FileStream(filename, FileMode.Open);
			BinaryReader br = new BinaryReader(fs);
			ms = new MemoryStream(br.ReadBytes((int)br.BaseStream.Length));
			fs.Close();
			br = new BinaryReader(ms);

			br.BaseStream.Position = 0x200;

			//Make sure this is a font file
			var fileMagic = br.ReadUInt32();
			if (fileMagic != 0xF0000001)
			{
				ms.Close();
				br.Close();
				return 1;
			}

			PackageFont tempfont = new PackageFont();

			tempfont.Name = filename.Substring(filename.LastIndexOf("\\") + 1);

			tempfont.HeaderVersion = (int)fileMagic;
			tempfont.ReadH2Header(br);

			br.BaseStream.Position = 0x3AC;

			int unk7 = br.ReadInt32();
			int unk8 = br.ReadInt32();

			int characterdataoffset = 0x40400 + (tempfont.CharacterCount * 0x10);

			//read characters
			for (int i = 0; i < tempfont.CharacterCount; i++)
			{
				CharacterData tempchar = new CharacterData();
				tempchar.Read(br, 0x40400 + (i * 0x10), true);

				br.BaseStream.Position = tempchar.OffsetH2;

				tempchar.compressedData = br.ReadBytes(tempchar.dataSize);

				CharacterEntries.Add(tempchar);
			}

			//read character indexes and match things up
			for (int i = 0; i < 65536; i++)
			{
				br.BaseStream.Position = 0x400 + (i * 4);

				int charindex = br.ReadInt32();

				FontCharacter tempfchar = new FontCharacter((ushort)i, CharacterEntries[charindex]);

				if (CharacterPointers.Contains(charindex))
					tempfchar.isdupe = true;
				else
					CharacterPointers.Add(charindex);

				tempfont.Characters.Add(tempfchar);
				tempfchar = null;

			}

			tempfont.H2File = filename;
			Fonts.Add(tempfont);


			br.Close();
			ms.Close();
			return 0;
		}

		public void RebuildPkg(string inputpkg)
		{
			UInt16 blockheader = 8;

			List<FontHeaderInfo> FontInfo = new List<FontHeaderInfo>();
			List<BlockRange> BlockInfo = new List<BlockRange>();

			List<charDatum> blockchars = new List<charDatum>();
			List<int> blockcharsizes = new List<int>();

			MemoryStream outBlocks = new MemoryStream();
			MemoryStream chartable = new MemoryStream();
			MemoryStream datatable = new MemoryStream();
			BinaryWriter ow = new BinaryWriter(outBlocks);
			BinaryWriter ctw = new BinaryWriter(chartable);
			BinaryWriter dw = new BinaryWriter(datatable);

			UInt16 tempChar;
			UInt16 lastChar = 0;

			int blockindex = 0;
			int blockoffset = 0;
			Int16 charcount = 0;
			bool freshblock = true;
			int chartablesize = 0;

			BlockRange blockk = new BlockRange();



			for (int i = 0; i < Fonts.Count; i++)
			{
				FontHeaderInfo fontt = new FontHeaderInfo();

				for (int j = 0; j < Fonts[i].Characters.Count; j++)
				{
					if (j == 0)
						fontt.startBlock = (short)blockindex;

					//check if we can fit this character into the current block
					var tempSize = 20 + Fonts[i].Characters[j].Data.dataSize;

					if (chartablesize + datatable.Length + tempSize > ChunkSize - 8)
					{
						//nope can't fit

						//build block character table
						var chartableoffset = 8;
						for (int k = 0; k < blockchars.Count; k++)
						{
							ctw.Write(blockchars[k].characterCode);
							ctw.Write(blockchars[k].fontIndex);
							ctw.Write(chartablesize + chartableoffset);
							chartableoffset += blockcharsizes[k];
						}

						blockchars.Clear();
						blockcharsizes.Clear();

						//calculate block padding
						var blockdiff = (ChunkSize - 8) - (chartablesize + datatable.Length);
						byte[] diffBuffer = new byte[blockdiff];

						//write block
						ow.Write(blockheader);
						ow.Write(charcount);
						ow.Write((Int16)(ctw.BaseStream.Length + 8));
						ow.Write((Int16)(dw.BaseStream.Length));
						ow.Write(chartable.ToArray());
						ow.Write(datatable.ToArray());
						ow.Write(diffBuffer);

						//note the last character in the block for header
						blockk.endIndex = new charDatum() { characterCode = lastChar, fontIndex = (short)i }; ;

						BlockInfo.Add(blockk);

						//clean up and reset stuff
						blockk = new BlockRange();
						freshblock = true;

						blockindex += 1;
						charcount = 0;
						blockoffset = 0;
						chartablesize = 0;

						ctw.Close();
						dw.Close();

						chartable = new MemoryStream();
						datatable = new MemoryStream();

						ctw = new BinaryWriter(chartable);
						dw = new BinaryWriter(datatable);
					}

					tempChar = Fonts[i].Characters[j].CharCode;

					//note the first character in the block
					if (freshblock)
					{
						blockk.startIndex = new charDatum() { characterCode = tempChar, fontIndex = (short)i };
						freshblock = false;
					}
					
					//add character in temp character table
					blockchars.Add(new charDatum() { characterCode = tempChar, fontIndex = (short)i });
				
					//add character data to temp stream
					dw.Write(Fonts[i].Characters[j].Data.dispWidth);
					dw.Write(Fonts[i].Characters[j].Data.dataSize);
					dw.Write(Fonts[i].Characters[j].Data.width);
					dw.Write(Fonts[i].Characters[j].Data.height);
					dw.Write(Fonts[i].Characters[j].Data.originx);
					dw.Write(Fonts[i].Characters[j].Data.originy);
					dw.Write(Fonts[i].Characters[j].Data.compressedData);

					//calculate character padding
					var dataalign = 12 + Fonts[i].Characters[j].Data.dataSize;
					byte[] alignhelp = new byte[0];

					if (dataalign % 8 != 0)
					{
						var dataalign2 = dataalign / 8;
						alignhelp = new byte[((dataalign2 * 8) + 8) - dataalign];

						dw.Write(alignhelp);
					}

					//note the size with padding for the character table
					blockcharsizes.Add(Fonts[i].Characters[j].Data.dataSize + 12 + alignhelp.Length);

					//clean up and reset stuff
					blockoffset += Fonts[i].Characters[j].Data.dataSize + 12 + alignhelp.Length;
					charcount += 1;
					chartablesize += 8;
					lastChar = tempChar;

				}

				fontt.blockCount = (short)(blockindex - fontt.startBlock + 1);

				FontInfo.Add(fontt);

			}

			//out of fonts and characters so write the final block
			var chartableoffsetx = 8;
			for (int k = 0; k < blockchars.Count; k++)
			{
				ctw.Write(blockchars[k].characterCode);
				ctw.Write(blockchars[k].fontIndex);
				ctw.Write(chartablesize + chartableoffsetx);
				chartableoffsetx += blockcharsizes[k];
			}

			var blockdiffx = (ChunkSize - 8) - (chartablesize + datatable.Length);

			byte[] diffBufferx = new byte[blockdiffx];

			ow.Write(blockheader);
			ow.Write(charcount);
			ow.Write((Int16)(ctw.BaseStream.Length + 8));
			ow.Write((Int16)(dw.BaseStream.Length));
			ow.Write(chartable.ToArray());
			ow.Write(datatable.ToArray());
			ow.Write(diffBufferx);

			blockk.endIndex = new charDatum() { characterCode = lastChar, fontIndex = (short)(Fonts.Count - 1) }; ;

			BlockInfo.Add(blockk);

			ow.Close();
			ctw.Close();
			dw.Close();
			chartable.Close();
			datatable.Close();

			//begin writing new stuff to the font package

			FileStream fs = new FileStream(inputpkg, FileMode.Open, FileAccess.Write);
			BinaryWriter bw = new BinaryWriter(fs);

			//update ordering
			bw.BaseStream.Position = 8 + (maxFontCount * 0xC);

			for (int i = 0; i < OrderList.Count; i++)
				bw.Write(OrderList[i]);

			//update the block info for each font
			bw.BaseStream.Position = 0x8;

			for(int i = 0; i < FontInfo.Count; i++)
			{
				bw.BaseStream.Position += 8;

				bw.Write((short)FontInfo[i].startBlock);
				bw.Write((short)FontInfo[i].blockCount);
			}

			//update the character info for each block
			bw.BaseStream.Position = BlockRangesOffset;

			for (int i = 0; i < BlockInfo.Count; i++)
			{
				bw.Write(BlockInfo[i].startIndex.characterCode);
				bw.Write(BlockInfo[i].startIndex.fontIndex);
			
				bw.Write(BlockInfo[i].endIndex.characterCode);
				bw.Write(BlockInfo[i].endIndex.fontIndex);
			}

			//clean up any nonexistant block info
			for (int i = 0; i < (maxFontCount - Fonts.Count); i++)
				bw.Write((Int64)0);

			//update block count
			bw.BaseStream.Position = (8 + (maxFontCount * 12) + (maxFontCount * 4) + 12);//0x114;
			bw.Write((int)BlockInfo.Count);

			//update each font header with new info
			for (int i = 0; i < Fonts.Count; i++)
			{
				bw.BaseStream.Position = FontEntries[i].offset + 0x24;

				bw.Write(Fonts[i].AscendHeight);
				bw.Write(Fonts[i].DescendHeight);
				bw.Write(Fonts[i].LeadHeight);
				bw.Write(Fonts[i].LeadWidth);

				var datasize = 0;
				for (int j = 0; j < Fonts[i].Characters.Count; j++)
					datasize += Fonts[i].Characters[j].Data.dataSize;

				bw.BaseStream.Position = FontEntries[i].offset + 0x13C;

				bw.Write((int)Fonts[i].Characters.Count);

				bw.BaseStream.Position += 0x14;

				bw.Write(datasize);
			}

			//finally write the new blocks
			bw.BaseStream.SetLength(ChunkSize);

			bw.BaseStream.Position = ChunkSize;
			bw.Write(outBlocks.ToArray());

			bw.Close();
			fs.Close();
		}

		public void RebuildFile(string input, int fontindex)
		{
			MemoryStream chartable = new MemoryStream();
			MemoryStream chardata = new MemoryStream();
			BinaryWriter ct = new BinaryWriter(chartable);
			BinaryWriter cd = new BinaryWriter(chardata);

			FileStream fs = new FileStream(input, FileMode.Open, FileAccess.Write);
			BinaryWriter bw = new BinaryWriter(fs);

			List<int> writtenindexes = new List<int>();
			List<CharacterData> writtenchars = new List<CharacterData>();

			//get the real amount of characters in the font
			int blahh = Fonts[fontindex].Characters.GroupBy(c => c.Data).Distinct().Count();

			int dataoffset = 0x40400 + (blahh * 0x10);

			//create the last 2 blocks of the file while updating indexes
			foreach (FontCharacter charr in Fonts[fontindex].Characters)
			{
				bw.BaseStream.Position = 0x400 + (charr.CharCode * 4);

				//Figure out if the character has already been written
				if (!writtenchars.Contains(charr.Data))
				{
					int tableindex = writtenchars.Count;

					writtenchars.Add(charr.Data);

					ct.Write(charr.Data.dispWidth);
					ct.Write(charr.Data.dataSize);
					ct.Write(charr.Data.width);
					ct.Write(charr.Data.height);
					ct.Write(charr.Data.originx);
					ct.Write(charr.Data.originy);
					ct.Write(dataoffset);

					cd.Write(charr.Data.compressedData);

					dataoffset += charr.Data.dataSize;

					bw.Write(tableindex);
				}
				else
				{
					bw.Write(writtenchars.IndexOf(charr.Data));
				}
					
			}

			bw.BaseStream.Position = 0x204;
			bw.Write(Fonts[fontindex].AscendHeight);
			bw.Write(Fonts[fontindex].DescendHeight);
			bw.Write(Fonts[fontindex].LeadHeight);
			bw.Write(Fonts[fontindex].LeadWidth);
			bw.Write(writtenchars.Count);
			bw.BaseStream.Position += 0x8;
			bw.Write((int)chardata.Length);

			bw.BaseStream.SetLength(0x40400);

			bw.BaseStream.Position = 0x40400;
			bw.Write(chartable.ToArray());
			bw.Write(chardata.ToArray());

			ct.Close();
			cd.Close();
			chartable.Close();
			chardata.Close();
			bw.Close();
			fs.Close();
		}

		/// <summary>
		/// Converts the given System.Drawing.Image and adds or replaces to the given font index/character. Left/Right padding is removed and set as the display width.
		/// </summary>
		/// <param name="charcode"></param>
		/// <param name="fontindex"></param>
		/// <param name="image"></param>
		/// <returns></returns>
		public void AddCustomCharacter(UInt16 charcode, int fontindex, Image image, CharTint tint, bool cropme, int dwidth = -1)
		{
			int origwidth = image.Width;

			if (cropme)
				image = CropWidth((Bitmap)image);

			Bitmap bm = (Bitmap)image;
			BitmapData bd = bm.LockBits(
				new Rectangle(new Point(0, 0), bm.Size),
				System.Drawing.Imaging.ImageLockMode.ReadOnly,
				bm.PixelFormat);
			int bytes = Math.Abs(bd.Stride) * bm.Height;
			byte[] bmBytes = new byte[bytes];
			System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, bmBytes, 0, bytes);
			bm.UnlockBits(bd);

			//h2 code I don't wanna mess with to convert to 32 bit
			if (image.PixelFormat != PixelFormat.Format32bppArgb)
			{
				int sBpp = (Image.GetPixelFormatSize(bm.PixelFormat) / 8);
				int stride = Math.Abs(bd.Stride);
				byte[] bmBytes2 = new byte[bytes * 4 / sBpp];
				for (int i = 0; i < bd.Height; i++)
					for (int ii = 0; ii < bd.Width; ii++)
					{
						switch (image.PixelFormat)
						{
							case PixelFormat.Format8bppIndexed:
								int colorIndex = bmBytes[i * stride + ii * sBpp];
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 0] = bm.Palette.Entries[colorIndex].B;  // b
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 1] = bm.Palette.Entries[colorIndex].G;  // g 
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 2] = bm.Palette.Entries[colorIndex].R;  // r
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 3] = bm.Palette.Entries[colorIndex].A;  // a
								break;
							case PixelFormat.Format24bppRgb:
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 0] = bmBytes[i * stride + ii * sBpp + 0];  // b
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 1] = bmBytes[i * stride + ii * sBpp + 1];  // g 
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 2] = bmBytes[i * stride + ii * sBpp + 2];  // r
								bmBytes2[(i * bm.Width * 4) + ii * 4 + 3] = 0xff;  // a
								break;
							default:
								throw new System.NotSupportedException(image.PixelFormat.ToString() + " format not supported!");
						}
					}
				bmBytes = bmBytes2;
				bytes = bm.Width * 4 * bm.Height;
			}

			List<ushort> pixelList = new List<ushort>();

			//convert pixels to 16 bit
			for (int i = 0; i < bytes / 4; i++)
			{
				byte blue = bmBytes[i * 4 + 0];
				byte green = bmBytes[i * 4 + 1];
				byte red = bmBytes[i * 4 + 2];
				byte alpha = bmBytes[i * 4 + 3];

				byte AR = (byte)(((alpha & 0xF0)) | ((red >> 4)));
				byte GB = (byte)(((green & 0xF0)) | ((blue >> 4)));

				ushort pixel = (ushort)((AR << 8) | GB);

				//adjust whites/blacks if requested
				switch (tint)
				{
					case CharTint.Cool:
						if ((pixel & 0xFFF) == 0x000) pixel += 1;
						else if (((pixel & 0xFFF) % 0x111) == 0) pixel -= 0x100;
						break;

					case CharTint.Warm:
						if ((pixel & 0xFFF) == 0x000) pixel += 0x100;
						else if (((pixel & 0xFFF) % 0x111) == 0) pixel -= 1;
						break;
				}

				pixelList.Add(pixel);
			}

			List<byte> data = new List<byte>();
			ushort basePixel = 0xFFF;

			//start compressing pixels like the game do
			if (image.Width > 1)
				for (int i = 0; i < pixelList.Count; i++)
				{
					byte run = 0;
					ushort pixel = pixelList[i];
					ushort nextpixel = 0;
					bool dontChange = false;

					//grab the next pixel, or prevent writing
					if (i + 1 >= pixelList.Count)
						dontChange = true;
					else
						nextpixel = pixelList[i + 1];

					//get the alpha for the first pixel
					var alpha = (pixel & 0xF000) >> 12;

					//extra checking to see if we are updating the base
					if ((pixel & 0xFFF) == (basePixel & 0xFFF)
						&& (pixel & 0xFFF) == (nextpixel & 0xFFF))
						dontChange = true;

					//do we write base change bytes
					if ((pixel != basePixel) && ((pixel & 0xFFF) != (basePixel & 0xFFF)) ||
						(!dontChange))
					{
						data.Add(0);
						data.Add((byte)((pixel & 0xFF00) >> 8));
						data.Add((byte)pixel);

						basePixel = (ushort)(pixel);
						continue;
					}

					//check for easy long-run 0/F alpha pixels
					if (alpha == 0 || alpha == 0xF)
					{
						while ((i + run) < pixelList.Count && pixelList[i + run] == pixel && run < 0x3F)
							run++;

						if (run > 1)
						{
							data.Add((byte)(((alpha & 4) << 4) | run));
							i += run - 1;
							continue;
						}
					}

					//if the alpha wasn't 0/f or only lasts 1 pixel, gotta do all this
					run = 0;
					byte codeL = 0;

					codeL = (byte)(alpha & 0xE);
					codeL <<= 2;
					codeL |= 0x80;

					byte codeR = 0;

					//get alpha for the next pixel
					alpha = (nextpixel & 0xF000) >> 12;

					//find run and limit if alpha is unsupported for a longer run
					while ((i + 1 + run) < pixelList.Count && (pixelList[i + 1 + run]) == (nextpixel) && run < 5)
						run++;

					if (run > 1 && alpha != 0 && alpha != 0xF)
						run = 1;

					//start writing
					if (run == 0)
					{
						data.Add((byte)(codeL));
						continue;
					}
					else if (run == 1)
					{
						codeR = (byte)(alpha >> 1);

						data.Add((byte)((codeL | codeR) | 0xC0));
						i += run;
						continue;
					}
					else if (run == 2) //these may still have a trick instead of using hardcoded codeRs but idk
						codeR = (alpha == 0xF) ? (byte)3 : (byte)7;
					else if (run == 3)
						codeR = (alpha == 0xF) ? (byte)2 : (byte)6;
					else if (run == 4)
						codeR = (alpha == 0xF) ? (byte)1 : (byte)5;
					else if (run == 5)
					{
						if (alpha == 0xF)
						{
							run--; //run becomes 4;
							codeR = 1;
						}
						else
							codeR = 4;
					}

					data.Add((byte)(codeL | codeR));
					i += run;

				}
			else
				data.Add(0x15);

			// Check if compressed size is too large to be imported
			if (data.Count > short.MaxValue)
				throw new System.OverflowException("Compressed image size must not be larger than " + short.MaxValue.ToString("n0") + " bytes\nCurrent image compresses to " + data.Count.ToString("n0") + " bytes");

			//see if we are adding or replacing
			var existingindex = Fonts[fontindex].FindCharacter(charcode);

			CharacterData ci = new CharacterData();

			if (existingindex != -1)
			{
				ci.originx = Fonts[fontindex].Characters[existingindex].Data.originx;
				ci.originy = Fonts[fontindex].Characters[existingindex].Data.originy;
			}
			else
			{
				ci.originx = 0;
				ci.originy = (ushort)Fonts[fontindex].AscendHeight;
			}

			ci.width = (UInt16)image.Width;
			ci.height = (UInt16)image.Height;

			if (dwidth == -1)
				ci.dispWidth = (UInt16)origwidth;
			else
				ci.dispWidth = (ushort)dwidth;

			ci.compressedData = data.ToArray();
			ci.dataSize = (UInt16)ci.compressedData.Length;

			

			FontCharacter ife = new FontCharacter(charcode, ci);

			ife.isdupe = false;

			if (existingindex != -1)
				Fonts[fontindex].Characters[existingindex] = ife;
			else
			{
				Fonts[fontindex].Characters.Add(ife);
				Fonts[fontindex].SortCharacters();
			}
		}

		internal Bitmap CropWidth(Bitmap image)
		{
			if (image.Width == 1)
				return image;

			Bitmap bm = (Bitmap)image;
			BitmapData bd = bm.LockBits(
				new Rectangle(new Point(0, 0), bm.Size),
				System.Drawing.Imaging.ImageLockMode.ReadOnly,
				bm.PixelFormat);
			int bytes = Math.Abs(bd.Stride) * bm.Height;
			byte[] bmBytes = new byte[bytes];
			System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, bmBytes, 0, bytes);

			int minwidth = int.MaxValue;
			int maxwidth = 0;
			int heightstart = int.MaxValue;

			for (int i = 0; i < bd.Height; i++)
				for (int ii = 0; ii < bd.Width; ii++)
				{
					var a = bmBytes[i * bd.Stride + 4 * ii + 3];
					if (a > 0)
					{
						if (ii < minwidth)
							minwidth = ii;
						if (ii > maxwidth)
							maxwidth = ii;
						if (i < heightstart)
							heightstart = i;
					}
				}

			if (minwidth > image.Width)
				minwidth = 1;
			if (maxwidth < minwidth)
				maxwidth = minwidth + 1;

			bm.UnlockBits(bd);

			Rectangle nopad = Rectangle.FromLTRB(minwidth, 0, maxwidth + 1, image.Height);
			Rectangle rect = new Rectangle(0, 0, nopad.Width, nopad.Height);

			Bitmap output = new Bitmap(nopad.Width, image.Height);
			using (Graphics g = Graphics.FromImage(output))
			{
				g.DrawImage(image, rect, nopad, GraphicsUnit.Pixel);
			}

			return output;
		}

		internal int FixAlpha(int alpha)
		{
			//not needed anymore with bitwise updates

			if ((alpha & 3) == 1)
				alpha &= 0xE;
			else if ((alpha & 3) == 2)
				alpha |= 3;

			return alpha;
		}
	}

	public enum CharTint
	{
		None,
		Cool,
		Warm
	}

	public class FontHeaderInfo
	{
		public int offset { get; set; }
		public int size { get; set; }

		public Int16 startBlock { get; set; }
		public Int16 blockCount { get; set; }
	}

	public class BlockRange
	{
		public charDatum startIndex { get; set; }
		public charDatum endIndex { get; set; }
	}

	public class charDatum
	{
		public UInt16 characterCode { get; set; }
		public Int16 fontIndex { get; set; }
	}

	public class PackageFont
	{
		public string H2File { get; set; }
		public string H2FileSafe()
		{
			if (H2File != null)
				return H2File.Substring(H2File.LastIndexOf("\\") + 1);
			else
				return null;
		}

		public int HeaderVersion { get; set; }

		public string Name { get; set; }

		public Int16 AscendHeight { get; set; }
		public Int16 DescendHeight { get; set; }
		public Int16 LeadHeight { get; set; }
		public Int16 LeadWidth { get; set; }

		private int KerningPairIndexOffset { get; set; }
		private int KerningPairCount { get; set; }

		public byte[] KerningPairIndexTable { get; set; }

		private int KerningPairOffset { get; set; }
		public int unk2 { get; set; }
		public int CharacterCount { get; set; }
		public int unk4 { get; set; }
		public int unk5 { get; set; }
		public int unk6 { get; set; }
		public int unk7 { get; set; }
		public int unk8 { get; set; }
		public int CompressedSize { get; set; }
		public int DecompressedSize { get; set; }

		public byte[] KerningPairTable { get; set; }

		public void ReadHeader(BinaryReader br)
		{
			HeaderVersion = br.ReadInt32();
			byte[] _name = br.ReadBytes(32);
			Name = (System.Text.Encoding.ASCII.GetString(_name).TrimEnd((Char)0));

			AscendHeight = br.ReadInt16();
			DescendHeight = br.ReadInt16();
			LeadHeight = br.ReadInt16();
			LeadWidth = br.ReadInt16();
			KerningPairIndexOffset = br.ReadInt32();
			KerningPairCount = br.ReadInt32(); //unsupported atm

			KerningPairIndexTable = br.ReadBytes(0x100);

			KerningPairOffset = br.ReadInt32();
			unk2 = br.ReadInt32();
			CharacterCount = br.ReadInt32();
			unk4 = br.ReadInt32();
			unk5 = br.ReadInt32();
			unk6 = br.ReadInt32();
			unk7 = br.ReadInt32();
			unk8 = br.ReadInt32();
			CompressedSize = br.ReadInt32();
			DecompressedSize = br.ReadInt32();

			if (KerningPairCount > 0)
				KerningPairTable = br.ReadBytes(KerningPairCount * 4);
		}

		public void ReadH2Header(BinaryReader br)
		{
			AscendHeight = br.ReadInt16();
			DescendHeight = br.ReadInt16();
			LeadHeight = br.ReadInt16();
			LeadWidth = br.ReadInt16();
			CharacterCount = br.ReadInt32();
			unk7 = br.ReadInt32();
			unk8 = br.ReadInt32();
			CompressedSize = br.ReadInt32();
			DecompressedSize = br.ReadInt32();

			KerningPairCount = br.ReadInt32();

			if (KerningPairCount > 0)
				KerningPairTable = br.ReadBytes(KerningPairCount * 4);
		}

		public List<FontCharacter> Characters = new List<FontCharacter>();

		/// <summary>
		/// Sorts the characters of a font. Called automatically by FontPackage.AddCharacter()
		/// </summary>
		public void SortCharacters()
		{
			Characters = Characters.OrderBy(c => c.CharCode).ToList();
		}

		public int FindCharacter(ushort charcode)
		{
			return Characters.FindIndex(c => c.CharCode.Equals(charcode));
		}

	}

	public class FontCharacter
	{
		public UInt16 CharCode { get; set; }
		public CharacterData Data { get; set; }

		public bool isdupe { get; set; }

		public FontCharacter(UInt16 code, CharacterData _char)
		{
			CharCode = code;

			Data = _char;
		}
	}

	public class Block
	{
		public int location { get; set; }
		public Int16 version { get; set; }
		public Int16 charCount { get; set; }
		public Int16 charTableSize { get; set; }
		public Int16 charDataSize { get; set; }

		public int DataOffset()
		{
			return (charTableSize + 8);
		}

		public List<BlockCharEntry> charTable = new List<BlockCharEntry>();
		public List<CharacterData> charData = new List<CharacterData>();

		public void Read(BinaryReader br, int offset)
		{
			location = offset;
			br.BaseStream.Position = offset;

			version = br.ReadInt16();
			charCount = br.ReadInt16();
			charTableSize = br.ReadInt16();
			charDataSize = br.ReadInt16();

			//read in table
			for (int i = 0; i < charCount; i++)
			{
				var _charindex = br.ReadUInt16();
				var _fontindex = br.ReadInt16();
				int _offset = br.ReadInt32();

				charDatum datum = new charDatum() { characterCode = _charindex, fontIndex = _fontindex };

				BlockCharEntry Index = new BlockCharEntry() { index = datum, dataOffset = _offset };

				charTable.Add(Index);
			}

			//read in data
			for (int i = 0; i < charTable.Count; i++)
			{
				CharacterData charinfo = new CharacterData();
				charinfo.Read(br, (location + charTable[i].dataOffset));

				charData.Add(charinfo);
			}
		}
	}

	public class BlockCharEntry
	{
		public charDatum index { get; set; }
		public int dataOffset { get; set; }
	}

	public class CharacterData
	{
		public UInt16 dispWidth { get; set; }
		public UInt16 dataSize { get; set; }
		public UInt16 width { get; set; }
		public UInt16 height { get; set; }
		public Int16 originx { get; set; }
		public UInt16 originy { get; set; }

		public UInt32 OffsetH2 { get; set; }

		public byte[] compressedData { get; set; }
		public byte[] Data { get; set; }

		public PixelFormat PixelFormat { get; set; }
		public int Bpp { get; set; }

		public CharacterData()
		{
			this.PixelFormat = PixelFormat.Format32bppArgb;
			this.Bpp = 4;
		}

		public void Read(BinaryReader br, int offset, bool h2 = false)
		{
			br.BaseStream.Position = offset;

			dispWidth = br.ReadUInt16();
			dataSize = br.ReadUInt16();
			width = br.ReadUInt16();
			height = br.ReadUInt16();
			originx = br.ReadInt16();
			originy = br.ReadUInt16();

			if (h2)
				OffsetH2 = br.ReadUInt32();
			else
				compressedData = br.ReadBytes(dataSize);
		}

		public BitmapSource getImage()
		{
			if (Data == null)
				decodeData();

			Bitmap bm = new Bitmap(width, height, PixelFormat);
			BitmapData bd = bm.LockBits(
				new Rectangle(new Point(0, 0), bm.Size),
				System.Drawing.Imaging.ImageLockMode.WriteOnly,
				bm.PixelFormat);
			System.Runtime.InteropServices.Marshal.Copy(Data, 0, bd.Scan0, Data.Length);
			bm.UnlockBits(bd);

			bd = bm.LockBits(
			new System.Drawing.Rectangle(0, 0, bm.Width, bm.Height),
			System.Drawing.Imaging.ImageLockMode.WriteOnly,
			bm.PixelFormat);

			return BitmapSource.Create(bd.Width, bd.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bd.Scan0, bd.Stride * bd.Height, bd.Stride);
		}

		//basically how the games do it
		internal void decodeData()
		{
			int bytes = width * height * Bpp;
			Data = new byte[bytes];

			int pixelIndex = 0;
			UInt16 baseColor = 0xFFF;

			for (int Z = 0; Z < compressedData.Length; Z++)
			{
				byte Code = compressedData[Z];

				byte codeFlags = (byte)(Code >> 6);
				byte codeValue = (byte)(Code & 0x3F);

				byte pixCount = 0;
				UInt16 newColor = 0;

				if (codeFlags < 2)
				{
					if (Code == 0)
					{
						newColor = (UInt16)(((compressedData[Z + 1] << 8) | compressedData[Z + 2]));
						Z += 2; //skip the 2 color bytes we just read

						drawPixels(pixelIndex, 1, ExpandColor(newColor));
						baseColor = (UInt16)(newColor & 0xFFF);
						pixelIndex += 1;
					}
					else
					{
						pixCount = codeValue;
						uint orVal = 0;

						if (codeFlags != 0)
							orVal = 0xF000;

						drawPixels(pixelIndex, pixCount, ExpandColor((UInt16)(baseColor | orVal)));
						pixelIndex += pixCount;
					}
					continue;
				}

				newColor = (UInt16)((Code >> 3) & 7);
				newColor = (UInt16)((newColor << 1) | (newColor & 1));
				newColor = (UInt16)((newColor << 12) | baseColor);

				drawPixels(pixelIndex, 1, ExpandColor(newColor));
				pixelIndex += 1;

				UInt16 threebitVal = (UInt16)(Code & 7);
				if (codeFlags != 2)
				{
					newColor = (UInt16)((threebitVal << 1) | (threebitVal & 1));
					newColor = (UInt16)((newColor << 12) | baseColor);

					drawPixels(pixelIndex, 1, ExpandColor(newColor));
					pixelIndex += 1;
				}
				else
				{
					pixCount = 0;
					newColor = baseColor;

					switch (threebitVal)
					{
						default:
						case 0:
							pixelIndex += (int)pixCount;
							continue;
						case 1:
							newColor = (UInt16)((baseColor) | 0xF000);
							pixCount = 4;
							break;
						case 2:
							newColor = (UInt16)((baseColor) | 0xF000);
							pixCount = 3;
							break;
						case 3:
							newColor = (UInt16)((baseColor) | 0xF000);
							pixCount = 2;
							break;
						case 4:
							pixCount = 5;
							break;
						case 5:
							pixCount = 4;
							break;
						case 6:
							pixCount = 3;
							break;
						case 7:
							pixCount = 2;
							break;
					}

					drawPixels(pixelIndex, (uint)pixCount, ExpandColor(newColor));
					pixelIndex += (int)pixCount;
				}
			}
		}

		/// <summary>
		/// This will take 16 bit color and expand it into a System.Drawing.Color
		/// </summary>
		/// <param name="input"></param>
		internal Color ExpandColor(UInt16 input)
		{
			byte a = (byte)(((input & 0xF000) >> 12) * 255 / 15);
			byte r = (byte)(((input & 0x0F00) >> 8) * 255 / 15);
			byte g = (byte)(((input & 0x00F0) >> 4) * 255 / 15);
			byte b = (byte)((input & 0x000F) * 255 / 15);

			return Color.FromArgb(a, r, g, b);
		}

		/// <summary>
		/// This will draw a pixel run into a bitmap's raw BitmapData in a A8R8G8B8 format, 99% of code from troymac1ure
		/// </summary>
		/// <param name="db"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <param name="color"></param>
		internal void drawPixels(int offset, uint count, Color color)
		{
			for (int X = 0; X < count; X++)
			{
				int x = (X + offset) % width;
				int y = (X + offset) / width;
				int stride = width * this.Bpp;

				if (((x * this.Bpp) + y * (stride)) >= Data.Length)
					break;

				Data[(x * this.Bpp) + y * stride] = (byte)(color.B);
				Data[(x * this.Bpp + 1) + y * stride] = (byte)(color.G);
				Data[(x * this.Bpp + 2) + y * stride] = (byte)(color.R);
				Data[(x * this.Bpp + 3) + y * stride] = (byte)(color.A);
			}
		}
	}
}
