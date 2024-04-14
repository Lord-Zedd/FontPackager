using FontPackager.Classes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for FontPrinter.xaml
	/// </summary>
	public partial class FontPrinter : Window
	{
		public BlamFont Font { get; set; }

		readonly ColorMatrix NormalMatrix = new ColorMatrix(new float[][]
		{
			new float[] {1, 0, 0, 0, 0},
			new float[] {0, 1, 0, 0, 0},
			new float[] {0, 0, 1, 0, 0},
			new float[] {0, 0, 0, 1, 0},
			new float[] {0, 0, 0, 0, 1}
		});

		readonly ColorMatrix ShadowMatrix = new ColorMatrix(new float[][]
{
			new float[] {0, 0, 0, 0, 0},
			new float[] {0, 0, 0, 0, 0},
			new float[] {0, 0, 0, 0, 0},
			new float[] {0, 0, 0, 1, 0},
			new float[] {0, 0, 0, 0, 1}
});

		public FontPrinter(BlamFont font)
		{
			Font = font;

			InitializeComponent();

			Title = "Font Packager Printer - " + Font.Name;
		}

		private void ButtonGenerate_Click(object sender, RoutedEventArgs e)
		{
			if (!int.TryParse(txtWidth.Text, out int width))
				return;

			if (!int.TryParse(txtHeight.Text, out int height))
				return;

			if (width > 2000 || height > 2000)
				return;

			Color foreground = Color.Transparent;
			Color background = Color.Transparent;

			if ((bool)chkForeCol.IsChecked && txtForeCol.Text.Length >= 6)
			{
				string foreString = txtForeCol.Text;
				if (foreString.StartsWith("#"))
					foreString = foreString.Substring(1);

				if (!byte.TryParse(foreString.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte foreRed))
					return;
				if (!byte.TryParse(foreString.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte foreGreen))
					return;
				if (!byte.TryParse(foreString.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte foreBlue))
					return;

				foreground = Color.FromArgb(0xFF, foreRed, foreGreen, foreBlue);
			}

			if ((bool)chkBackCol.IsChecked && txtBackCol.Text.Length >= 6)
			{
				string backString = txtBackCol.Text;
				if (backString.StartsWith("#"))
					backString = backString.Substring(1);

				if (!byte.TryParse(backString.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte backRed))
					return;
				if (!byte.TryParse(backString.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte backGreen))
					return;
				if (!byte.TryParse(backString.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte backBlue))
					return;

				background = Color.FromArgb(0xFF, backRed, backGreen, backBlue);
			}

			bool kern = (bool)chkKern.IsChecked;
			bool wrap = (bool)chkWrap.IsChecked;
			bool drop = (bool)chkShadow.IsChecked;
			bool crop = (bool)chkCrop.IsChecked;

			string input = txtInput.Text;
			int lineheight = Font.AscendHeight + Font.DescendHeight + Font.LeadHeight;

			Dictionary<char, BlamCharacter> charCache = new Dictionary<char, BlamCharacter>();
			Dictionary<char, Bitmap> charBitmapCache = new Dictionary<char, Bitmap>();

			var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

			using (Graphics g = Graphics.FromImage(result))
			{
				g.CompositingMode = CompositingMode.SourceOver;
				g.CompositingQuality = CompositingQuality.HighQuality;
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = PixelOffsetMode.None;
				g.Clear(background);

				using (ImageAttributes attr = new ImageAttributes())
				{
					char prev = (char)0xFFFF;

					int runwidth = 0;
					int runheight = 0;

					foreach (char c in input)
					{
						if (c == '\r')
							continue;

						if (c == '\n')
						{
							runwidth = 0;
							runheight += lineheight;
							prev = (char)0xFFFF;
							continue;
						}

						if (c == '\t')
						{
							runwidth += 32;
							continue;
						}

						char runtime = c;
						BlamCharacter bc;
						if (!charCache.ContainsKey(runtime))
						{
							int index = Font.FindCharacter(runtime);
							if (index == -1)
							{
								index = Font.FindCharacter('\0');
								runtime = '\0';
							}

							BlamCharacter orig = Font.Characters[index];

							if (orig.DecompressedData == null)
								CharacterTools.DecompressData(orig);

							if (foreground != Color.Transparent)
							{
								//we shall tint the pixels like the game, even though it is rather destructive
								BlamCharacter tint = orig.Clone();

								tint.CompressedData = null;
								for (int p = 0; p < tint.DecompressedData.Length; p += 4)
								{
									byte pb = tint.DecompressedData[p];
									byte pg = tint.DecompressedData[p + 1];
									byte pr = tint.DecompressedData[p + 2];

									if (pb == pg && pg == pr)
									{
										tint.DecompressedData[p] = foreground.B;
										tint.DecompressedData[p + 1] = foreground.G;
										tint.DecompressedData[p + 2] = foreground.R;
									}
								}
								charCache[runtime] = tint;
							}
							else
								charCache[runtime] = orig;
						}

						bc = charCache[runtime];

						Bitmap bcb;
						if (!charBitmapCache.ContainsKey(runtime))
						{
							Bitmap conv = PixelsToBitmap(bc.DecompressedData, bc.Width, bc.Height);
							charBitmapCache[runtime] = conv;
						}

						bcb = charBitmapCache[runtime];
						bcb.MakeTransparent();

						if (kern && prev != 0xFFFF &&
							Font.TryFindKerningValue(prev, runtime, out int kernpad))
						{
							//don't apply kerning between upper and lower cases? games appear to do this
							if (!(prev >= 0x41 && prev <= 0x5A &&
								runtime >= 0x61 && runtime <= 0x7A))
								runwidth += kernpad;
						}

						runwidth += bc.OriginX;

						if (wrap && runwidth + (int)bc.DisplayWidth > width)
						{
							runwidth = 0;
							runheight += lineheight;
						}

						if (drop)
						{
							attr.SetColorMatrix(ShadowMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
							var droprect = new Rectangle(runwidth + 1, runheight + 1, (int)bc.DisplayWidth, bcb.Height);
							g.DrawImage(bcb, droprect, 0, 0, (int)bc.DisplayWidth, bcb.Height, GraphicsUnit.Pixel, attr);
							attr.SetColorMatrix(NormalMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
						}

						var rect = new Rectangle(runwidth, runheight, (int)bc.DisplayWidth, bcb.Height);
						g.DrawImage(bcb, rect, 0, 0, (int)bc.DisplayWidth, bcb.Height, GraphicsUnit.Pixel, attr);

						runwidth += (int)bc.DisplayWidth;
						prev = runtime;
					}
				}

				
			}

			if (crop)
			{
				var cropped = CropBitmapToBounds(result);
				result.Dispose();
				result = cropped;
			}

			outputImg.Source = ConvertBitmap(result);
			result.Dispose();
		}

		private Bitmap CropBitmapToBounds(Bitmap image)
		{
			var bounds = CharacterTools.GetImageBounds(image);
			var destination = new Rectangle(0, 0, bounds.Width, bounds.Height);

			Bitmap cropped = new Bitmap(bounds.Width, bounds.Height);

			using (Graphics g = Graphics.FromImage(cropped))
			{
				g.CompositingMode = CompositingMode.SourceOver;
				g.CompositingQuality = CompositingQuality.HighQuality;
				g.InterpolationMode = InterpolationMode.NearestNeighbor;
				g.PixelOffsetMode = PixelOffsetMode.None;

				g.DrawImage(image, destination, bounds, GraphicsUnit.Pixel);
			}

			return cropped;
		}

		private void ButtonSave_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Image";
			sfd.Filter = "PNG Image (*.png)|*.png;";
			if ((bool)sfd.ShowDialog())
			{
				switch (sfd.FilterIndex)
				{
					case 1:
						using (var fileStream = new FileStream(sfd.FileName, FileMode.Create))
						{
							BitmapEncoder encoder = new PngBitmapEncoder();
							encoder.Frames.Add(BitmapFrame.Create((BitmapSource)outputImg.Source));
							encoder.Save(fileStream);
						}
						break;
				}
			}
		}

		private Bitmap PixelsToBitmap(byte[] buffer, int width, int height)
		{
			Bitmap b = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			Rectangle BoundsRect = new Rectangle(0, 0, width, height);
			BitmapData bmpData = b.LockBits(BoundsRect, ImageLockMode.WriteOnly, b.PixelFormat);
			IntPtr ptr = bmpData.Scan0;

			Marshal.Copy(buffer, 0, ptr, buffer.Length);
			b.UnlockBits(bmpData);

			return b;
		}

		private static BitmapSource ConvertBitmap(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(
				new Rectangle(0, 0, bitmap.Width, bitmap.Height),
				ImageLockMode.ReadOnly, bitmap.PixelFormat);

			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height,
				bitmap.HorizontalResolution, bitmap.VerticalResolution,
				System.Windows.Media.PixelFormats.Bgra32, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);

			return bitmapSource;
		}
	}
}
