using System;
using System.Windows.Media.Imaging;
using System.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.IO;

namespace FontPackager.Classes
{
	/// <summary>
	/// Defines a character within a font.
	/// </summary>
	public class BlamCharacter : INotifyPropertyChanged
	{
		public ushort UnicIndex { get; set; }

		uint _dwidth;
		public uint DisplayWidth
		{
			get { return _dwidth; }
			set { _dwidth = value; NotifyPropertyChanged("DisplayWidth"); NotifyPropertyChanged("UIDisplayWidth"); }
		}

		public ushort Width { get; set; }
		public ushort Height { get; set; }

		public byte[] CompressedData { get; set; }
		public byte[] DecompressedData { get; set; }

		short _originx;
		public short OriginX
		{
			get { return _originx; }
			set { _originx = value; NotifyPropertyChanged("OriginX"); NotifyPropertyChanged("UIOriginX"); }
		}

		short _originy;
		public short OriginY
		{
			get { return _originy; }
			set { _originy = value; NotifyPropertyChanged("OriginY"); NotifyPropertyChanged("UIOriginY"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public BlamCharacter(ushort unicindex)
		{
			UnicIndex = unicindex;
		}

		public string UTFString
		{
			get
			{
				var utf8 = Encoding.UTF8.GetBytes(Convert.ToChar(UnicIndex).ToString());
				return BitConverter.ToString(utf8).Replace("-", " ");
			}
		}

		/// <summary>
		/// Gets the compressed size of this character, compressing if needed. Should be used instead of <see cref="CompressedData"/>.Length
		/// </summary>
		public int CompressedSize
		{
			get
			{
				if (CompressedData == null)
					if (!CharacterTools.CompressData(this))
						return -1;

				return CompressedData.Length;
			}
		}

		/// <summary>
		/// Gets the decompressed size of this character, decompressing if needed. Should be used instead of <see cref="DecompressedData"/>.Length
		/// </summary>
		public int DecompressedSize
		{
			get
			{
				if (DecompressedData == null)
					if (!CharacterTools.DecompressData(this))
						return -1;

				return DecompressedData.Length;
			}
		}

		public BitmapSource Image
		{
			get
			{
				if (CompressedData == null)
					return null;

				if (DecompressedData == null)
					CharacterTools.DecompressData(this);

				if (Width > short.MaxValue || Width == 0)
					return null;

				return BitmapSource.Create(Width, Height, DPIHelper.DPI, DPIHelper.DPI, PixelFormats.Bgra32, null, DecompressedData, Width * 4);
			}
		}

		/// <summary>
		/// Verifies this <see cref="BlamCharacter"/> against the given <see cref="FileFormat"/> and translates the results to a readable format.
		/// </summary>
		/// <returns>Any found errors, or an empty string.</returns>
		public string Verify(FileFormat format)
		{
			using (StringWriter sw = new StringWriter())
			{
				string linebase = UnicIndex.ToString("X4") + ": ";

				if (format.HasFlag(FileFormat.Package))
				{
					int onechar = (format.HasFlag(FileFormat.x64Char) ? 0x10 : 0xC);

					int chunksize = 0x8000;
					if (format.HasFlag(FileFormat.ChunkC))
						chunksize = 0xC000;
					else if (format.HasFlag(FileFormat.Chunk10))
						chunksize = 0x10000;

					if (CompressedSize > (chunksize - 8 - onechar - 4))
						sw.WriteLine(linebase + "Compressed size " + CompressedSize + " is greater than than the package chunk size, " + chunksize.ToString() + ".");
				}
				else if (format.HasFlag(FileFormat.Table) && CompressedSize > ushort.MaxValue)
					sw.WriteLine(linebase + "Compressed size " + CompressedSize + " is greater than " + ushort.MaxValue.ToString() + ".");
				

				int decompressedlimit = int.MaxValue;
				if (format.HasFlag(FileFormat.PixelLimit4k))
					decompressedlimit = 0x4000;
				if (format.HasFlag(FileFormat.PixelLimit20k))
					decompressedlimit = 0x20000;
				else if (format.HasFlag(FileFormat.PixelLimit100k))
					decompressedlimit = 0x100000;

				if (DecompressedSize > decompressedlimit)
					sw.WriteLine(linebase + "Decompressed size " + DecompressedSize + " is greater than " + decompressedlimit.ToString() + ".");


				if (format.HasFlag(FileFormat.ResLimit768x512))
				{
					if (Width > 768 || Height > 512)
						sw.WriteLine(linebase + "Dimensions " + Width + "x" + Height + " are greater than the maximum, 768x512.");
				}	
				else if (format.HasFlag(FileFormat.ResLimit256x56))
				{
					if (Width > 256 || Height > 56)
						sw.WriteLine(linebase + "Dimensions " + Width + "x" + Height + " are greater than the maximum, 256x56.");
				}	
				else if (format.HasFlag(FileFormat.Package) && (Width > 256 || Height > 64))
					sw.WriteLine(linebase + "Dimensions " + Width + "x" + Height + " are greater than the maximum, 256x64.");


				if (format.HasFlag(FileFormat.x64Char))
				{
					if (DisplayWidth > uint.MaxValue)
						sw.WriteLine(linebase + "Display Width " + DisplayWidth + "is greater than " + uint.MaxValue.ToString() + ".");
				}	
				else if (DisplayWidth > ushort.MaxValue)
					sw.WriteLine(linebase + "Display Width " + DisplayWidth + "is greater than " + ushort.MaxValue.ToString() + ".");

				return sw.ToString();
			}
		}


	}

}
