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
		public static Tuple<IOError, FormatInformation, List<BlamFont>, List<int>> Read(string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Open))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					uint pversion = br.ReadUInt32();
					if (pversion != PackageVersion360 && pversion != PackageVersionX64 && pversion != PackageVersionX64H2A)
					{
						return new Tuple<IOError, FormatInformation, List<BlamFont>, List<int>>
							(IOError.BadVersion, null, null, null);
					}

					int fontcount = br.ReadInt32();
					if (fontcount <= 0)
					{
						return new Tuple<IOError, FormatInformation, List<BlamFont>, List<int>>
							(IOError.Empty, null, null, null);
					}

					List<BlamFont> fonts = new List<BlamFont>();
					List<int> orders = new List<int>();

					FormatInformation info = new FormatInformation(FileFormat.Package);

					int maxcounthack = br.ReadInt32();
					if (maxcounthack == 1048)
						info.Flags |= FormatFlags.Max64Fonts;

					bool confirmedChunkSize = false;

					if (pversion == PackageVersionX64)
						info = FormatInformation.GenericMCC;
					else if (pversion == PackageVersionX64H2A)
					{
						info = FormatInformation.H2AMCC;
						confirmedChunkSize = true;
					}

					br.BaseStream.Position -= 4;
					List<FontInfo> fontinfo = new List<FontInfo>();

					for (int i = 0; i < info.MaximumFontCount; i++)
					{
						fontinfo.Add(new FontInfo()
						{
							Offset = br.ReadInt32(),
							Size = br.ReadInt32(),
							StartBlock = br.ReadInt16(),
							BlockCount = br.ReadInt16()
						});
					}

					for (int i = 0; i < info.MaximumFontCount; i++)
						orders.Add(br.ReadInt32());

					int headerTableOffset = br.ReadInt32();
					int headerTableSize = br.ReadInt32();
					int BlockRangesOffset = br.ReadInt32();
					int blockCount = br.ReadInt32();

					br.BaseStream.Position = BlockRangesOffset;

					List<Block> blocks = new List<Block>();

					//iterate each possible block size until we get a result
					if (!confirmedChunkSize && fs.Length > 0x8002)
					{
						fs.Position = 0x8000;
						if (br.ReadInt16() == BlockVersion)
						{
							info.ChunkSize = ChunkSize.Size8000;
							confirmedChunkSize = true;
						}
					}

					if (!confirmedChunkSize && fs.Length > 0xC002)
					{
						fs.Position = 0xC000;
						if (br.ReadInt16() == BlockVersion)
						{
							info.ChunkSize = ChunkSize.SizeC000;
							confirmedChunkSize = true;
						}
					}

					if (!confirmedChunkSize && fs.Length > 0x10002)
					{
						fs.Position = 0x10000;
						if (br.ReadInt16() == BlockVersion)
						{
							// h4mcc uses the groundhog format but the regular mcc package version, so correct it if needed
							if (info == FormatInformation.GenericMCC)
								info = FormatInformation.H4MCC;
							else
								info.ChunkSize = ChunkSize.Size10000;

							confirmedChunkSize = true;
						}
					}

					if (info.ChunkSize == ChunkSize.None)
						return new Tuple<IOError, FormatInformation, List<BlamFont>, List<int>>
							(IOError.UnknownBlock, null, null, null);

					for (int i = 0; i < blockCount; i++)
					{
						Block dataBlock = new Block();
						dataBlock.Read(br, info.ChunkSizeValue + (i * info.ChunkSizeValue), info);

						blocks.Add(dataBlock);
					}

					for (int i = 0; i < fontcount; i++)
					{
						br.BaseStream.Position = fontinfo[i].Offset;

						uint fversion = br.ReadUInt32();

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

						VirtualChar FallbackCharacter = null;

						if (info.Flags.HasFlag(FormatFlags.x64Header))
						{
							ulong fallback = br.ReadUInt64();
							if (fallback != 0xFFFFFFFFFFFFFFFF)
								FallbackCharacter = new VirtualChar(fallback);
						}
						else
						{
							uint fallback = br.ReadUInt32();
							if (fallback != 0xFFFFFFFF)
								FallbackCharacter = new VirtualChar(fallback);
						}

						int MaxCompressedSize = br.ReadInt32();
						int MaxDecompressedSize = br.ReadInt32();
						int CompressedSize = br.ReadInt32();
						int DecompressedSize = br.ReadInt32();

						if (info.Flags.HasFlag(FormatFlags.x64Header))
						{
							tempfont.MCCScale = br.ReadInt32();
							tempfont.UnknownMCC = br.ReadInt32();
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

						for (int bl = 0; bl < (fontinfo[i].StartBlock + fontinfo[i].BlockCount); bl++)
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
					return new Tuple<IOError, FormatInformation, List<BlamFont>, List<int>>
							(IOError.None, info, fonts, orders);
				}
			}
		}

		/// <summary>
		/// Creates or overwrites a font_package.bin file.
		/// </summary>
		/// <param name="fonts">The fonts to package.</param>
		/// <param name="orders">The engine order to use.</param>
		/// <param name="path">The path to the font_package.bin to be created.</param>
		/// <param name="info">The target format.</param>
		public static void Write(List<BlamFont> fonts, List<int> orders, string path, FormatInformation info)
		{
			BlockWriter blw = new BlockWriter(info);
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
						if (i >= info.MaximumFontCount)
							break;

						BlamFont f = fonts[i];

						int startoffset = (int)ms.Position;

						using (MemoryStream fms = new MemoryStream())
						{
							using (BinaryWriter fbw = new BinaryWriter(fms))
							{
								fbw.Write(info.Flags.HasFlag(FormatFlags.x64Header) ? FontVersionX64 : FontVersion360);

								string trimname = f.Name;
								if (trimname.Length > 32)
									trimname = trimname.Substring(0, 32);

								byte[] namebytes = new byte[32];
								Array.Copy(Encoding.ASCII.GetBytes(trimname), namebytes, trimname.Length);
								fbw.Write(namebytes);

								fbw.Write(f.AscendHeight);
								fbw.Write(f.DescendHeight);
								fbw.Write(f.LeadHeight);
								fbw.Write(f.LeadWidth);
								fbw.Write(info.PackageFontHeaderBaseLength);
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
								
								fbw.Write(info.PackageFontHeaderBaseLength + kernpairs.Length);

								ushort indexcount = (ushort)(f.Characters.Last().UnicIndex + 1);

								fbw.Write((int)indexcount);
								fbw.Write(f.Characters.Count);

								int indexsize = info.Flags.HasFlag(FormatFlags.x64Header) ? 8 : 4;

								fbw.Write(info.PackageFontHeaderBaseLength + kernpairs.Length + (indexcount * indexsize));
								fbw.Write(FontInfos[i].BlockDataSize);

								if (info.Flags.HasFlag(FormatFlags.x64Header))
								{
									if (FontInfos[i].FallbackCharacter != null)
										fbw.Write(FontInfos[i].FallbackCharacter.PackDatum64());
									else
										fbw.Write((long)-1);
								}
								else
								{
									if (FontInfos[i].FallbackCharacter != null)
										fbw.Write(FontInfos[i].FallbackCharacter.PackDatum32());
									else
										fbw.Write(-1);
								}

								fbw.Write(FontInfos[i].MaxCompressedSize);
								fbw.Write(FontInfos[i].MaxDecompressedSize);

								fbw.Write(FontInfos[i].CompressedSize);
								fbw.Write(FontInfos[i].DecompressedSize);

								if (info.Flags.HasFlag(FormatFlags.x64Header))
								{
									fbw.Write(f.MCCScale != 0 ? f.MCCScale : 0x1);
									fbw.Write(f.UnknownMCC);
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
					int orderstart = 8 + (info.MaximumFontCount * 0xC);
					int headerstart = orderstart + (info.MaximumFontCount * 4) + 0x10;

					if (info.Flags.HasFlag(FormatFlags.GenericVersion) || info.Flags.HasFlag(FormatFlags.x64Header | FormatFlags.x64Character))
						bw.Write(PackageVersionX64);
					else if (info.Flags.HasFlag(FormatFlags.x64Header))
						bw.Write(PackageVersionX64H2A);
					else
						bw.Write(PackageVersion360);

					bw.Write(fonts.Count);
					for (int i = 0; i < info.MaximumFontCount; i++)
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

					for (int i = 0; i < info.MaximumFontCount; i++)
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

					fs.Position = info.ChunkSizeValue;
					bw.Write(BlockOutput);
				}
			}
		}

		private class Block
		{
			public List<BlockChar> BlockCharacters = new List<BlockChar>();

			public void Read(BinaryReader br, int offset, FormatInformation info)
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
					if (info.Flags.HasFlag(FormatFlags.x64Character))
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
			int FontIndex = 0;

			List<CharDatum> BlockChars = new List<CharDatum>();
			List<int> BlockCharSizes = new List<int>();

			ushort LastChar = 0;
			FormatInformation Info;

			MemoryStream ms;
			BinaryWriter bw;

			MemoryStream cms;
			BinaryWriter cbw;

			MemoryStream dms;
			BinaryWriter dbw;

			public List<WritingFontInfo> FontInfos;
			public List<BlockInfo> BlockInfos;

			BlockInfo CurrentBlock { get; set; }

			public BlockWriter(FormatInformation info)
			{
				Info = info;
				ms = new MemoryStream();
				bw = new BinaryWriter(ms);
				Reset();

				FontInfos = new List<WritingFontInfo>();
				BlockInfos = new List<BlockInfo>();
				CurrentBlock = new BlockInfo();
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
				CurrentBlock.StartIndex = new CharDatum() { UnicIndex = fonts[0].Characters[0].UnicIndex, FontIndex = 0 };
				for (int i = 0; i < fonts.Count; i++)
				{
					if (i >= Info.MaximumFontCount)
						break;

					FontIndex = i;
					WritingFontInfo font = new WritingFontInfo();
					font.StartBlock = (short)BlockInfos.Count;

					foreach (BlamCharacter bc in fonts[i].Characters)
					{

						font.CompressedSize += bc.CompressedSize;
						int adjustedDecomp = bc.DecompressedSize / 2;//16bpp
						font.DecompressedSize += adjustedDecomp;

						font.MaxCompressedSize = Math.Max(bc.CompressedSize, font.MaxCompressedSize);
						font.MaxDecompressedSize = Math.Max(adjustedDecomp, font.MaxDecompressedSize);

						int maximumCharSize = Info.ChunkSizeValue - 8 - Info.PackageCharacterInfoLength;

						if (bc.CompressedSize > maximumCharSize)
							throw new ArgumentException(
								"Character " + bc.UnicIndex.ToString("X4") + " has a compressed size " + bc.CompressedSize.ToString("X") +
								" larger than the maximum size " + maximumCharSize.ToString("X") + " based on the package format's ChunkSize and cannot physically fit anywhere.");

						AddChar(bc, i);

						if (bc.UnicIndex == 0x25A1)
							font.FallbackCharacter = new VirtualChar(font.BlockDataSize, BlockCharSizes.Last());

						font.BlockDataSize += BlockCharSizes.Last();
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

						if (Info.Flags.HasFlag(FormatFlags.x64Character))
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

						bool wontfit = (BlockChars.Count * 8) + //calculated char table length
							dms.Length + //written data length
							tms.Length + 8 //current char data + table entry
							> Info.ChunkSizeValue - 8; //block without header

						if (wontfit)
						{
							WriteBlock();
							CurrentBlock.StartIndex = c;
						}

						BlockChars.Add(c);

						int lengthtest = (int)tms.Length;

						if (tms.Length % 8 != 0)
						{
							int remainder = (int)tms.Length % 8;
							tms.SetLength(tms.Length + (8 - remainder));
						}

						dbw.Write(tms.ToArray());

						BlockCharSizes.Add((int)tms.Length);

						LastChar = bc.UnicIndex;
					}
				}
			}

			private void WriteBlock()
			{
				var chartableoffset = 8;
				for (int k = 0; k < BlockChars.Count; k++)
				{
					cbw.Write(BlockChars[k].UnicIndex);
					cbw.Write(BlockChars[k].FontIndex);
					cbw.Write((BlockChars.Count * 8) + chartableoffset);
					chartableoffset += BlockCharSizes[k];
				}

				bw.Write(BlockVersion);
				bw.Write((short)BlockChars.Count);
				bw.Write((short)(cms.Length + 8));
				bw.Write((short)dms.Length);
				bw.Write(cms.ToArray());
				bw.Write(dms.ToArray());

				BlockChars.Clear();
				BlockCharSizes.Clear();

				if (ms.Length % Info.ChunkSizeValue != 0)
				{
					int remainder = (int)ms.Length % Info.ChunkSizeValue;
					ms.SetLength(ms.Length + (Info.ChunkSizeValue - remainder));
					ms.Position = ms.Length;
				}

				CurrentBlock.EndIndex = new CharDatum() { UnicIndex = LastChar, FontIndex = (short)FontIndex };

				BlockInfos.Add(CurrentBlock);

				CurrentBlock = new BlockInfo();

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

		/// <summary>
		/// An entry from the package header containing the location of a font header and the blocks it uses. Used for reading.
		/// </summary>
		private class FontInfo
		{
			public int Offset { get; set; }
			public int Size { get; set; }

			public short StartBlock { get; set; }
			public short BlockCount { get; set; }
		}

		/// <summary>
		/// An extended version of FontInfo to hold various values used in the font header calculated during write.
		/// </summary>
		private class WritingFontInfo : FontInfo
		{
			public int MaxCompressedSize { get; set; }
			public int MaxDecompressedSize { get; set; }

			public int CompressedSize { get; set; }
			public int DecompressedSize { get; set; }

			public int BlockDataSize { get; set; }
			public VirtualChar FallbackCharacter { get; set; }
		}

		/// <summary>
		/// An entry from the package header containing information about a block. Used for reading/writing.
		/// </summary>
		private class BlockInfo
		{
			public CharDatum StartIndex { get; set; }
			public CharDatum EndIndex { get; set; }
		}

		/// <summary>
		/// An entry in the table at the start of a block defining each character. Used for reading.
		/// </summary>
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

		/// <summary>
		/// A datum for characters in a block.
		/// </summary>
		private class CharDatum
		{
			public ushort UnicIndex { get; set; }
			public short FontIndex { get; set; }
		}

		/// <summary>
		/// A virtual character datum that is used for a single probably not even used value in the font header.
		/// </summary>
		private class VirtualChar
		{
			public int Offset { get; set; }
			public int Size { get; set; }

			public VirtualChar(int offset, int size)
			{
				Offset = offset;
				Size = size;
			}

			public VirtualChar(uint datum)
			{
				short packedOffset = (short)(datum & 0xFFFF);
				short packedSize = (short)(datum >> 16 & 0xFFFF);

				Offset = packedOffset << 3;
				Size = packedSize >> 2;
			}

			public VirtualChar(ulong datum)
			{
				int packedOffset = (int)(datum & 0xFFFFFFFF);
				int packedSize = (int)(datum >> 32 & 0xFFFFFFFF);

				Offset = packedOffset << 3;
				Size = packedSize << 3;
			}

			public uint PackDatum32()
			{
				int packedOffset = Offset >> 3;
				int packedSize = Size << 2;

				uint datum = (uint)packedOffset & 0xFFFF;
				datum |= (uint)((packedSize & 0xFFFF) << 16);
				return datum;
			}

			public ulong PackDatum64()
			{
				int packedOffset = Offset >> 3;
				int packedSize = Size >> 3;

				ulong datum = (uint)packedOffset & 0xFFFFFFFF;
				datum |= (ulong)(uint)(packedSize & 0xFFFFFFFF) << 32;
				return datum;
			}
		}

	}
}
