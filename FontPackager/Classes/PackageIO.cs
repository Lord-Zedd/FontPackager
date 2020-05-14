using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FontPackager.Classes
{
	/// <summary>
	/// Handles reading and writing of font_package files.
	/// </summary>
	public static class PackageIO
	{
		const uint PackageVersion360 = 0xC0000003;
		const uint PackageVersionX64 = 0xC0000004;
		const uint PackageVersionX64H2A = 0xC0000005;
		const uint FontVersion360 = 0xF0000005;
		const uint FontVersionX64 = 0xF0000006;
		const short BlockVersion = 8;

		/// <summary>
		/// Reads a font_package.bin file.
		/// </summary>
		/// <param name="path">The path to the font_package.bin file to be read.</param>
		public static Tuple<IOError, FileFormat, List<BlamFont>, List<int>> Read(string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Open))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					uint pversion = br.ReadUInt32();
					if (pversion != PackageVersion360 && pversion != PackageVersionX64 && pversion != PackageVersionX64H2A)
					{
						return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
							(IOError.BadVersion, 0, null, null);
					}

					int fontcount = br.ReadInt32();
					if (fontcount <= 0)
					{
						return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
							(IOError.Empty, 0, null, null);
					}

					List<BlamFont> fonts = new List<BlamFont>();
					List<int> orders = new List<int>();

					int maxfontcount = 16;

					int maxcounthack = br.ReadInt32();
					if (maxcounthack == 1048)
						maxfontcount = 64;

					FileFormat fmt = FileFormat.Package;

					if (pversion == PackageVersionX64)
						fmt = FileFormat.MCC;
					else if (pversion == PackageVersionX64H2A)
						fmt = FileFormat.H2AMCC;

					if (maxfontcount == 64)
						fmt |= FileFormat.Max64;

					br.BaseStream.Position -= 4;
					List<FontInfo> fontinfo = new List<FontInfo>();

					for (int i = 0; i < maxfontcount; i++)
					{
						fontinfo.Add(new FontInfo()
						{
							Offset = br.ReadInt32(),
							Size = br.ReadInt32(),
							StartBlock = br.ReadInt16(),
							BlockCount = br.ReadInt16()
						});
					}

					for (int i = 0; i < maxfontcount; i++)
						orders.Add(br.ReadInt32());

					int headerTableOffset = br.ReadInt32();
					int headerTableSize = br.ReadInt32();
					int BlockRangesOffset = br.ReadInt32();
					int blockCount = br.ReadInt32();

					br.BaseStream.Position = BlockRangesOffset;

					List<Block> blocks = new List<Block>();

					br.BaseStream.Position = 0x8000;
					short h4bCheck = br.ReadInt16();

					if (!fmt.HasFlag(FileFormat.ChunkC) && !fmt.HasFlag(FileFormat.Chunk10) &&
						maxfontcount == 64 && h4bCheck != BlockVersion)
						fmt |= FileFormat.ChunkC;

					int ChunkSize = 0x8000;
					if (fmt.HasFlag(FileFormat.ChunkC))
						ChunkSize = 0xC000;
					else if (fmt.HasFlag(FileFormat.Chunk10))
						ChunkSize = 0x10000;

					int blocktest = 0;

					for (int i = 0; i < blockCount; i++)
					{
						Block dataBlock = new Block();
						dataBlock.Read(br, ChunkSize + (i * ChunkSize), fmt);

						blocks.Add(dataBlock);
						blocktest += dataBlock.BlockCharacters.Count;
					}

					for (int i = 0; i < fontcount; i++)
					{
						br.BaseStream.Position = fontinfo[i].Offset;

						uint fversion = br.ReadUInt32();

						int start = (int)br.BaseStream.Position;

						var tempfont = new BlamFont(br.ReadStringToNull(32));

						tempfont.AscendHeight = br.ReadInt16();
						tempfont.DescendHeight = br.ReadInt16();
						tempfont.LeadHeight = br.ReadInt16();
						tempfont.LeadWidth = br.ReadInt16();
						int pairstart = br.ReadInt32();
						int paircount = br.ReadInt32();

						byte[] KerningPairIndexes = br.ReadBytes(0x100);

						int pairend = br.ReadInt32();
						int LastCharacter = br.ReadInt32();
						int CharacterCount = br.ReadInt32();
						int virtualdatastart = br.ReadInt32();
						int virtualdatasize = br.ReadInt32();
						tempfont.Unknown6 = br.ReadInt32();
						if (fmt.HasFlag(FileFormat.x64Head))
							tempfont.Unknown6b = br.ReadInt32();
						tempfont.Unknown7 = br.ReadInt32();
						tempfont.Unknown8 = br.ReadInt32();
						int CompressedSize = br.ReadInt32();
						int DecompressedSize = br.ReadInt32();

						int lastc = LastCharacter;

						if (fmt.HasFlag(FileFormat.x64Head))
						{
							tempfont.MCCScale = br.ReadInt32();
							tempfont.UnknownMCC2 = br.ReadInt32();
						}

						List<Tuple<byte, sbyte>> pairs = new List<Tuple<byte, sbyte>>();

						for (int p = 0; p < paircount; p++)
							pairs.Add(new Tuple<byte, sbyte>(br.ReadByte(), br.ReadSByte()));

						int kernremaining = paircount;

						for (int p = 0xFF; p > 0; p--)
						{
							byte ind = KerningPairIndexes[p];

							if (ind >= kernremaining)
								continue;

							for (int k = ind; k < kernremaining; k++)
							{
								tempfont.KerningPairs.Add(new KerningPair((byte)p, pairs[k].Item1, pairs[k].Item2));
							}
							kernremaining = ind;

						}

						tempfont.KerningPairs = tempfont.KerningPairs.OrderBy(x => x.Character).ThenBy(x => x.TargetCharacter).ToList();

						for (int bl = fontinfo[i].StartBlock; bl < (fontinfo[i].StartBlock + fontinfo[i].BlockCount); bl++)
						{
							for (int ch = 0; ch < blocks[bl].BlockCharacters.Count; ch++)
							{
								if (blocks[bl].BlockCharacters[ch].FontIndex == i)
								{
									tempfont.Characters.Add(blocks[bl].BlockCharacters[ch].Character);
								}
							}
						}
						fonts.Add(tempfont);
					}
					return new Tuple<IOError, FileFormat, List<BlamFont>, List<int>>
							(IOError.None, fmt, fonts, orders);
				}
			}
		}

		/// <summary>
		/// Creates or overwrites a font_package.bin file.
		/// </summary>
		/// <param name="fonts">The fonts to package.</param>
		/// <param name="orders">The engine order to use.</param>
		/// <param name="path">The path to the font_package.bin to be created.</param>
		/// <param name="format">The target format.</param>
		public static void Write(List<BlamFont> fonts, List<int> orders, string path, FileFormat format)
		{
			int maxcount = (format & FileFormat.Max64) != 0 ? 64 : 16;

			int chunksize = 0x8000;
			if (format.HasFlag(FileFormat.ChunkC))
				chunksize = 0xC000;
			else if (format.HasFlag(FileFormat.Chunk10))
				chunksize = 0x10000;

			BlockWriter blw = new BlockWriter(chunksize, format);
			blw.Write(fonts);

			byte[] BlockOutput = blw.GetBlocks();
			List<WritingFontInfo> FontInfos = blw.FontInfos;
			List<BlockInfo> BlockInfos = blw.BlockInfos;
			blw.Dispose();

			byte[] FontHeaderOutput;
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter bw = new BinaryWriter(ms))
				{
					for (int i = 0; i < fonts.Count; i++)
					{
						if (i >= maxcount)
							break;

						BlamFont f = fonts[i];

						int startoffset = (int)ms.Position;

						using (MemoryStream fms = new MemoryStream())
						{
							using (BinaryWriter fbw = new BinaryWriter(fms))
							{
								fbw.Write(format.HasFlag(FileFormat.x64Head) ? FontVersionX64 : FontVersion360);

								string trimname = f.Name;
								if (trimname.Length > 32)
									trimname = trimname.Substring(0, 32);

								int basesize = format.HasFlag(FileFormat.x64Head) ? 0x168 : 0x15C;
								
								byte[] namebytes = new byte[32];
								Array.Copy(Encoding.ASCII.GetBytes(trimname), namebytes, trimname.Length);
								fbw.Write(namebytes);

								fbw.Write(f.AscendHeight);
								fbw.Write(f.DescendHeight);
								fbw.Write(f.LeadHeight);
								fbw.Write(f.LeadWidth);
								fbw.Write(basesize);
								fbw.Write(f.KerningPairs.Count);

								byte[] kernindexes = new byte[0x100];

								byte[] kernpairs;
								using (MemoryStream kms = new MemoryStream())
								{
									using (BinaryWriter kbw = new BinaryWriter(kms))
									{
										int paircount = 0;
										for (int k = 0; k < 0x100; k++)
										{
											List<KerningPair> matchingpairs = f.KerningPairs.Where(x=>x.Character == k).ToList();

											kernindexes[k] = (byte)paircount;

											foreach (KerningPair kp in matchingpairs)
											{
												kbw.Write(kp.TargetCharacter);
												kbw.Write((sbyte)kp.Value);
												paircount++;
											}

										}
										kernpairs = kms.ToArray();
									}
								}

								fbw.Write(kernindexes);

								fbw.Write(basesize + kernpairs.Length);

								ushort indexcount = (ushort)(f.Characters.Last().UnicIndex + 1);

								fbw.Write((int)indexcount);
								fbw.Write(f.Characters.Count);

								int indexsize = (format.HasFlag(FileFormat.x64Head)) ? 8 : 4;

								fbw.Write((basesize + kernpairs.Length + (indexcount * indexsize)));
								fbw.Write(FontInfos[i].BlockDataSize);

								if (format.HasFlag(FileFormat.x64Head))
								{
									//seems like just 1 cant be null, but doesnt seem to be a long
									if (f.Unknown6 == -1 || f.Unknown6b == -1)
									{
										fbw.Write((int)-1);
										fbw.Write((int)-1);
									}
									else
									{
										fbw.Write(f.Unknown6);
										fbw.Write(f.Unknown6b);
									}

								}
								else
									fbw.Write(f.Unknown6 != -1 ? f.Unknown6 : -1);

								fbw.Write(f.Unknown7 != 0 ? f.Unknown7 : 1);
								fbw.Write(f.Unknown8 != 0 ? f.Unknown8 : 1); //cannot figure out what these are for, but seems bullshit-able

								fbw.Write(FontInfos[i].CompressedSize);
								fbw.Write(FontInfos[i].DecompressedSize);

								if (format.HasFlag(FileFormat.x64Head))
								{
									fbw.Write(f.MCCScale != 0 ? f.MCCScale : 0x1);
									fbw.Write(f.UnknownMCC2 != 0 ? f.UnknownMCC2 : 0x0);
								}

								fbw.Write(kernpairs);

								FontInfos[i].Offset = startoffset;
								FontInfos[i].Size = (int)fms.Position;
								
								bw.Write(fms.ToArray());
							}
						}

					}

					FontHeaderOutput = ms.ToArray();
				}
			}

			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
			{
				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					int orderstart = 8 + (maxcount * 0xC);
					int headerstart = orderstart + (maxcount * 4) + 0x10;

					if (format.HasFlag(FileFormat.x64Head | FileFormat.x64Char))
						bw.Write(PackageVersionX64);
					else if (format.HasFlag(FileFormat.x64Head))
						bw.Write(PackageVersionX64H2A);
					else
						bw.Write(PackageVersion360);

					bw.Write(fonts.Count);
					for (int i = 0; i < maxcount; i++)
					{
						if (i < FontInfos.Count)
						{
							bw.Write(FontInfos[i].Offset + headerstart);
							bw.Write(FontInfos[i].Size);
							bw.Write(FontInfos[i].StartBlock);
							bw.Write(FontInfos[i].BlockCount);
						}	
						else
							bw.Write(new byte[0xC]);
					}

					for (int i = 0; i < maxcount; i++)
					{
						if (i < orders.Count)
							bw.Write(orders[i]);
						else
							bw.Write(-1);
					}
					int fontstart = (int)fs.Position + 0x10;
					bw.Write(fontstart);
					bw.Write(FontHeaderOutput.Length);
					bw.Write(fontstart + FontHeaderOutput.Length);
					bw.Write(BlockInfos.Count);
					bw.Write(FontHeaderOutput);
					foreach (BlockInfo bi in BlockInfos)
					{
						bw.Write(bi.StartIndex.UnicIndex);
						bw.Write(bi.StartIndex.FontIndex);
						bw.Write(bi.EndIndex.UnicIndex);
						bw.Write(bi.EndIndex.FontIndex);
					}

					fs.Position = chunksize;
					bw.Write(BlockOutput);

				}
			}
	
		}

		private class Block
		{
			public List<BlockChar> BlockCharacters = new List<BlockChar>();

			public void Read(BinaryReader br, int offset, FileFormat format)
			{
				br.BaseStream.Position = offset;

				short version = br.ReadInt16();
				int charCount = br.ReadInt16();
				short charTableSize = br.ReadInt16();
				int charDataSize = br.ReadInt16();

				for (int i = 0; i < charCount; i++)
				{
					var _charindex = br.ReadUInt16();
					var _fontindex = br.ReadInt16();
					int _offset = br.ReadInt32();

					BlockChar bc = new BlockChar(_fontindex, _offset, new BlamCharacter(_charindex));

					BlockCharacters.Add(bc);
				}

				foreach (BlockChar bc in BlockCharacters)
				{
					br.BaseStream.Position = offset + bc.DataOffset;

					int datalength = 0;
					if ((format.HasFlag(FileFormat.x64Char)))
					{
						bc.Character.DisplayWidth = br.ReadUInt32();
						datalength = br.ReadInt32();
					}
					else
					{
						bc.Character.DisplayWidth = br.ReadUInt16();
						datalength = br.ReadUInt16();
					}

					bc.Character.Width = br.ReadUInt16();
					bc.Character.Height = br.ReadUInt16();
					bc.Character.OriginX = br.ReadInt16();
					bc.Character.OriginY = br.ReadInt16();

					bc.Character.CompressedData = br.ReadBytes(datalength);
				}
				
			}
		}

		private class BlockWriter : IDisposable
		{
			int fontindex = 0;
			int chunksize;

			List<CharDatum> blockchars = new List<CharDatum>();
			List<int> blockcharsizes = new List<int>();

			short charcount = 0;
			ushort lastChar = 0;
			FileFormat fmt;

			MemoryStream ms;
			BinaryWriter bw;

			MemoryStream cms;
			BinaryWriter cbw;

			MemoryStream dms;
			BinaryWriter dbw;

			public List<WritingFontInfo> FontInfos;
			public List<BlockInfo> BlockInfos;

			BlockInfo currentblock { get; set; }

			public BlockWriter(int chunk, FileFormat format)
			{
				chunksize = chunk;
				fmt = format;
				ms = new MemoryStream();
				bw = new BinaryWriter(ms);
				Reset();

				FontInfos = new List<WritingFontInfo>();
				BlockInfos = new List<BlockInfo>();
				currentblock = new BlockInfo();
			}

			private void Reset()
			{
				cms = new MemoryStream();
				cbw = new BinaryWriter(cms);

				dms = new MemoryStream();
				dbw = new BinaryWriter(dms);
			}

			public void Write(List<BlamFont> fonts)
			{
				int maxcount = (fmt & FileFormat.Max64) != 0 ? 64 : 16;

				currentblock.StartIndex = new CharDatum() { UnicIndex = fonts[0].Characters[0].UnicIndex, FontIndex = 0 };
				for (int i = 0; i < fonts.Count; i++)
				{
					if (i >= maxcount)
						break;

					fontindex = i;
					WritingFontInfo font = new WritingFontInfo();
					font.StartBlock = (short)BlockInfos.Count;

					foreach (BlamCharacter bc in fonts[i].Characters)
					{

						font.CompressedSize += bc.CompressedSize;
						font.DecompressedSize += bc.DecompressedSize / 2;//16bpp

						AddChar(bc, i);

						font.BlockDataSize += blockcharsizes.Last();
							
					}

					font.BlockCount = (short)(BlockInfos.Count - font.StartBlock + 1);
					FontInfos.Add(font);
				}

				WriteBlock();
			}

			private void AddChar(BlamCharacter bc, int font)
			{
				using (MemoryStream tms = new MemoryStream())
				{
					using (BinaryWriter tbw = new BinaryWriter(tms))
					{
						CharDatum c = new CharDatum() { UnicIndex = bc.UnicIndex, FontIndex = (short)font };

						if (fmt.HasFlag(FileFormat.x64Char))
						{
							tbw.Write(bc.DisplayWidth);
							tbw.Write(bc.CompressedSize);
						}
						else
						{
							tbw.Write((ushort)bc.DisplayWidth);
							tbw.Write((short)bc.CompressedSize);
						}
						tbw.Write(bc.Width);
						tbw.Write(bc.Height);
						tbw.Write(bc.OriginX);
						tbw.Write(bc.OriginY);
						tbw.Write(bc.CompressedData);

						bool wontfit = ((charcount * 8) + //calculated char table length
							dms.Length + //written data length
							tms.Length + 8 //current char data + table entry
							> chunksize - 8); //block without header

						if (wontfit)
						{
							WriteBlock();
							currentblock.StartIndex = new CharDatum() { UnicIndex = bc.UnicIndex, FontIndex = (short)font };
						}

						blockchars.Add(c);

						if (tms.Length % 8 != 0)
						{
							int remainder = (int)tms.Length % 8;
							tms.SetLength(tms.Length + (8 - remainder));
						}

						dbw.Write(tms.ToArray());

						blockcharsizes.Add((int)tms.Length);

						charcount += 1;
						lastChar = bc.UnicIndex;

					}
				}
				
			}

			private void WriteBlock()
			{
				var chartableoffset = 8;
				for (int k = 0; k < blockchars.Count; k++)
				{
					cbw.Write(blockchars[k].UnicIndex);
					cbw.Write(blockchars[k].FontIndex);
					cbw.Write((charcount * 8) + chartableoffset);
					chartableoffset += blockcharsizes[k];
				}

				blockchars.Clear();
				blockcharsizes.Clear();

				bw.Write(BlockVersion);
				bw.Write(charcount);
				bw.Write((short)(cms.Length + 8));
				bw.Write((short)(dms.Length));
				bw.Write(cms.ToArray());
				bw.Write(dms.ToArray());

				if (ms.Length % chunksize != 0)
				{
					int remainder = (int)ms.Length % chunksize;
					ms.SetLength(ms.Length + (chunksize - remainder));
					ms.Position = ms.Length;
				}

				currentblock.EndIndex = new CharDatum() { UnicIndex = lastChar, FontIndex = (short)fontindex };

				BlockInfos.Add(currentblock);

				currentblock = new BlockInfo();

				charcount = 0;

				Reset();
			}

			public byte[] GetBlocks()
			{
				return ms.ToArray();
			}

			public void Dispose()
			{
				bw.Dispose();
				cbw.Dispose();
				dbw.Dispose();
			}
		}

		private class FontInfo
		{
			public int Offset { get; set; }
			public int Size { get; set; }

			public short StartBlock { get; set; }
			public short BlockCount { get; set; }

		}

		private class WritingFontInfo : FontInfo
		{
			public int CompressedSize { get; set; }
			public int DecompressedSize { get; set; }

			public int BlockDataSize { get; set; }
			
		}

		private class BlockInfo
		{
			public CharDatum StartIndex { get; set; }
			public CharDatum EndIndex { get; set; }
		}

		private class BlockChar
		{
			public int FontIndex { get; set; }
			public int DataOffset { get; set; }
			public BlamCharacter Character { get; set; }

			public BlockChar(int font, int off, BlamCharacter bc)
			{
				FontIndex = font;
				DataOffset = off;
				Character = bc;
			}
		}

		private class CharDatum
		{
			public ushort UnicIndex { get; set; }
			public short FontIndex { get; set; }
		}

	}
}
