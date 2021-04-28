using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FontPackager.Classes;
using System;
using System.Runtime.InteropServices;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for FontCreator.xaml
	/// </summary>
	public partial class FontCreator : Window
	{
		public BlamFont Font { get; set; }

		BlamFont runtimefont;
		
		List<FontFamily> families = new List<FontFamily>();
		InstalledFontCollection pcfonts = new InstalledFontCollection();

		const string help =
			"For best results it is helpful to only consider the Ascend Heights when matching against an existing font instead of size.\r\n" +
			"If that fails, match the size and adjust the header's Ascend Height afterwards to match the original.\r\n" +
			"The font size box supports decimals, to maximize the size at a certain Ascend Height.\r\n\r\n" +
			"Characters should rest above the green line or very close to it. Adjust any major gaps using the offset value.\r\n\r\n" +
			"Bold is recommended for most cases.\r\n\r\n" +
			"Spaces and other whitespace will not display accurately in the preview, but should have a proper display width on creation.\r\n\r\n" +
			"Drag characters from open fonts for comparisons. These will disappear when a change is made.\r\n\r\n" +
			"For MCC don't forget to set the proper scale after import.\r\n\r\n" +
			"Using a larger character range than default can take longer to convert.\r\n\r\n" +
			"Also be gentle with this creation tool. It should be fine but it is previously known to crash if you spam changes. :)";

		List<BlamCharacter> previewcharacters = new List<BlamCharacter>();

		public FontCreator()
		{
			InitializeComponent();

			for (int i = 0; i < pcfonts.Families.Count(); i++)
			{
				if (pcfonts.Families[i].IsStyleAvailable(System.Drawing.FontStyle.Regular) ||
					pcfonts.Families[i].IsStyleAvailable(System.Drawing.FontStyle.Bold))
					families.Add(pcfonts.Families[i]);
			}

			pcfontlist.ItemsSource = families;

			pcfontlist.SelectedIndex = 0;
		}

		private void CreateFont(bool preview)
		{
			prevchars.ItemsSource = null;
			runtimefont = null;
			ascprev.Text = "null";
			descprev.Text = "null";


			bool parsed;

			parsed = float.TryParse(pcsize.Text, out float fontsize);
			if (!parsed || fontsize <= 0)
			{
				MessageBox.Show("Font Size could not be parsed or is invalid.");
				return;
			}

			parsed = short.TryParse(offsety.Text, out short offset);
			if (!parsed)
			{
				MessageBox.Show("Offset could not be parsed or is invalid.");
				return;
			}

			ushort rngStart = 0;
			ushort rngEnd = 0xFF;

			if (!preview)
			{
				parsed = ushort.TryParse(rangeStart.Text, System.Globalization.NumberStyles.HexNumber, null, out rngStart);
				if (!parsed)
				{
					MessageBox.Show("Range Start Character could not be parsed or is invalid.");
					return;
				}

				parsed = ushort.TryParse(rangeEnd.Text, System.Globalization.NumberStyles.HexNumber, null, out rngEnd);
				if (!parsed)
				{
					MessageBox.Show("Range End Character could not be parsed or is invalid.");
					return;
				}
			}

			FontFamily fam = (FontFamily)pcfontlist.SelectedItem;

			bool hasregular = fam.IsStyleAvailable(System.Drawing.FontStyle.Regular);
			bool hasbold = fam.IsStyleAvailable(System.Drawing.FontStyle.Bold);

			System.Drawing.FontStyle fontparams = System.Drawing.FontStyle.Bold;
			if (!hasbold || (hasregular && !(bool)pcbold.IsChecked))
				fontparams = System.Drawing.FontStyle.Regular;

			using (Font importfont = new Font(fam, fontsize, fontparams))
			{
				string name = (importfont.Name + "-" + importfont.Size.ToString()).Replace(" ", "");

				if (name.Length > 32)
					name = name.Substring(0, 32);

				runtimefont = new BlamFont(name);

				int em = fam.GetEmHeight(fontparams);
				var a = fam.GetCellAscent(fontparams);
				var d = fam.GetCellDescent(fontparams);
				var s = fam.GetLineSpacing(fontparams);

				float hpnt = (int)(importfont.Size * DPIHelper.DPI / 72);
				float hpix = hpnt / em;
				short asc = (short)(a * hpix);
				short desc = (short)(d * hpix);
				short lin = (short)(s * hpix);
				short lmod = (short)(lin - asc - desc);
				asc++;
				desc++;

				runtimefont.AscendHeight = (short)(asc + offset);
				runtimefont.DescendHeight = desc;
				runtimefont.LeadHeight = lmod;

				if (!preview)
				{
					runtimefont.KerningPairs = NativeMethods.GetKerningPairs(importfont);

					List<NativeMethods.FontRange> ranges = NativeMethods.GetUnicodeRangesForFont(importfont);
					foreach (NativeMethods.FontRange range in ranges)
					{
						for (ushort i = range.Low; i <= range.High; i++)
						{
							if (i >= rngStart && i <= rngEnd)
							{
								BlamCharacter bc = CreateCharacter(importfont, i, (short)(asc + offset));

								if (bc == null)
									continue;

								runtimefont.AddCharacter(bc);
							}
						}

					}
				}
				else
				{
					string previewstring = GetPreviewString();

					var chars = previewstring.ToList().Distinct().OrderBy(c => c).ToList();

					foreach(char c in chars)
					{
						BlamCharacter bc = CreateCharacter(importfont, c, (short)(asc + offset));

						if (bc == null)
							continue;

						runtimefont.AddCharacter(bc);
					}
				}

			}

			ascprev.Text = runtimefont.AscendHeight.ToString();
			descprev.Text = runtimefont.DescendHeight.ToString();

			UpdatePreview();
		}

		private static BlamCharacter CreateCharacter(Font font, ushort unic, short ascend)
		{
			using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
			{
				sf.FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;
				sf.Trimming = StringTrimming.None;

				Rectangle actualrect;
				Rectangle measuredrect;

				BlamCharacter bc;

				using (Bitmap dd = new Bitmap(font.Height * 4, font.Height))
				{
					using (Graphics g = Graphics.FromImage(dd))
					{
						g.PageUnit = GraphicsUnit.Pixel;
						g.TextRenderingHint = TextRenderingHint.AntiAlias;
						g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
						g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

						using (SolidBrush brush = new SolidBrush(Color.White))
							g.DrawString(((char)unic).ToString(), font, brush, new PointF(font.Height * 2, 0), sf);

						var fmeasure = g.MeasureString(((char)unic).ToString(), font, new PointF(font.Height * 2, 0), sf);
						measuredrect = new Rectangle(font.Height * 2, 0, (int)fmeasure.Width, (int)fmeasure.Height);
					}

					if (measuredrect.Width < 1) measuredrect.Width = 1;
					if (measuredrect.Height < 1) measuredrect.Height = 1;

					if (dd == null || dd.Width <= 1)
						return null;

					bc = CharacterTools.CreateCharacter(unic, dd, CharTint.None, true, out actualrect);
				}
				if (bc == null)
					return null;

				bc.OriginY = ascend;

				if (actualrect.Width >= 1)
				{
					bc.OriginX = (short)(actualrect.Left - measuredrect.Left);
					int mathWidth = actualrect.Width - (actualrect.Right - measuredrect.Right);

					if (mathWidth > 0)
						bc.DisplayWidth = (uint)mathWidth;
					else
						bc.DisplayWidth = (uint)measuredrect.Width;

				}
				else
					bc.DisplayWidth = (uint)measuredrect.Width;

				return bc;
			}
		}

		private void UpdatePreview()
		{
			if (runtimefont == null)
				return;

			string previewstring = GetPreviewString();

			prevchars.ItemsSource = null;
			previewcharacters.Clear();
			foreach (char c in previewstring)
			{
				int found = runtimefont.FindCharacter(c);

				if (found != -1)
					previewcharacters.Add(runtimefont.Characters[found]);
			}

			prevchars.ItemsSource = previewcharacters;
		}

		private string GetPreviewString()
		{
			string previewstring = "Font PackAger!";
			if (!string.IsNullOrEmpty(prevstring.Text) && !string.IsNullOrWhiteSpace(prevstring.Text))
				previewstring = prevstring.Text;
			return previewstring;
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			CreateFont(false);

			if (!string.IsNullOrEmpty(fname.Text))
			{
				string name = fname.Text;

				if (name.Length > 32)
					name = name.Substring(0, 32);

				runtimefont.Name = name;
			}
			Font = runtimefont;
			Close();
		}

		private void updatepreview_Click(object sender, RoutedEventArgs e)
		{
			CreateFont(true);
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void Help_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(help, "Import Help");
		}

		private void ResetRange_Click(object sender, RoutedEventArgs e)
		{
			rangeStart.Text = "0";
			rangeEnd.Text = "3FF";
		}

		private void prevchars_Drop(object sender, DragEventArgs e)
		{
			List<BlamCharacter> chars = (List<BlamCharacter>)(e.Data.GetData(typeof(List<BlamCharacter>)));

			if (chars == null)
				return;

			prevchars.ItemsSource = null;
			foreach (BlamCharacter bc in chars)
			{
				previewcharacters.Add(bc);
			}
			prevchars.ItemsSource = previewcharacters;

		}

		private void prevchars_PreviewDragOver(object sender, DragEventArgs e)
		{
			List<BlamCharacter> chars = (List<BlamCharacter>)(e.Data.GetData(typeof(List<BlamCharacter>)));

			if (chars == null)
			{
				e.Effects = DragDropEffects.None;
				e.Handled = true;
			}

		}

		private void window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			families.Clear();
			pcfonts.Dispose();
		}

		private static class NativeMethods
		{
			[StructLayout(LayoutKind.Sequential)]
			public struct KERNINGPAIR
			{
				public short wFirst;
				public short wSecond;
				public int iKernelAmount;
			}

			[DllImport("gdi32.dll")]
			static extern uint GetKerningPairs(IntPtr hdc, uint nNumPairs, [Out] KERNINGPAIR[] lpkrnpair);

			[DllImport("gdi32.dll")]
			static extern uint GetFontUnicodeRanges(IntPtr hdc, IntPtr lpgs);

			[DllImport("gdi32.dll")]
			static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

			[DllImport("gdi32.dll")]
			static extern IntPtr DeleteObject(IntPtr hObject);

			public static List<KerningPair> GetKerningPairs(Font font)
			{
				List<KerningPair> kp = new List<KerningPair>();

				using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
				{
					g.PageUnit = GraphicsUnit.Pixel;
					IntPtr hdc = g.GetHdc();
					IntPtr hf = font.ToHfont();
					IntPtr sel = SelectObject(hdc, hf);

					uint count = 255;
					KERNINGPAIR[] res = new KERNINGPAIR[count];
					uint ret = GetKerningPairs(hdc, count, res);

					sel = SelectObject(hdc, sel);

					g.ReleaseHdc(hdc);
					DeleteObject(hf);

					if (ret != 0)
					{
						for (int i = 0; i < ret; i++)
						{
							if (res[i].wFirst > 0xFF || res[i].wSecond > 0xFF)
								continue;
							kp.Add(new KerningPair((byte)res[i].wFirst, (byte)res[i].wSecond, (short)res[i].iKernelAmount));
						}
					}
				}

				return kp;
			}

			public struct FontRange
			{
				public ushort Low;
				public ushort High;
			}

			// https://stackoverflow.com/questions/103725/is-there-a-way-to-programmatically-determine-if-a-font-file-has-a-specific-unico
			public static List<FontRange> GetUnicodeRangesForFont(Font font)
			{
				List<FontRange> fontRanges = new List<FontRange>();

				using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
				{
					g.PageUnit = GraphicsUnit.Pixel;
					IntPtr hdc = g.GetHdc();
					IntPtr hf = font.ToHfont();
					IntPtr sel = SelectObject(hdc, hf);

					uint size = GetFontUnicodeRanges(hdc, IntPtr.Zero);
					IntPtr glyphSet = Marshal.AllocHGlobal((int)size);
					GetFontUnicodeRanges(hdc, glyphSet);
					
					int count = Marshal.ReadInt32(glyphSet, 12);
					for (int i = 0; i < count; i++)
					{
						FontRange range = new FontRange();
						range.Low = (ushort)Marshal.ReadInt16(glyphSet, 16 + i * 4);
						range.High = (ushort)(range.Low + Marshal.ReadInt16(glyphSet, 18 + i * 4) - 1);
						fontRanges.Add(range);
					}
					SelectObject(hdc, sel);
					Marshal.FreeHGlobal(glyphSet);
					g.ReleaseHdc(hdc);
					DeleteObject(hf);
				}

				return fontRanges;
			}

		}

	}
}
