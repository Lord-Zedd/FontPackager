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
using System.Drawing.Imaging;

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

			logbox.Text = "Font Packager by Lord Zedd. Open a font package!\r\nDon't mod online you fucking shitheads.";
		}
		FontPackage package;

		#region top controls
		private void btnOpenPkg_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Package";
			ofd.Filter = "Font Collection (*.bin,*.txt)|*.bin;*.txt|Single H2 Font (*.*) | *";
			if ((bool)ofd.ShowDialog())
			{
				package = new FontPackage();

				if (!ofd.FileName.EndsWith(".bin"))
				{
					switch (package.LoadH2(ofd.FileName))
					{
						case 0:
							break;
						case 1:
							WriteLog("File \"" + ofd.SafeFileName + "\" has an invalid header version value and was not loaded.");
							return;
						case 2:
							WriteLog("List \"" + ofd.SafeFileName + "\" contained no valid fonts.");
							return;
					}

					btnSavefile.IsEnabled = true;
				}
				else
				{
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

					btnSavefile.IsEnabled = false;
				}

				inputpkgpath.Text = ofd.FileName;

				WriteLog("File \"" + ofd.SafeFileName + "\" has been loaded successfully with " + package.Fonts.Count + " fonts.");

				FinishOpening();

			}
		}

		private void btnOpenFold_Click(object sender, RoutedEventArgs e)
		{
			System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();

			System.Windows.Forms.DialogResult result = fbd.ShowDialog();
			if (result.ToString() == "OK")
			{
				package = new FontPackage();

				switch (package.LoadH2Folder(fbd.SelectedPath))
				{
					case 0:
						break;
					case 1:
						WriteLog("Folder \"\\" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" contained no valid fonts.");
						return;
				}
			}
			else return;

			inputpkgpath.Text = fbd.SelectedPath;

			WriteLog("Folder \"\\" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" has been loaded successfully with " + package.Fonts.Count + " fonts.");

			btnSavefile.IsEnabled = true;
			FinishOpening();

		}

		private void btnSavepkg_Click(object sender, RoutedEventArgs e)
		{
			if (!inputpkgpath.Text.EndsWith(".bin"))
			{
				for (int i = 0; i < package.Fonts.Count; i++)
				{
					if (File.Exists(package.Fonts[i].H2File))
						package.RebuildFile(package.Fonts[i].H2File, i);
				}

				if (inputpkgpath.Text.EndsWith(".txt"))
					WriteLog("Fonts from \"" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" have been saved successfully.");
				else if (inputpkgpath.Text.EndsWith("\\"))
					WriteLog("Fonts from \"\\" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" have been saved successfully.");
				else
					WriteLog("Font \"" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" has been saved successfully.");
			}
			else
			{
				package.RebuildPkg(inputpkgpath.Text);

				WriteLog("Font Package \"" + System.IO.Path.GetFileName(inputpkgpath.Text) + "\" has been saved successfully.");
			}
			
		}

		private void btnSavefile_Click(object sender, RoutedEventArgs e)
		{
			if (!inputpkgpath.Text.EndsWith(".bin"))
			{
				if (File.Exists(package.Fonts[fontslist.SelectedIndex].H2File))
					package.RebuildFile(package.Fonts[fontslist.SelectedIndex].H2File, fontslist.SelectedIndex);

				WriteLog("Font \"" + package.Fonts[fontslist.SelectedIndex].H2FileSafe() + "\" has been saved successfully.");
			}
		}
		#endregion

		#region functions
		private void FinishOpening()
		{
			fontslist.Items.Clear();

			btnSavepkg.IsEnabled = true;
			btnAddBat.IsEnabled = true;
			btndeleteBat.IsEnabled = true;

			


			bool bigfont = false;
			orderlistfonts.Items.Clear();
			orderlistfonts.Items.Add("null");

			for (int i = 0; i < package.Fonts.Count; i++)
			{
				fontslist.Items.Add(i.ToString() + ": " + package.Fonts[i].Name + "  [" + package.Fonts[i].CharacterCount + "]");

				orderlistfonts.Items.Add(package.Fonts[i].Name);

				if (package.Fonts[i].CharacterCount > 5000)
					bigfont = true;
			}

			fontslist.SelectedIndex = 0;

			if (bigfont)
				WriteLog("NOTE: Package contains one or more fonts with over 5000 characters, performance may suffer.");

			orderlistfonts.SelectedIndex = 0;

			if (inputpkgpath.Text.EndsWith(".bin"))
			{
				orderlist.IsEnabled = true;
				btnOrder.IsEnabled = true;
				orderlistfonts.IsEnabled = true;
				txtOrderDesc.Visibility = Visibility.Visible;
				txtOrderHTwo.Visibility = Visibility.Collapsed;
				UpdateOrderDisplay();
			}
			else
			{
				txtOrderDesc.Visibility = Visibility.Collapsed;
				txtOrderHTwo.Visibility = Visibility.Visible;
			}

				
		}

		public void UpdateFontDisplay()
		{
			if (fontslist.SelectedIndex == -1)
				return ;

			lstChars.Items.Clear();

			for (int i = 0; i < package.Fonts[fontslist.SelectedIndex].Characters.Count; i++)
			{
				if (package.Fonts[fontslist.SelectedIndex].Characters[i].Data.width == 0)
					continue;
				if (package.Fonts[fontslist.SelectedIndex].Characters[i].isdupe == true)
					continue;

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

				fontChar.ToolTip = "Unicode: " + unicode.ToString("X4") + " [" + unicode.ToString() + "]" +
					"\r\nUTF8: " + utf8Code +
					"\r\nDouble click to copy as a unicode character to the clipboard." +
					"\r\n\r\nWidth: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.width +
					"\r\nDisplay Width: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.dispWidth +
					"\r\nHeight: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.height +
					"\r\nOrigin x: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.originx +
					"\r\nOrigin y: " + package.Fonts[fontslist.SelectedIndex].Characters[i].Data.originy;
				fontChar.Height = package.Fonts[fontslist.SelectedIndex].Characters[i].Data.height;
				fontChar.Padding = new Thickness(-1);
				fontChar.VerticalAlignment = System.Windows.VerticalAlignment.Center;
				fontChar.Margin = new Thickness(1.5,2.5,1.5,2.5);
				fontChar.Tag = unicode;
				fontChar.MouseDoubleClick += CopyUnicode;

				lstChars.Items.Add(fontChar);
			}

			fontAHeight.Text = package.Fonts[fontslist.SelectedIndex].AscendHeight.ToString();
			fontDHeight.Text = package.Fonts[fontslist.SelectedIndex].DescendHeight.ToString();
			fontLHeight.Text = package.Fonts[fontslist.SelectedIndex].LeadHeight.ToString();
			fontLWidth.Text = package.Fonts[fontslist.SelectedIndex].LeadWidth.ToString();
		}

		public void UpdateOrderDisplay()
		{
			orderlist.Items.Clear();

			for (int i = 0; i < package.OrderList.Count; i++)
			{
				var oi = new OrderItem() { OrderIndex = i, OrderValue = package.OrderList[i], Name = orderlistfonts.Items[package.OrderList[i] + 1].ToString() };
				orderlist.Items.Add(oi);
			}
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
				WriteLog(command + " failed: Invalid unicode entered (Box is empty)");
				return 0xFFFF;
			}

			UInt16 char2add;

			try
			{
				char2add = UInt16.Parse(input, System.Globalization.NumberStyles.HexNumber);

				if (char2add == 0xFFFF)
					WriteLog(command + " failed: Character 0xFFFF is invalid, use 0xFFFE.");

				return char2add;
			}
			catch
			{
				WriteLog(command + " failed: Invalid unicode entered (Could not parse hex)");
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

			package.Fonts[fontslist.SelectedIndex].AscendHeight = height;
			package.Fonts[fontslist.SelectedIndex].DescendHeight = tpad;
			package.Fonts[fontslist.SelectedIndex].LeadHeight = bpad;
			package.Fonts[fontslist.SelectedIndex].LeadWidth = unk;
		}

		private short ParseHeaderShort(string valuename, string input)
		{
			short output = -1;

			try
			{
				output = short.Parse(input);
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

		private ushort CharFromSelectedItem()
		{
			ListBoxItem item = (ListBoxItem)lstChars.SelectedItem;
			return ((ushort)item.Tag);
		}
		private int IndexFromSelectedItem()
		{
			return package.Fonts[fontslist.SelectedIndex].FindCharacter(CharFromSelectedItem());
		}

		public System.Drawing.Image BitmapFromFont(Font font, float offset, string txt)
		{
			StringFormat yee;

			if (txt == " ")
				yee = StringFormat.GenericDefault;
			else
				yee = StringFormat.GenericTypographic;

			System.Drawing.Bitmap bmp = new Bitmap(1, 1);
			Graphics g = Graphics.FromImage(bmp);

			PointF rect = new PointF(0, offset);
			SizeF size = g.MeasureString(txt, font, rect, yee);

			if (size.Width < 1) size.Width = 1;
			if (size.Height < 1) size.Width = 1;

			bmp = new Bitmap((int)size.Width, (int)size.Height);
			g = Graphics.FromImage(bmp);

			g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
			g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
			g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

			g.DrawString(txt, font, new SolidBrush(System.Drawing.Color.White), rect, yee);

			return bmp;
		}

		#endregion

		#region font tab controls

		private void btnFontUpdate_Click(object sender, RoutedEventArgs e)
		{
			if (fontslist.SelectedIndex == -1)
				return;

			short height = ParseHeaderShort("ascend height", fontAHeight.Text);
			short tpad = ParseHeaderShort("descend height", fontDHeight.Text);
			short bpad = ParseHeaderShort("lead height", fontLHeight.Text);
			short unk = ParseHeaderShort("lead width", fontLWidth.Text);

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

				UInt16 char2replace = CharFromSelectedItem();

				package.AddCustomCharacter(char2replace, fontslist.SelectedIndex, newpic, (CharTint)tintEnum.SelectedIndex, (bool)chkCrop.IsChecked);

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
						encoder.Frames.Add(BitmapFrame.Create(package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.getImage()));
						encoder.Save(file);
						file.Close();
						lstChars.UnselectAll();

						WriteLog("Character successfully extracted to " + sfd.SafeFileName + ".");
						break;
					case 2:
						FileStream filex = new FileStream(sfd.FileName, FileMode.Create, System.IO.FileAccess.Write);
						filex.Write(package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.compressedData, 0,
							package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.dataSize);
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
					ushort oldchar = CharFromSelectedItem();
					package.Fonts[fontslist.SelectedIndex].Characters.RemoveAt(lstChars.SelectedIndex);
					UpdateFontDisplay();

					WriteLog("Character " + oldchar.ToString("X4") + " was successfully removed!");
				}
		}

		private void btnAdd_Click(object sender, RoutedEventArgs e)
		{
			UInt16 char2add = ParseChar("Add", newChar.Text);
			if (char2add == 0xFFFF)
				return;

			var path = OpenImage();
			if (path != null)
			{
				System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);
				package.AddCustomCharacter(char2add, fontslist.SelectedIndex, newpic, (CharTint)tintEnum.SelectedIndex, (bool)chkCrop.IsChecked);

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
			short leftpad = 0;

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
				height = ushort.Parse(charOriginY.Text);
			}
			catch
			{
				WriteLog("Character update failed: Could not parse display height.");
				return;
			}

			try
			{
				leftpad = short.Parse(charOriginX.Text);
			}
			catch
			{
				WriteLog("Character update failed: Could not parse unknown value.");
				return;
			}

			package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.dispWidth = width;
			package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.originx = leftpad;
			package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.originy = height;
			

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
						

					package.AddCustomCharacter(abc.CharCodes[i], fontslist.SelectedIndex, System.Drawing.Image.FromStream(ms), CharTint.None, true, dispwidth);

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

			if (startchar > endchar)
			{
				WriteLog("Copy failed: Invalid character range.");
				return;
			}

			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Package";
			ofd.Filter = "Font Package (*.bin)|*.bin|Single H2 Font (*.*) | *";
			if ((bool)ofd.ShowDialog())
			{
				FontPackage package2 = new FontPackage();

				bool ish2 = false;

				if (ofd.FileName.EndsWith(".bin"))
					package2.Load(ofd.FileName);
				else if (!ofd.FileName.EndsWith(".txt"))
				{
					package2.LoadH2(ofd.FileName);
					ish2 = true;
				}
					
				else
				{
					WriteLog("Copy failed: Halo 2 .txt files not supported for copying.");
				}

				ushort fontindex = 0;

				if (!ish2)
				{
					fontindex = ParseChar("Copy", HOfont.Text);
					if (fontindex == 0xFFFF && !ish2)
						return;
				}

				

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
					UpdateFontInfo(package2.Fonts[fontindex].AscendHeight, package2.Fonts[fontindex].DescendHeight, package2.Fonts[fontindex].LeadHeight, package2.Fonts[fontindex].LeadWidth);

				UpdateFontDisplay();

				WriteLog("Characters successfully copied from font \"" + package2.Fonts[fontindex].Name + "\" in package \"" + ofd.SafeFileName + "\".");
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
				height = short.Parse(fontAHeight.Text);
			}
			catch
			{
				WriteLog("Height update failed: Could not parse height.");
				return;
			}

			for (int i = 0; i < package.Fonts[fontslist.SelectedIndex].Characters.Count; i++)
				package.Fonts[fontslist.SelectedIndex].Characters[i].Data.originy = (ushort)height;

			UpdateFontDisplay();
		}

		private void pcgetfonts_Click(object sender, RoutedEventArgs e)
		{
			System.Drawing.Text.InstalledFontCollection pcfonts = new System.Drawing.Text.InstalledFontCollection();

			for (int i = 0; i < pcfonts.Families.Count(); i++)
			{
				pcfontlist.Items.Add(pcfonts.Families[i].Name);
			}

			pcfontlist.SelectedIndex = 0;
		}

		private void pcimport_Click(object sender, RoutedEventArgs e)
		{
			ushort startchar = ParseChar("Copy", HOstart.Text);
			if (startchar == 0xFFFF)
				return;

			ushort endchar = ParseChar("Copy", HOend.Text);
			if (endchar == 0xFFFF)
				return;

			if (startchar > endchar)
			{
				WriteLog("Import failed: Invalid character range.");
				return;
			}

			if (pcsize.Text.Length == 0)
			{
				WriteLog("Import failed: Invalid size entered (Box is empty)");
				return;
			}

			float fontsize = 8;
			float charoffset = 0;

			try
			{
				fontsize = float.Parse(pcsize.Text);
			}
			catch
			{
				WriteLog("Import failed: Invalid size entered (Could not parse)");
				return;
			}

			try
			{
				charoffset = float.Parse(pcoffset.Text);
			}
			catch
			{
				WriteLog("Import failed: Invalid ofset entered (Could not parse)");
				return;
			}

			System.Drawing.FontStyle fontparams = System.Drawing.FontStyle.Regular;
			if ((bool)pcbold.IsChecked) fontparams = System.Drawing.FontStyle.Bold;

			Font importfont = new Font(pcfontlist.SelectedValue.ToString(), fontsize, fontparams);

			for (ushort i = startchar; i <= endchar; i++)
			{
				int existindex = package.Fonts[fontslist.SelectedIndex].Characters.FindIndex(x => x.CharCode == i);

				if (existindex != -1)
				{
					System.Drawing.Image newpic = BitmapFromFont(importfont, charoffset, ((char)i).ToString());
					if (newpic == null) continue;
					if (newpic.Width == 1) continue;
					package.AddCustomCharacter((ushort)i, fontslist.SelectedIndex, newpic, (CharTint)tintEnum.SelectedIndex, true);

					newpic.Dispose();
				}
			}

			UpdateFontDisplay();
		}
		#endregion
		#endregion

		#region batch tab controls

		private void btnOrder_Click(object sender, RoutedEventArgs e)
		{
			foreach (OrderItem item in orderlist.SelectedItems)
			{
				package.OrderList[item.OrderIndex] = orderlistfonts.SelectedIndex - 1;
			}

			UpdateOrderDisplay();
		}

		private void btnAddBat_Click(object sender, RoutedEventArgs e)
		{
			UInt16 char2add = ParseChar("Batch add", newCharBat.Text);
			if (char2add == 0xFFFF)
				return;

			var path = OpenImage();
			if (path != null)
			{
				System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);

				for (int i = 0; i < package.Fonts.Count; i++)
					package.AddCustomCharacter(char2add, i, newpic, (CharTint)tintEnum.SelectedIndex, (bool)chkCrop.IsChecked);

				newpic.Dispose();
				UpdateFontDisplay();

				WriteLog("Character " + char2add + " was successfully added from " + System.IO.Path.GetFileName(path) + " to every font.");
				newCharBat.Text = "";
				tabz.SelectedIndex = 0;
			}
		}

		private void btndeleteBat_Click(object sender, RoutedEventArgs e)
		{
			ushort char2delete = ParseChar("Batch remove", deleteBat.Text);
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
				pcimport.IsEnabled = true;

				UpdateFontDisplay();
			}
			else
			{
				btnAdd.IsEnabled = false;
				btnABC.IsEnabled = false;
				btnFontUpdate.IsEnabled = false;
				HObtn.IsEnabled = false;
				btnHFix.IsEnabled = false;
				pcimport.IsEnabled = false;
				lstChars.Items.Clear();

				fontAHeight.Text = "";
				fontDHeight.Text = "";
				fontLHeight.Text = "";
				fontLWidth.Text = "";
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

				charWidth.Text = package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.dispWidth.ToString();
				charOriginX.Text = package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.originx.ToString();
				charOriginY.Text = package.Fonts[fontslist.SelectedIndex].Characters[IndexFromSelectedItem()].Data.originy.ToString();
				
			}
			else
			{
				btnReplace.IsEnabled = false;
				btnExtract.IsEnabled = false;
				btnDelete.IsEnabled = false;
				btnCharUpdate.IsEnabled = false;

				charWidth.Text = "";
				charOriginY.Text = "";
				charOriginX.Text = "";
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

		public class OrderItem
		{
			public int OrderIndex { get; set; }
			public int OrderValue { get; set; }
			public string Name { get; set; }
		}
	}
}

