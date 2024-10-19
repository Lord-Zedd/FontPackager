using System;
using System.Windows.Media.Imaging;
using System.Text;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Collections.Generic;

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
		/// Verifies this <see cref="BlamCharacter"/> against the given <see cref="FormatInformation"/>.
		/// </summary>
		/// <returns>Any found errors.</returns>
		public List<VerificationResult> Verify(FormatInformation info)
		{
			List<VerificationResult> results = new List<VerificationResult>();

			string prefix = $"{UnicIndex:X4}: ";

			if (info.Format == FileFormat.Package && CompressedSize > (info.ChunkSizeValue - 8 - info.PackageCharacterInfoLength - 4))
				results.Add(new VerificationResult($"{prefix} Compressed size {CompressedSize:X} is greater than the package chunk size {info.ChunkSizeValue:X} for the selected format.", true));
			else if (info.Format == FileFormat.Table && CompressedSize > ushort.MaxValue)
				results.Add(new VerificationResult($"{prefix} Compressed size {CompressedSize:X} is greater than the max value of {ushort.MaxValue:X}.", true));

			if (DecompressedSize > info.PixelLimitValue)
				results.Add(new VerificationResult($"{prefix} Decompressed size {DecompressedSize:X} is greater than the known engine limit of {info.PixelLimitValue:X}.", true));

			if (info.ResolutionLimit != ResolutionLimit.None &&
				(Width > info.ResolutionLimitWidth ||
				Height > info.ResolutionLimitHeight))
				results.Add(new VerificationResult($"{prefix} Dimensions {Width}x{Height} are greater than the visible maximum {info.ResolutionLimitWidth}x{info.ResolutionLimitHeight}. This can be ignored but may not display ingame.", false));

			if (DisplayWidth > info.MaximumDisplayWidth)
				results.Add(new VerificationResult($"{prefix} Display Width {DisplayWidth} is greater than the max value of {info.MaximumDisplayWidth}.", true));

			return results;
		}

		/// <summary>
		/// Clones this <see cref="BlamCharacter"/> to a new instance, with copies of <see cref="CompressedData"/> and <see cref="DecompressedData"/>
		/// </summary>
		/// <returns>A fresh clone of the <see cref="BlamCharacter"/> which this method was called from.</returns>
		public BlamCharacter Clone()
		{
			BlamCharacter result = new BlamCharacter(UnicIndex);
			result.DisplayWidth = DisplayWidth;
			result.Width = Width;
			result.Height = Height;
			result.OriginX = OriginX;
			result.OriginY = OriginY;

			if (CompressedData != null)
			{
				result.CompressedData = new byte[CompressedData.Length];
				Array.Copy(CompressedData, result.CompressedData, CompressedData.Length);
			}
			
			if (DecompressedData != null)
			{
				result.DecompressedData = new byte[DecompressedData.Length];
				Array.Copy(DecompressedData, result.DecompressedData, DecompressedData.Length);
			}

			return result;
		}
	}

}
