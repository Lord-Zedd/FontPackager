using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace FontPackager.Classes
{
	public enum IOError
	{
		None = 0,
		BadVersion = 1,
		UnknownBlock = 2,
		Empty = 3,
	}

	public enum CharTint
	{
		None,
		Cool,
		Warm,
		Custom
	}

	public static class ReaderExtensions
	{
		public static string ReadStringToNull(this BinaryReader br, int maxlength = -1)
		{
			string output = "";
			char c;

			int maximum = maxlength;

			if (maximum == -1)
				maximum = (int)br.BaseStream.Length - (int)br.BaseStream.Position;

			for (int j = 0; j < maximum; j++)
			{
				c = (char)br.ReadByte();
				if (c == 0)
				{
					if (maxlength != -1)
						br.BaseStream.Position += maximum - 1 - j;
					break;
				}

				output += c.ToString();
			}

			return output;
		}

	}

	public static class DPIHelper
	{
		public static readonly int DPI;
		public static readonly float DPIScale;

		static DPIHelper()
		{
			Matrix dpim = PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice;
			DPIScale = (float)dpim.M11;
			DPI = (int)(DPIScale * 96);
		}
	}

	public class EngineOrderItem
	{
		public BlamFont Font { get; set; }

		public EngineOrderItem(BlamFont font)
		{
			Font = font;
		}
	}

	public class VerificationResult
	{
		public bool IsCritical { get; set; }
		public string Message { get; set; }

		public VerificationResult(string msg, bool crit)
		{
			Message = msg;
			IsCritical = crit;
		}
	}

	public class TintInfo
	{
		public static TintInfo None { get; } = new TintInfo(CharTint.None);
		public static TintInfo Cool { get; } = new TintInfo(CharTint.Cool);
		public static TintInfo Warm { get; } = new TintInfo(CharTint.Warm);

		public CharTint TintType { get; set; }
		public System.Drawing.Color CustomColor { get; set; }

		public TintInfo(CharTint tint)
		{
			TintType = tint;
			CustomColor = System.Drawing.Color.White;
		}
		public TintInfo(CharTint tint, System.Drawing.Color custom)
		{
			TintType = tint;
			CustomColor = custom;
		}
	}

	class BigEndianReader : BinaryReader
	{
		private byte[] buffer = new byte[8];

		public BigEndianReader(Stream stream) : base(stream) { }

		public override short ReadInt16()
		{
			buffer = base.ReadBytes(2);
			Array.Reverse(buffer, 0, 2);
			return BitConverter.ToInt16(buffer, 0);
		}

		public override int ReadInt32()
		{
			buffer = base.ReadBytes(4);
			Array.Reverse(buffer, 0 , 4);
			return BitConverter.ToInt32(buffer, 0);
		}

		public override long ReadInt64()
		{
			buffer = base.ReadBytes(8);
			Array.Reverse(buffer);
			return BitConverter.ToInt64(buffer, 0);
		}

		public override ushort ReadUInt16()
		{
			buffer = base.ReadBytes(2);
			Array.Reverse(buffer, 0, 2);
			return BitConverter.ToUInt16(buffer, 0);
		}

		public override uint ReadUInt32()
		{
			buffer = base.ReadBytes(4);
			Array.Reverse(buffer, 0 , 4);
			return BitConverter.ToUInt32(buffer, 0);
		}

		public override float ReadSingle()
		{
			buffer = base.ReadBytes(4);
			Array.Reverse(buffer, 0 , 4);
			return BitConverter.ToSingle(buffer, 0);
		}

		public override ulong ReadUInt64()
		{
			buffer = base.ReadBytes(8);
			Array.Reverse(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}

		public override double ReadDouble()
		{
			buffer = base.ReadBytes(8);
			Array.Reverse(buffer);
			return BitConverter.ToUInt64(buffer, 0);
		}
	}

	public class BigEndianWriter : BinaryWriter
	{
		private byte[] buffer = new byte[8];

		public BigEndianWriter(Stream stream) : base(stream) { }

		public override void Write(short value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer, 0, 2);
			base.Write(buffer, 0, 2);
		}

		public override void Write(int value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer, 0, 4);
			base.Write(buffer, 0, 4);
		}

		public override void Write(long value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer);
			base.Write(buffer);
		}

		public override void Write(ushort value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer, 0, 2);
			base.Write(buffer, 0, 2);
		}

		public override void Write(uint value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer, 0, 4);
			base.Write(buffer, 0, 4);
		}

		public override void Write(ulong value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer);
			base.Write(buffer);
		}

		public override void Write(float value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer, 0, 4);
			base.Write(buffer, 0, 4);
		}

		public override void Write(double value)
		{
			buffer = BitConverter.GetBytes(value);
			Array.Reverse(buffer);
			base.Write(buffer);
		}
	}


}
