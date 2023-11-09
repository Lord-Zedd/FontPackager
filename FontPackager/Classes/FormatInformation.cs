using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FontPackager.Classes
{
	/// <summary>
	/// Defines the output format.
	/// </summary>
	public enum FileFormat
	{
		Tag, //read only
		Table,
		Package
	}

	/// <summary>
	/// Flags that should only pertain to <see cref="FileFormat.Package"/>
	/// </summary>
	[Flags]
	public enum FormatFlags
	{
		None = 0,
		Max64Fonts = 0x1,
		x64Header = 0x2,
		x64Character = 0x4,
		GenericVersion = 0x8,

		MCC = Max64Fonts | x64Header | x64Character,
		Groundhog = Max64Fonts | x64Header,
		H4MCC = Max64Fonts | x64Header | GenericVersion
	}

	/// <summary>
	/// Chunk size of <see cref="FileFormat.Package"/>.
	/// </summary>
	public enum ChunkSize
	{
		None,
		Size8000,
		SizeC000,
		Size10000
	}

	/// <summary>
	/// Hard decompressed size limit of <see cref="FileFormat.Table"/>
	/// </summary>
	public enum PixelLimit
	{
		None,
		Limit4000,
		Limit20000,
		Limit80000,
		Limit100000
	}

	/// <summary>
	/// Hard resolution limit of <see cref="FileFormat.Package"/>
	/// </summary>
	public enum ResolutionLimit
	{
		None,
		Limit256x64,
		Limit256x67,
		Limit256x56,
		Limit784x512
	}

	/// <summary>
	/// Contains information about a font format
	/// </summary>
	public class FormatInformation
	{
		public FileFormat Format { get; set; }
		public FormatFlags Flags { get; set; }
		public ChunkSize ChunkSize { get; set; }
		public PixelLimit PixelLimit { get; set; }
		public ResolutionLimit ResolutionLimit { get; set; }

		public FormatInformation(FileFormat format, FormatFlags flags, ChunkSize chunkSize, PixelLimit pixelLimit, ResolutionLimit resolutionLimit)
		{
			Format = format;
			Flags = flags;
			ChunkSize = chunkSize;
			PixelLimit = pixelLimit;
			ResolutionLimit = resolutionLimit;
		}

		public FormatInformation(FileFormat format)
		{
			Format = format;
			ResolutionLimit = ResolutionLimit.Limit256x64;
		}

		#region get
		/// <summary>
		/// Gets the maximum font count of this format based on <see cref="FormatFlags"/>.
		/// </summary>
		public int MaximumFontCount
		{
			get
			{
				if (Format == FileFormat.Table)
					return 12;
				else if (Format == FileFormat.Package)
					return Flags.HasFlag(FormatFlags.Max64Fonts) ? 64 : 16;
				else
					throw new ArgumentException("Requested a maximum font count for the Tag format.");
			}
		}

		/// <summary>
		/// Gets the base (without kerning pairs) length of the font header of this package format based on <see cref="FormatFlags"/>.
		/// </summary>
		public int PackageFontHeaderBaseLength
		{
			get
			{
				if (Format == FileFormat.Package)
					return Flags.HasFlag(FormatFlags.x64Header) ? 0x168 : 0x15C;
				else
					throw new ArgumentException("Requested a font header length for a format other than Package.");
			}
		}

		/// <summary>
		/// Gets the length of the character infomation of this package format based on <see cref="FormatFlags"/>.
		/// </summary>
		public int PackageCharacterInfoLength
		{
			get
			{
				if (Format == FileFormat.Package)
					return Flags.HasFlag(FormatFlags.x64Character) ? 0x10 : 0xC;
				else
					throw new ArgumentException("Requested a character info length for a format other than Package.");
			}
		}

		/// <summary>
		/// Gets the chunk size of this format based on <see cref="ChunkSize"/>.
		/// </summary>
		public int ChunkSizeValue
		{
			get
			{
				if (Format != FileFormat.Package)
					throw new ArgumentException("Requested a chunk size for a format other than Package.");

				switch (ChunkSize)
				{
					case ChunkSize.None:
						throw new ArgumentException("Requested a chunk size of 0 for a Package format. Impossible.");
					default:
					case ChunkSize.Size8000:
						return 0x8000;
					case ChunkSize.SizeC000:
						return 0xC000;
					case ChunkSize.Size10000:
						return 0x10000;

				}
			}
		}

		/// <summary>
		/// Gets the pixel limit of this format based on <see cref="PixelLimit"/>.
		/// </summary>
		public int PixelLimitValue
		{
			get
			{
				switch (PixelLimit)
				{
					default:
					case PixelLimit.None:
						return int.MaxValue;
					case PixelLimit.Limit4000:
						return 0x4000;
					case PixelLimit.Limit20000:
						return 0x20000;
					case PixelLimit.Limit80000:
						return 0x80000;
					case PixelLimit.Limit100000:
						return 0x100000;

				}
			}
		}

		/// <summary>
		/// Gets the maximum character width of this format based on <see cref="ResolutionLimit"/>.
		/// </summary>
		public int ResolutionLimitWidth
		{
			get
			{
				switch (ResolutionLimit)
				{
					default:
					case ResolutionLimit.None:
						return ushort.MaxValue;
					case ResolutionLimit.Limit256x56:
					case ResolutionLimit.Limit256x64:
						return 256;
					case ResolutionLimit.Limit784x512:
						return 784;

				}
			}
		}

		/// <summary>
		/// Gets the maximum character height of this format based on <see cref="ResolutionLimit"/>.
		/// </summary>
		public int ResolutionLimitHeight
		{
			get
			{
				switch (ResolutionLimit)
				{
					default:
					case ResolutionLimit.None:
						return ushort.MaxValue;
					case ResolutionLimit.Limit256x56:
						return 56;
					case ResolutionLimit.Limit256x64:
						return 64;
					case ResolutionLimit.Limit784x512:
						return 512;

				}
			}
		}

		/// <summary>
		/// Gets the maximum character display width of this format based on <see cref="Format"/>.
		/// </summary>
		public uint MaximumDisplayWidth
		{
			get
			{
				return Flags.HasFlag(FormatFlags.x64Character) ? uint.MaxValue : ushort.MaxValue;
			}
		}
		#endregion

		#region static definitions
		/// <summary>
		/// Halo 2 (Xbox)
		/// </summary>
		public static FormatInformation H2X { get; } = new FormatInformation(FileFormat.Table, FormatFlags.None, ChunkSize.None, PixelLimit.Limit4000, ResolutionLimit.None);
		/// <summary>
		/// Halo 2 (Vista)
		/// </summary>
		public static FormatInformation H2V { get; } = new FormatInformation(FileFormat.Table, FormatFlags.None, ChunkSize.None, PixelLimit.Limit20000, ResolutionLimit.None);
		/// <summary>
		/// Halo 3 Beta (360)
		/// </summary>
		public static FormatInformation H3B { get; } = new FormatInformation(FileFormat.Table, FormatFlags.None, ChunkSize.None, PixelLimit.None, ResolutionLimit.Limit256x56);

		/// <summary>
		/// Halo 2 (MCC)
		/// </summary>
		public static FormatInformation H2MCC { get; } = new FormatInformation(FileFormat.Table, FormatFlags.None, ChunkSize.None, PixelLimit.Limit80000, ResolutionLimit.None);

		/// <summary>
		/// Halo 3, ODST, Reach and anything in between. (360)
		/// </summary>
		public static FormatInformation GenericPackage { get; } = new FormatInformation(FileFormat.Package, FormatFlags.None, ChunkSize.Size8000, PixelLimit.None, ResolutionLimit.Limit256x64);
		/// <summary>
		/// Halo 4 Beta (360)
		/// </summary>
		public static FormatInformation H4B { get; } = new FormatInformation(FileFormat.Package, FormatFlags.Max64Fonts, ChunkSize.Size8000, PixelLimit.None, ResolutionLimit.Limit256x64);
		/// <summary>
		/// Halo 4 (360), Halo 5 Forge (PC, is barely used by the game)
		/// </summary>
		public static FormatInformation H4 { get; } = new FormatInformation(FileFormat.Package, FormatFlags.Max64Fonts, ChunkSize.SizeC000, PixelLimit.None, ResolutionLimit.Limit256x67);

		/// <summary>
		/// Halo 3, ODST, Reach. (MCC)
		/// </summary>
		public static FormatInformation GenericMCC { get; } = new FormatInformation(FileFormat.Package, FormatFlags.MCC, ChunkSize.SizeC000, PixelLimit.None, ResolutionLimit.Limit784x512);
		/// <summary>
		/// Halo 2 Anniversary Multiplayer (MCC)
		/// </summary>
		public static FormatInformation H2AMCC { get; } = new FormatInformation(FileFormat.Package, FormatFlags.Groundhog, ChunkSize.Size10000, PixelLimit.Limit100000, ResolutionLimit.Limit784x512);
		/// <summary>
		/// Halo 4 (MCC)
		/// </summary>
		public static FormatInformation H4MCC { get; } = new FormatInformation(FileFormat.Package, FormatFlags.H4MCC, ChunkSize.Size10000, PixelLimit.Limit100000, ResolutionLimit.Limit784x512);
		#endregion

		#region overrides
		public static bool operator ==(FormatInformation a, FormatInformation b)
		{
			if (a is null)
				return b is null;

			return a.Equals(b);
		}

		public static bool operator !=(FormatInformation a, FormatInformation b)
		{
			return !(a == b);
		}

		public override bool Equals(object obj)
		{
			if (obj != null && obj is FormatInformation info)
			{
				return
					GetHashCode() == info.GetHashCode();
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			//6 bits per member should be good
			return
				(int)Format << 24 |
				(int)Flags << 18 |
				(int)ChunkSize << 12 |
				(int)PixelLimit << 6 |
				(int)ResolutionLimit;

		}
		#endregion
	}
}
