﻿using System;
using System.Collections.Generic;
using System.IO;

namespace FontPackager
{
	//Class for .abc files generated by the SDK tool FontMaker
	class abcFile
	{
		public int Version { get; set; }
		public float Height { get; set; }
		public float TopPadding { get; set; }
		public float BottomPadding { get; set; }
		public float YAdvance { get; set; }

		ushort TransCount { get; set; }

		public List<ushort> TransTable = new List<ushort>();

		public int GlyphCount { get; set; }

		public List<GlyphEntry> GlyphTable = new List<GlyphEntry>();

		public List<char> CharCodes = new List<char>();

		public void Read(BigEndianReader br)
		{
			Version = br.ReadInt32();
			Height = br.ReadSingle();
			TopPadding = br.ReadSingle();
			BottomPadding = br.ReadSingle();
			YAdvance = br.ReadSingle();

			TransCount = br.ReadUInt16();

			br.BaseStream.Position += 0x2;

			for (int i = 0; i < TransCount; i++)
			{
				ushort currentchar = br.ReadUInt16();
				TransTable.Add(currentchar);
			}

			GlyphCount = br.ReadInt32();

			for (int i = 0; i < GlyphCount; i++)
			{
				GlyphEntry gly = new GlyphEntry();

				gly.Left = br.ReadUInt16();
				gly.Top = br.ReadUInt16();
				gly.Right = br.ReadUInt16();
				gly.Bottom = br.ReadUInt16();

				gly.Offset = br.ReadUInt16();
				gly.Width = br.ReadUInt16();
				gly.Advance = br.ReadUInt16();
				gly.Mask = br.ReadUInt16();

				GlyphTable.Add(gly);
			}

			//ABC doesn't store actual unicode so we gotta convert them from the Translation Table
			TransToUnicode();
		}

		internal void TransToUnicode()
		{
			bool zero = false;
			char unic = (char)0;
			for (int i = 0; i < TransCount; i++)
			{
				if (zero == true && TransTable[i] == 0)
				{
					unic++;
					continue;
				}

				unic++;

				if (TransTable[i] == 0)
				{
					zero = true;
					CharCodes.Add((char)0);
				}
				else
					CharCodes.Add(unic);
			}
		}

		public class GlyphEntry
		{
			public ushort Left { get; set; }
			public ushort Top { get; set; }
			public ushort Right { get; set; }
			public ushort Bottom { get; set; }

			public ushort Offset { get; set; }

			public ushort Width { get; set; }

			public ushort Advance { get; set; }
			public ushort Mask { get; set; }
		}
	}

	//also big endian reader from http://www.techbliss.org/threads/big-endian-binaryreader.84/
	public class BigEndianReader : BinaryReader
	{
		private byte[] a16 = new byte[2];
		private byte[] a32 = new byte[4];
		private byte[] a64 = new byte[8];

		public BigEndianReader(Stream stream) : base(stream) { }

		public override Int16 ReadInt16()
		{
			a16 = base.ReadBytes(2);
			Array.Reverse(a16);
			return BitConverter.ToInt16(a16, 0);
		}

		public override int ReadInt32()
		{
			a32 = base.ReadBytes(4);
			Array.Reverse(a32);
			return BitConverter.ToInt32(a32, 0);
		}

		public override Int64 ReadInt64()
		{
			a64 = base.ReadBytes(8);
			Array.Reverse(a64);
			return BitConverter.ToInt64(a64, 0);
		}
		public override UInt16 ReadUInt16()
		{
			a16 = base.ReadBytes(2);
			Array.Reverse(a16);
			return BitConverter.ToUInt16(a16, 0);
		}

		public override UInt32 ReadUInt32()
		{
			a32 = base.ReadBytes(4);
			Array.Reverse(a32);
			return BitConverter.ToUInt32(a32, 0);
		}

		public override Single ReadSingle()
		{
			a32 = base.ReadBytes(4);
			Array.Reverse(a32);
			return BitConverter.ToSingle(a32, 0);
		}

		public override UInt64 ReadUInt64()
		{
			a64 = base.ReadBytes(8);
			Array.Reverse(a64);
			return BitConverter.ToUInt64(a64, 0);
		}

		public override Double ReadDouble()
		{
			a64 = base.ReadBytes(8);
			Array.Reverse(a64);
			return BitConverter.ToUInt64(a64, 0);
		}

		public string ReadStringToNull()
		{
			string result = "";
			char c;
			for (int i = 0; i < base.BaseStream.Length; i++)
			{
				if ((c = (char)base.ReadByte()) == 0)
				{
					break;
				}
				result += c.ToString();
			}
			return result;
		}
	}
}