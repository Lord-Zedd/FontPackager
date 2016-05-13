using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Drawing;
using System.Threading;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			logbox.Text = "Font Packager v1 by Lord Zedd. Open a font package!\r\nDon't mod online you fucking shitheads.";
		}
		FontPackage package;

		#region top controls
		private void btnOpenPkg_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Package";
			ofd.Filter = "font_package (*.bin)|*.bin";
			if ((bool)ofd.ShowDialog())
			{
				package = new FontPackage();

				switch (package.Load(ofd.FileName))
				{
					case 0:
						break;
					case 1:
						WriteLog("File \"" + ofd.SafeFileName + "\" has an invalid header version value and was not loaded.");
						return;
					case 2:
						WriteLog("File \"" + ofd.SafeFileName + "\" has a font count of 0 and was not loaded.");
						return;
				}

				fontslist.Items.Clear();

				inputpkgpath.Text = ofd.FileName;
				btnSavepkg.IsEnabled = true;
				btnAddBat.IsEnabled = true;
				btndeleteBat.IsEnabled = true;

				bool bigfont = false;

				for (int i = 0; i < package.Fonts.Count; i++)
				{
					fontslist.Items.Add(i.ToString() + ": " + package.Fonts[i].Name + "  [" + package.Fonts[i].Characters.Count + "]");

					if (package.Fonts[i].Characters.Count > 5000)
						bigfont = true;
				}

				fontslist.SelectedIndex = 0;

				WriteLog("Font Package \"" + ofd.SafeFileName + "\" has been loaded successfully with " + fontslist.Items.Count.ToString() + " fonts.");

				if (bigfont)
					WriteLog("NOTE: Package contains one or more fonts with over 5000 characters, performance may suffer.");

			}
		}

		private void btnSavepkg_Click(object sender, RoutedEventArgs e)
		{
			package.Rebuild(inputpkgpath.Text);
			WriteLog("Font Package \"" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" has been saved successfully.");
		}
		#endregion

		#region functions
		public void UpdateFontDisplay()
		{
			if (fontslist.SelectedIndex == -1)
				return ;

			lstChars.Items.Clear();

			for (int i = 0; i < package.Fonts[fontslist.SelectedIndex].Characters.Count; i++)
			{
				ListBoxItem fontChar = new ListBoxItem();

				fontChar.Content = new System.Windows.Controls.Image()
				{
					Source = package.Fonts[fontslist.SelectedIndex].Characters[i].Data.getImage(),
					Stretch = Stretch.None
				};

				var unicode = package.Fonts[fontslist.SelectedIndex].Characters[i].CharCode;
				var utf8 = Encoding.UTF8.GetBytes(Convert.ToChar(unicode).ToString());

				string utf8Code = "";
				for (int u = 0; u < utf8.Length; u++)
				{
					if (u == 0)
						utf8Code += utf8[u].ToString("X2");
					else
						utf8Code += " " + utf8[u].ToString("X2");
				}


				fontChar.ToolTip = "Unicode: " + unicode +
					"\r\nUTF8: " + utf8Code +
					"\r\nDouble click to copy as a unicode character to the clipboard." +
					"\r\n\r\nWidth: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.width +
					"\r\nDisplay Width: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.dispWidth +
					"\r\nHeight: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.height +
					"\r\nDisplay Height: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.dispHeight +
					"\r\nUnknown(ftype): " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.fType;
				fontChar.Height = package.Fonts[fontslist.SelectedIndex].Characters[i].Data.height;
				fontChar.Padding = new Thickness(0);
				fontChar.VerticalAlignment = System.Windows.VerticalAlignment.Center;
				fontChar.Margin = new Thickness(1);
				fontChar.Tag = unicode;
				fontChar.MouseDoubleClick += CopyUnicode;

				lstChars.Items.Add(fontChar);
			}

			fontHeight.Text = package.FontHeaders[fontslist.SelectedIndex].lineHeight.ToString();
			fontTPad.Text = package.FontHeaders[fontslist.SelectedIndex].lineTopPad.ToString();
			fontBPad.Text = package.FontHeaders[fontslist.SelectedIndex].lineBotPad.ToString();
			fontUnk.Text = package.FontHeaders[fontslist.SelectedIndex].lineIndent.ToString();
		}

		private string OpenImage()
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Select Image";
			ofd.Filter = "Image Files(*.bmp; *.jpg; *.gif; *.png)| *.bmp; *.jpg; *.gif; *.png; | All files(*.*) | *; ";
			if ((bool)ofd.ShowDialog())
				return ofd.FileName;
			else
				return null;
		}

		private UInt16 ParseChar(string command, string input)
		{
			if (input.Length == 0)
			{
				WriteLog(command + "failed: Invalid unicode entered (Box is empty)");
				return 0xFFFF;
			}

			UInt16 char2add;

			try
			{
				char2add = UInt16.Parse(input, System.Globalization.NumberStyles.HexNumber);

				if (char2add == 0xFFFF)
					WriteLog(command + "failed: Character 0xFFFF is invalid, use 0xFFFE.");

				return char2add;
			}
			catch
			{
				WriteLog(command + "failed: Invalid unicode entered (Could not parse hex)");
				return 0xFFFF;
			}
		}

		private void WriteLog(string input)
		{
			logbox.AppendText("\r\n" + input);
			logbox.ScrollToEnd();
		}

		public void UpdateFontInfo(short height, short tpad, short bpad, short unk)
		{
			if (fontslist.SelectedIndex == -1)
				return;

			package.FontHeaders[fontslist.SelectedIndex].lineHeight = height;
			package.FontHeaders[fontslist.SelectedIndex].lineTopPad = tpad;
			package.FontHeaders[fontslist.SelectedIndex].lineBotPad = bpad;
			package.FontHeaders[fontslist.SelectedIndex].lineIndent = unk;
		}

		private short ParseHeaderShort(string valuename, string input)
		{
			short output = -1;

			try
			{
				output = short.Parse(fontHeight.Text);
			}
			catch
			{
				WriteLog("Info update failed: Could not parse " + valuename + ".");
				return -1;
			}

			if (output < 0 || output > 64)
			{
				WriteLog("Info update failed: " + valuename + " " + output + " out of range (0-64).");
				return -1;
			}
			return output;
		}

		#endregion

		#region font tab controls

		private void btnFontUpdate_Click(object sender, RoutedEventArgs e)
		{
			if (fontslist.SelectedIndex == -1)
				return;

			short height = ParseHeaderShort("height", fontHeight.Text);
			short tpad = ParseHeaderShort("top padding", fontTPad.Text);
			short bpad = ParseHeaderShort("bottom padding", fontBPad.Text);
			short unk = ParseHeaderShort("indent", fontUnk.Text);

			if (height == -1 || tpad == -1 || bpad == -1 || unk == -1)
				return;

			UpdateFontInfo(height, tpad, bpad, unk);
		}

		#region font character tab controls
		private void btnReplace_Click(object sender, RoutedEventArgs e)
		{
			if (lstChars.SelectedIndex == -1)
				return;

			var path = OpenImage();
			if (path != null)
			{
				System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);

				UInt16 char2replace = package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].CharCode;

				package.AddCustomCharacter(char2replace, fontslist.SelectedIndex, newpic, tintCheck.IsChecked.Value);

				newpic.Dispose();

				UpdateFontDisplay();

				WriteLog("Character " + char2replace + " was successfully replaced with " + System.IO.Path.GetFileName(path) + ".");
			}

		}

		private void btnExtract_Click(object sender, RoutedEventArgs e)
		{
			if (lstChars.SelectedIndex == -1)
				return;

			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Font Character";
			sfd.Filter = "PNG Image (*.png)|*.png;|Raw Data (Debug) (*.bin)|*.bin;";
			if ((bool)sfd.ShowDialog())
			{
				switch (sfd.FilterIndex)
				{
					case 1:
						FileStream file = new FileStream(sfd.FileName, FileMode.Create, System.IO.FileAccess.Write);
						BitmapEncoder encoder = new PngBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.getImage()));
						encoder.Save(file);
						file.Close();
						lstChars.UnselectAll();

						WriteLog("Character successfully extracted to " + sfd.SafeFileName + ".");
						break;
					case 2:
						FileStream filex = new FileStream(sfd.FileName, FileMode.Create, System.IO.FileAccess.Write);
						filex.Write(package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.compressedData, 0, package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.dataSize);
						filex.Close();
						lstChars.UnselectAll();

						WriteLog("Raw compressed character successfully extracted to " + sfd.SafeFileName + ".");
						break;
				}
			}
		}

		private void btnDelete_Click(object sender, RoutedEventArgs e)
		{
			if (lstChars.SelectedIndex != -1)
				if (MessageBox.Show("You sure you wanna do this?", "Delete Character", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
				{
					ushort oldchar = package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].CharCode;
					package.Fonts[fontslist.SelectedIndex].Characters.RemoveAt(lstChars.SelectedIndex);
					UpdateFontDisplay();

					WriteLog("Character " + oldchar.ToString("X4") + " was successfully removed!");
				}
		}

		private void btnAdd_Click(object sender, RoutedEventArgs e)
		{
			UInt16 char2add = ParseChar("Add ", newChar.Text);
			if (char2add == 0xFFFF)
				return;

			var path = OpenImage();
			if (path != null)
			{
				System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);
				package.AddCustomCharacter(char2add, fontslist.SelectedIndex, newpic, tintCheck.IsChecked.Value);

				newpic.Dispose();
				UpdateFontDisplay();

				WriteLog("Character " + char2add + " was successfully added from " + System.IO.Path.GetFileName(path) + ".");
				newChar.Text = "";
			}
		}

		private void btnCharUpdate_Click(object sender, RoutedEventArgs e)
		{
			if (fontslist.SelectedIndex == -1)
				return;
			if (lstChars.SelectedIndex == -1)
				return;

			ushort width = 0;
			ushort height = 0;
			short unk = 0;

			try
			{
				width = ushort.Parse(charWidth.Text);
			}
			catch
			{
				WriteLog("Character update failed: Could not parse display width.");
				return;
			}

			try
			{
				height = ushort.Parse(charHeight.Text);
			}
			catch
			{
				WriteLog("Character update failed: Could not parse display height.");
				return;
			}

			try
			{
				unk = short.Parse(charUnk.Text);
			}
			catch
			{
				WriteLog("Character update failed: Could not parse unknown value.");
				return;
			}

			package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.dispWidth = width;
			package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.dispHeight = height;
			package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.fType = unk;

			WriteLog("Character update successful.");
			UpdateFontDisplay();
		}
		#endregion

		#region font font tab controls
		private void btnABC_Click(object sender, RoutedEventArgs e)
		{
			abcFile abc;

			string abcname = "";

			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open ABC File";
			ofd.Filter = "FontMaker ABC File (*.abc)|*.abc";
			if ((bool)ofd.ShowDialog())
			{
				FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
				BigEndianReader br = new BigEndianReader(fs);

				br.BaseStream.Position = 0;

				abc = new abcFile();

				abc.Read(br);

				br.Close();
				fs.Close();

				abcname = ofd.SafeFileName;
			}
			else
				return;

			if (abc.Height < 0 || abc.Height > 64)
			{
				WriteLog("ABC import failed: Height " + abc.Height + " out of range (0-64).");
				return;
			}
			if (abc.TopPadding < 0 || abc.TopPadding > 64)
			{
				WriteLog("ABC import failed: Top padding " + abc.TopPadding + " out of range (0-64).");
				return;
			}
			if (abc.BottomPadding < 0 || abc.BottomPadding > 64)
			{
				WriteLog("ABC import failed: Bottom padding " + abc.BottomPadding + " out of range (0-64).");
				return;
			}
			if (abc.YAdvance < 0 || abc.YAdvance > 64)
			{
				WriteLog("ABC import failed: Indent " + abc.YAdvance + " out of range (0-64).");
				return;
			}

			ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Sheet";
			ofd.Filter = "Converted FontMaker Image (*.png)|*.png";
			if ((bool)ofd.ShowDialog())
			{
				System.Drawing.Image sheet = System.Drawing.Image.FromFile(ofd.FileName);

				for (int i = 0; i < abc.GlyphCount; i++)
				{

					System.Drawing.Rectangle rect = new System.Drawing.Rectangle(abc.GlyphTable[i].Left,
						abc.GlyphTable[i].Top,
						(abc.GlyphTable[i].Right - abc.GlyphTable[i].Left),
						(abc.GlyphTable[i].Bottom - abc.GlyphTable[i].Top));

					if (rect.Left + rect.Width > sheet.Width)
						rect.Width = sheet.Width - rect.Left;

					if (rect.Top + rect.Height > sheet.Height)
						rect.Height = sheet.Height - rect.Top;

					//today i learned about empty characters
					if (rect.Width == 0)
						rect.Width = 1;
					if (rect.Height == 0)
						rect.Height = 1;

					Bitmap bm = new Bitmap(sheet);
					System.Drawing.Imaging.BitmapData bd = bm.LockBits(rect,
					System.Drawing.Imaging.ImageLockMode.WriteOnly,
					bm.PixelFormat);

					var sauce = BitmapSource.Create(bd.Width, bd.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bd.Scan0, bd.Stride * bd.Height, bd.Stride);

					MemoryStream ms = new MemoryStream();
					var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
					encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(sauce));
					encoder.Save(ms);
					ms.Flush();

					var dispwidth = abc.GlyphTable[i].Width;

					if (abc.CharCodes[i] == 0x20)
					{
						var periodindex = package.Fonts[fontslist.SelectedIndex].FindCharacter(0x2C);

						if (periodindex != -1)
							dispwidth = package.Fonts[fontslist.SelectedIndex].Characters[periodindex].Data.dispWidth;
						else
							dispwidth = 4;
					}
						

					package.AddCustomCharacter(abc.CharCodes[i], fontslist.SelectedIndex, System.Drawing.Image.FromStream(ms), false, dispwidth);

					ms.Close();
					bm.Dispose();
				}

				//for (int i = 0; i < package.Fonts[fontslist.SelectedIndex].Characters.Count; i++)
				//{
				//	package.Fonts[fontslist.SelectedIndex].Characters[i].Data.dispHeight = (ushort)abc.YAdvance;
				//}
				//
				//UpdateFontInfo((short)abc.Height, (short)abc.TopPadding, (short)abc.BottomPadding, 0);
				UpdateFontDisplay();

				WriteLog("Characters successfully imported from abc file \"" + abcname + "\".");
			}
		}

		private void HOCopy(object sender, RoutedEventArgs e)
		{
			ushort startchar = ParseChar("Copy" , HOstart.Text);
			if (startchar == 0xFFFF)
				return;

			ushort endchar = ParseChar("Copy", HOend.Text);
			if (endchar == 0xFFFF)
				return;

			ushort fontindex = ParseChar("Copy", HOfont.Text);
			if (fontindex == 0xFFFF)
				return;

			if (startchar > endchar)
			{
				WriteLog("Copy failed: Invalid character range.");
				return;
			}

			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Package";
			ofd.Filter = "font_package (*.bin)|*.bin";
			if ((bool)ofd.ShowDialog())
			{
				FontPackage package2 = new FontPackage();

				package2.Load(ofd.FileName);

				if (fontindex > package2.Fonts.Count)
				{
					WriteLog("Copy failed: Invalid font index for second package.");
					package2 = new FontPackage();
					return;
				}

				for (int i = 0; i < package2.Fonts[fontindex].Characters.Count; i++)
					if (package2.Fonts[fontindex].Characters[i].CharCode >= startchar && package2.Fonts[fontindex].Characters[i].CharCode <= endchar)
					{
						var existingchar = package.Fonts[fontslist.SelectedIndex].FindCharacter(package2.Fonts[fontindex].Characters[i].CharCode);

						if (existingchar != -1)
							package.Fonts[fontslist.SelectedIndex].Characters[existingchar].Data = package2.Fonts[fontindex].Characters[i].Data;
						else
							package.Fonts[fontslist.SelectedIndex].Characters.Add(package2.Fonts[fontindex].Characters[i]);
					}

				package.Fonts[fontslist.SelectedIndex].SortCharacters();

				if ((bool)HOinfo.IsChecked)
					UpdateFontInfo(package2.FontHeaders[fontindex].lineHeight, package2.FontHeaders[fontindex].lineTopPad, package2.FontHeaders[fontindex].lineBotPad, package2.FontHeaders[fontindex].lineIndent);

				UpdateFontDisplay();

				WriteLog("Characters successfully copied from font \"" + package2.FontHeaders[fontindex].Name() + "\" in package \"" + ofd.SafeFileName + "\".");
				package2 = new FontPackage();
			}

		}

		private void btnHFix_Click(object sender, RoutedEventArgs e)
		{
			if (fontslist.SelectedIndex == -1)
				return;

			short height = 0;

			try
			{
				height = short.Parse(fontHeight.Text);
			}
			catch
			{
				WriteLog("Height update failed: Could not parse height.");
				return;
			}

			for (int i = 0; i < package.Fonts[fontslist.SelectedIndex].Characters.Count; i++)
				package.Fonts[fontslist.SelectedIndex].Characters[i].Data.dispHeight = (ushort)height;

			UpdateFontDisplay();
		}
		#endregion
		#endregion

		#region batch tab controls
		private void btnAddBat_Click(object sender, RoutedEventArgs e)
		{
			UInt16 char2add = ParseChar("Batch add ", newCharBat.Text);
			if (char2add == 0xFFFF)
				return;

			var path = OpenImage();
			if (path != null)
			{
				System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);

				for (int i = 0; i < package.Fonts.Count; i++)
					package.AddCustomCharacter(char2add, i, newpic, tintCheck.IsChecked.Value);

				newpic.Dispose();
				UpdateFontDisplay();

				WriteLog("Character " + char2add + " was successfully added from " + System.IO.Path.GetFileName(path) + " to every font.");
				newCharBat.Text = "";
				tabz.SelectedIndex = 0;
			}
		}

		private void btndeleteBat_Click(object sender, RoutedEventArgs e)
		{
			ushort char2delete = ParseChar("Batch remove ", deleteBat.Text);
			if (char2delete == 0xFFFF)
				return;

			if (MessageBox.Show("You sure you wanna do this? This is worse than the other one.", "Batch Delete Character", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
			{
				for (int i = 0; i < package.Fonts.Count; i++)
				{
					var index = package.Fonts[i].FindCharacter(char2delete);

					if (index != -1)
						package.Fonts[i].Characters.RemoveAt(index);
				}

				UpdateFontDisplay();

				WriteLog("Character " + char2delete.ToString("X4") + " was successfully removed from any applicable fonts!");

				deleteBat.Text = "";

				tabz.SelectedIndex = 0;
			}
		}

		#endregion

		#region control events
		private void fontslist_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (fontslist.SelectedIndex != -1)
			{				
				btnAdd.IsEnabled = true;
				btnABC.IsEnabled = true;
				btnFontUpdate.IsEnabled = true;
				HObtn.IsEnabled = true;
				btnHFix.IsEnabled = true;

				UpdateFontDisplay();
			}
			else
			{
				btnAdd.IsEnabled = false;
				btnABC.IsEnabled = false;
				btnFontUpdate.IsEnabled = false;
				HObtn.IsEnabled = false;
				btnHFix.IsEnabled = false;
				lstChars.Items.Clear();

				fontHeight.Text = "";
				fontTPad.Text = "";
				fontBPad.Text = "";
				fontUnk.Text = "";
			}
				
		}

		private void lstChars_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (lstChars.SelectedIndex != -1)
			{
				btnReplace.IsEnabled = true;
				btnExtract.IsEnabled = true;
				btnDelete.IsEnabled = true;
				btnCharUpdate.IsEnabled = true;

				charWidth.Text = package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.dispWidth.ToString();
				charHeight.Text = package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.dispHeight.ToString();
				charUnk.Text = package.Fonts[fontslist.SelectedIndex].Characters[lstChars.SelectedIndex].Data.fType.ToString();
			}
			else
			{
				btnReplace.IsEnabled = false;
				btnExtract.IsEnabled = false;
				btnDelete.IsEnabled = false;
				btnCharUpdate.IsEnabled = false;

				charWidth.Text = "";
				charHeight.Text = "";
				charUnk.Text = "";
			}
		}

		private void lstChars_MouseDown(object sender, MouseButtonEventArgs e)
		{
			lstChars.UnselectAll();
		}

		private void CopyUnicode(object sender, MouseButtonEventArgs e)
		{
			ListBoxItem item = sender as ListBoxItem;

			System.Windows.Clipboard.SetText(Convert.ToChar(item.Tag).ToString());
		}

		private void cbInvertBG_Checked(object sender, RoutedEventArgs e)
		{
			lstChars.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xEE, 0xEE, 0xEE));
		}

		private void cbInvertBG_Unchecked(object sender, RoutedEventArgs e)
		{
			lstChars.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x11, 0x11, 0x11));
		}

		#endregion
	}
}

