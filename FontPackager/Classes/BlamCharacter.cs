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
			set { _dwidth = value; NotifyPropertyChanged("DisplayWidth"); }
		}

		public ushort Width { get; set; }
		public ushort Height { get; set; }

		public byte[] CompressedData { get; set; }
		public byte[] DecompressedData { get; set; }

		short _originx;
		public short OriginX
		{
			get { return _originx; }
			set { _originx = value; NotifyPropertyChanged("OriginX"); }
		}

		short _originy;
		public short OriginY
		{
			get { return _originy; }
			set { _originy = value; NotifyPropertyChanged("OriginY"); }
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
		/// Verifies this <see cref="BlamCharacter"/> against the given <see cref="FormatInformation"/> and translates the results to a readable format.
		/// </summary>
		/// <returns>Any found errors, or an empty string.</returns>
		public string Verify(FormatInformation info)
		{
			using (StringWriter sw = new StringWriter())
			{
				string linebase = UnicIndex.ToString("X4") + ": ";

				if (info.Format == FileFormat.Package)
				{
					if (CompressedSize > (info.ChunkSizeValue - 8 - info.PackageCharacterInfoLength - 4))
						sw.WriteLine(linebase + "Compressed size " + CompressedSize + " is greater than than the package chunk size, " + info.ChunkSizeValue.ToString() + ".");
				}
				else if (info.Format == FileFormat.Table && CompressedSize > ushort.MaxValue)
					sw.WriteLine(linebase + "Compressed size " + CompressedSize + " is greater than " + ushort.MaxValue.ToString() + ".");
				
				if (DecompressedSize > info.PixelLimitValue)
					sw.WriteLine(linebase + "Decompressed size " + DecompressedSize + " is greater than " + info.PixelLimitValue.ToString() + ".");

				if (info.ResolutionLimit != ResolutionLimit.None &&
					(Width > info.ResolutionLimitWidth ||
					Height > info.ResolutionLimitHeight))
				{
					sw.WriteLine(linebase + "Dimensions " + Width + "x" + Height + " are greater than the maximum, " + info.ResolutionLimitWidth + "x" + info.ResolutionLimitHeight + ".");
				}

				if (DisplayWidth > info.MaximumDisplayWidth)
					sw.WriteLine(linebase + "Display Width " + DisplayWidth + "is greater than the maximum" + info.MaximumDisplayWidth.ToString() + ".");

				return sw.ToString();
			}
		}
	}

}
