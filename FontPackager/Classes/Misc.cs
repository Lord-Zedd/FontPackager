using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace FontPackager.Classes
{
	[Flags]
	public enum FileFormat
	{
		Tag = 0x1,
		Table = 0x2,
		Package = 0x4,

		Max64 = 0x10,
		ChunkC = 0x20,

		x64 = 0x800,

		PixelLimit4k = 0x1000,
		PixelLimit20k = 0x2000,
		PixelLimit100k = 0x4000,

		ResLimit256x56 = 0x10000,
		ResLimit768x512 = 0x20000,

		H2X = Table | PixelLimit4k,
		H2V = Table | PixelLimit20k,
		H3B = Table | ResLimit256x56,
		H4B = Package | Max64,
		H4 = Package | Max64 | ChunkC,
		MCC = Package | Max64 | ChunkC | ResLimit768x512 | PixelLimit100k | x64 //temp until moar games

	}

	public enum IOError
	{
		None = 0,
		BadVersion = 1,
		Empty = 2,
	}

	public enum CharTint
	{
		None,
		Cool,
		Warm
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
}
