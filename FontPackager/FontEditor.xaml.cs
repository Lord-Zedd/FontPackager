using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontPackager.Dialogs;
using FontPackager.Classes;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for FontEditor.xaml
	/// </summary>
	public partial class FontEditor : Window
	{
		public BlamFont Font { get; set; }

		public static RoutedCommand ReplaceChar = new RoutedCommand();
		public static RoutedCommand ExtractChar = new RoutedCommand();
		public static RoutedCommand ExtractBat = new RoutedCommand();
		public static RoutedCommand RemoveChar = new RoutedCommand();

		public static RoutedCommand AddChar = new RoutedCommand();
		public static RoutedCommand AddBat = new RoutedCommand();

		List<CharacterRange> _ranges = new List<CharacterRange>()
		{
			new CharacterRange(-1),
			new CharacterRange(0),
			new CharacterRange(1),
			new CharacterRange(2),
			new CharacterRange(3),
			new CharacterRange(4),
			new CharacterRange(5),
			new CharacterRange(6),
			new CharacterRange(7),
			new CharacterRange(8),
			new CharacterRange(9),
			new CharacterRange(0xA),
			new CharacterRange(0xB),
			new CharacterRange(0xC),
			new CharacterRange(0xD),
			new CharacterRange(0xE),
			new CharacterRange(0xF),
		};
		public List<CharacterRange> Ranges { get { return _ranges; } }

		bool itemwasclicked = false;

		public FontEditor(BlamFont font)
		{
			Font = font;

			InitializeComponent();

			UpdateCharacterList();
			UpdateHeader();
		}

		#region tooltips
		static string _pressenterf = "Press Enter to update this header value.";
		static string _pressenterc = "Press Enter to update this value for all selected characters.";
		static string _posneg = "Can be positive or negative, but negative/extreme values may cause the character to get cut off by the game in some situations.\r\n";
		public static string FontNameToolTip
		{
			get
			{
				return "The name of this font.\r\n" +
			_pressenterf;
			}
		}
		public static string MCCScaleToolTip
		{
			get
			{
				return "This value is used in MCC to scale higher-res fonts down.\r\n" +
			"Expected usage is that it should match the file's name (1 for original, 2 for x2, etc.) but any nonzero number works.\r\n" +
			"A value of 1 will display pixels as-is, where higher numbers will downsample.\r\n" +
			"This can be ignored if you aren't modding MCC.\r\n" +
			_pressenterf;
			}
		}
		public static string AscendHeightToolTip
		{
			get
			{
				return "Ascend Height is the amount of space from the top of the line to the base line.\r\n" +
			"Typically equal to the Y origin of all characters.\r\n" +
			"If a PC font doesn't line up correctly, try setting this value to that of the original font's.\r\n" +
			_posneg +
			_pressenterf;
			}
		}
		public static string DescendHeightToolTip
		{
			get
			{
				return "Descend Height is the amount of space from the base line to the bottom of the line.\r\n" +
			"This value, when added to the Ascend Height plus one, should usually equal the average character/line height.\r\n" +
			_posneg +
			_pressenterf;
			}
		}
		public static string LeadHeightToolTip
		{
			get
			{
				return "Lead Height is added space between lines of a single string.\r\n" +
			"This value is usually zero.\r\n" +
			_posneg +
			_pressenterf;
			}
		}
		public static string LeadWidthToolTip
		{
			get
			{
				return "Lead Width is added space before each line, shifting everything over.\r\n" +
			"This value is usually zero.\r\n" +
			_posneg +
			_pressenterf;
			}
		}

		public static string DisplayWidthToolTip
		{
			get
			{
				return "Display Width is how wide the game treats this character when drawing.\r\n" +
			"Values less than the width of the actual image will result in visual overlap.\r\n" +
			"Cannot be negative.\r\n" +
			_pressenterc;
			}
		}
		public static string XOriginToolTip
		{
			get
			{
				return "X Origin is the horizontal offset applied by the game when drawing.\r\n" +
			_posneg +
			_pressenterc;
			}
		}
		public static string YOriginToolTip
		{
			get
			{
				return "Y Origin is the vertical offset applied by the game when drawing.\r\n" +
			"This value represents the base line for which most alphanumeric characters sit on top of. Typically equal across all characters and the font's Ascend Height.\r\n" +
			"If icons don't line up correctly with a PC font, try setting this value to the original font's Ascend Height.\r\n" +
			_posneg +
			_pressenterc;
			}
		}
		#endregion

		#region general
		private void window_Closing(object sender, CancelEventArgs e)
		{
			listchars.ItemsSource = null;
		}

		private void UpdateHeader()
		{
			Title = "Font Packager - " + Font.Name;
		}

		private CharTint GetTintSetting()
		{

			if (menuTCool.IsChecked)
				return CharTint.Cool;
			else if (menuTWarm.IsChecked)
				return CharTint.Warm;
			else
				return CharTint.None;
		}
		
		private void Save_Click(object sender, RoutedEventArgs e)
		{
			switch((string)((MenuItem)sender).Tag)
			{
				case "h2x":
					SaveLoose(FileFormat.H2X);
					break;
				case "h2v":
					SaveLoose(FileFormat.H2V);
					break;
				case "h3b":
					SaveLoose(FileFormat.H3B);
					break;
				case "h2mcc":
					SaveLoose(FileFormat.H2MCC);
					break;
			}
		}

		private void SaveLoose(FileFormat format)
		{
			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Font File";
			sfd.Filter = "Loose Font (*)|*";
			sfd.FileName = Font.SanitizedName;
			if (!(bool)sfd.ShowDialog())
				return;

			if (!VerifyFont(format))
				return;

			TableIO.WriteLooseFile(Font, sfd.FileName, format);
		}

		public bool VerifyFont(FileFormat format)
		{
			string result = Font.Verify(format);

			if (string.IsNullOrEmpty(result))
				return true;

			ListDialog ve = new ListDialog(result, true);
			ve.ShowDialog();

			return ve.IgnoreErrors;
		}

		private void bg_Checked(object sender, RoutedEventArgs e)
		{
			if (menuBLgtCheck.IsChecked)
				listchars.Background = (ImageBrush)Resources["LightCheck"];
			else if (menuBDrk.IsChecked)
				listchars.Background = (Brush)Resources["DarkSolid"];
			else if (menuBLgt.IsChecked)
				listchars.Background = (Brush)Resources["LightSolid"];
			else
				listchars.Background = (ImageBrush)Resources["DarkCheck"];
		}

		private void UpdateCharacterList()
		{
			listchars.ItemsSource = null;
			
			UpdateRanges();
			
			CharacterRange selected = (CharacterRange)bigrange.SelectedItem;
			int min = selected.Minimum;
			int max = selected.Maximum;

			listchars.ItemsSource = Font.Characters.Where(x=>x.UnicIndex >= min && x.UnicIndex <= max);
		}

		private void UpdateRanges()
		{
			foreach (CharacterRange cr in Ranges)
			{
				int min = cr.Minimum;
				int max = cr.Maximum;
				cr.Count = Font.Characters.Where(x => x.UnicIndex >= min && x.UnicIndex <= max).Count();
			}

			if (bigrange.SelectedIndex == -1)
			{
				if (Ranges[0].IsEnabled)
					bigrange.SelectedIndex = 0;
				else
					bigrange.SelectedIndex = 1;
			}

		}

		private void bigrange_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UpdateCharacterList();
		}
		#endregion

		#region character listbox
		private void listchars_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (itemwasclicked)
				return;
			listchars.UnselectAll();

			charWidth.Text = "";
			charOriginX.Text = "";
			charOriginY.Text = "";
		}

		private void listchars_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			bool first = true;
			uint? lastdw = null;
			int? lastox = null;
			int? lastoy = null;

			foreach (BlamCharacter bc in listchars.SelectedItems)
			{
				if (first)
				{
					lastdw = bc.DisplayWidth;
					lastox = bc.OriginX;
					lastoy = bc.OriginY;
					first = false;
				}
				else
				{
					if (lastdw.HasValue && lastdw.Value != bc.DisplayWidth)
						lastdw = null;

					if (lastox.HasValue && lastox.Value != bc.OriginX)
						lastox = null;

					if (lastoy.HasValue && lastoy.Value != bc.OriginY)
						lastoy = null;
				}

			}

			if (lastdw.HasValue)
				charWidth.Text = lastdw.Value.ToString();
			else
				charWidth.Text = "";

			if (lastox.HasValue)
				charOriginX.Text = lastox.Value.ToString();
			else
				charOriginX.Text = "";

			if (lastoy.HasValue)
				charOriginY.Text = lastoy.Value.ToString();
			else
				charOriginY.Text = "";

		}

		private void listchars_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (Keyboard.IsKeyDown(Key.Delete))
			{
				RemoveSelectedCharacters();
			}
		}

		private void listchars_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			itemwasclicked = false;
		}

		#endregion

		#region value updating
		private void fontName_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				if (string.IsNullOrEmpty(fontName.Text))
					return;

				fontName.GetBindingExpression(TextBox.TextProperty).UpdateSource();
				UpdateHeader();
			}
		}

		private void HeaderShort_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				var box = (TextBox)sender;

				string txt = box.Text;
				if (string.IsNullOrEmpty(txt))
					return;

				short val;
				bool parsed = short.TryParse(txt, out val);

				if (!parsed)
					return;

				switch ((string)box.Tag)
				{
					case "aheight":
						Font.AscendHeight = val;
						break;
					case "dheight":
						Font.DescendHeight = val;
						break;
					case "lheight":
						Font.LeadHeight = val;
						break;
					case "lwidth":
						Font.LeadWidth = val;
						break;
				}
			}
		}

		private void HeaderInt_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				var box = (TextBox)sender;

				string txt = box.Text;
				if (string.IsNullOrEmpty(txt))
					return;

				int val;
				bool parsed = int.TryParse(txt, out val);

				if (!parsed)
					return;

				if (val == 0)
				{
					val = 1;
					box.Text = "1";
				}

				switch ((string)box.Tag)
				{
					case "mccscale":
						Font.MCCScale = val;
						break;
				}
			}
		}

		private void CharacterWidth_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				var box = (TextBox)sender;

				string txt = box.Text;
				if (string.IsNullOrEmpty(txt) || listchars.SelectedItems.Count == 0)
					return;

				uint val;
				bool parsed = uint.TryParse(txt, out val);

				if (!parsed)
					return;

				foreach (BlamCharacter bc in listchars.SelectedItems)
					bc.DisplayWidth = val;
			}
		}

		private void CharacterShort_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				var box = (TextBox)sender;

				string txt = box.Text;
				if (string.IsNullOrEmpty(txt) || listchars.SelectedItems.Count == 0)
					return;

				short val;
				bool parsed = short.TryParse(txt, out val);

				if (!parsed)
					return;

				switch ((string)box.Tag)
				{
					case "corx":
						foreach (BlamCharacter bc in listchars.SelectedItems)
							bc.OriginX = val;
						break;
					case "cory":
						foreach (BlamCharacter bc in listchars.SelectedItems)
							bc.OriginY = val;
						break;
				}


			}
		}
		#endregion

		#region context menu
		private void ReplaceChar_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			AddCharacter(((BlamCharacter)listchars.SelectedItem).UnicIndex);
		}

		private void SingleChar_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listchars.SelectedItems.Count == 1)
				e.CanExecute = true;
			else
				e.CanExecute = false;
		}

		private void ExtractChar_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Font Character";
			sfd.Filter = "PNG Image (*.png)|*.png;|Raw Data (Debug) (*.bin)|*.bin;";
			if (!(bool)sfd.ShowDialog())
				return;

			BlamCharacter selected = (BlamCharacter)listchars.SelectedItem;

			switch (sfd.FilterIndex)
			{
				case 1:
					using (FileStream file = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
					{
						BitmapEncoder encoder = new PngBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(selected.Image));
						encoder.Save(file);
					}
					break;
				case 2:
					using (FileStream file = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
					{
						file.Write(selected.CompressedData, 0, selected.CompressedSize);
					}	
					break;
			}
		}

		private void ExtractBat_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var res = MessageBox.Show("There are " + listchars.SelectedItems.Count + " characters currently selected. Each character will be saved as a separate image file once a folder is chosen. Are you sure?", "Confirm Batch Extraction", MessageBoxButton.OKCancel);

			if (res != MessageBoxResult.OK)
				return;

			using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
			{
				System.Windows.Forms.DialogResult result = fbd.ShowDialog();
				if (!(result.ToString() == "OK"))
					return;

				foreach (BlamCharacter bc in listchars.SelectedItems)
				{
					using (FileStream file = new FileStream(fbd.SelectedPath + "\\" + bc.UnicIndex.ToString("X4") + ".png", FileMode.Create, FileAccess.Write))
					{
						BitmapEncoder encoder = new PngBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(bc.Image));
						encoder.Save(file);
					}

				}
			}
		}

		private void RemoveChar_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			RemoveSelectedCharacters();
		}

		private void AddChar_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			UnicodeInput uinput = new UnicodeInput(Font.Name);
			uinput.ShowDialog();

			if (uinput.DialogResult.Value == false)
				return;

			AddCharacter(uinput.Unicode);
		}

		private void AddBat_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string[] files;

			using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
			{
				System.Windows.Forms.DialogResult result = fbd.ShowDialog();
				if (!(result.ToString() == "OK"))
					return;

				files = Directory.GetFiles(fbd.SelectedPath, "*.png");
			}

			int successcount = 0;
			int badnamecount = 0;
			int badconvertcount = 0;
			int badverifycount = 0;

			if (files.Length == 0)
			{
				MessageBox.Show("Selected folder contains no png images.");
				return;
			}
			
			string allerrors = "";

			foreach (string s in files)
			{
				string filename = Path.GetFileNameWithoutExtension(s);

				ushort unic;
				bool parsed = ushort.TryParse(filename, NumberStyles.HexNumber, null, out unic);

				if (!parsed)
				{
					badnamecount++;
					continue;
				}

				System.Drawing.Image newpic = System.Drawing.Image.FromFile(s);

				BlamCharacter newchar = CharacterTools.CreateCharacter(unic, newpic, GetTintSetting(), menuCrop.IsChecked);
				newpic.Dispose();

				if (newchar == null)
				{
					badconvertcount++;
					continue;
				}

				string errors = newchar.Verify(((MainWindow)Application.Current.MainWindow).TargetFormat);

				if (!string.IsNullOrEmpty(errors))
				{
					badverifycount++;
					allerrors += errors;
					continue;
				}

				Font.AddCharacter(newchar);

				successcount++;

			}

			string badname = "";
			if (badnamecount > 0)
				badname = "\r\n" + badnamecount + " characters had an unexpected/invalid filename and were not added. Filename should be only the hex unicode index. (ex: E100.png)";
			string badconvert = "";
			if (badconvertcount > 0)
				badconvert = "\r\n" + badconvertcount + " characters could not be converted and were not added.";
			string badverify = "";
			if (!string.IsNullOrEmpty(allerrors))
				badverify = "\r\n" + badverifycount + " characters failed verification. The errors returned were:\r\n";
			UpdateCharacterList();

			ListDialog ve = new ListDialog("Batch Add",
				"Batch add completed.\r\n" + successcount + " characters were added/replaced successfully. Any errors, if any will appear below:",
				badname + badconvert + badverify + allerrors, false);
			ve.ShowDialog();
		}


		private void ExtractBat_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listchars.SelectedItems.Count > 0)
				e.CanExecute = true;
			else
				e.CanExecute = false;
		}
		#endregion

		#region char manipulation
		private static string OpenImage()
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

		private void AddCharacter(ushort ch)
		{
			var path = OpenImage();
			if (path == null)
				return;


			System.Drawing.Image newpic = System.Drawing.Image.FromFile(path);

			BlamCharacter newchar = CharacterTools.CreateCharacter(ch, newpic, GetTintSetting(), menuCrop.IsChecked);
			newpic.Dispose();
			if (newchar == null)
			{
				MessageBox.Show("The given image failed to convert. The resolution could be too large or the wrong format.");
				return;
			}
			
			string errors = newchar.Verify(((MainWindow)Application.Current.MainWindow).TargetFormat);

			if (!string.IsNullOrEmpty(errors))
			{
				ListDialog ve = new ListDialog(errors, true);
				ve.ShowDialog();
				if (!ve.IgnoreErrors)
					return;
			}

			Font.AddCharacter(newchar);

			UpdateCharacterList();
		}

		private void RemoveSelectedCharacters()
		{
			if (listchars.SelectedItems.Count == 0)
				return;

			List<ushort> unics = new List<ushort>();

			foreach (BlamCharacter bc in listchars.SelectedItems)
				unics.Add(bc.UnicIndex);

			var res = MessageBox.Show(unics.Count.ToString() + " characters will be removed. Continue?", "Confirm Remove", MessageBoxButton.OKCancel);

			if (res != MessageBoxResult.OK)
				return;

			listchars.UnselectAll();
			foreach (ushort ch in unics)
				Font.RemoveCharacter(ch);

			UpdateCharacterList();
		}
		#endregion

		#region drag and drop
		private void charitem_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && listchars.SelectedItems.Count > 0)
			{
				itemwasclicked = false;
				DataObject newe = new DataObject();
				newe.SetData(Font);
				var chars = new List<BlamCharacter>();

				foreach (BlamCharacter bc in listchars.SelectedItems)
					chars.Add(bc);

				newe.SetData(chars);

				DragDrop.DoDragDrop((listchars), newe, DragDropEffects.Copy);	
			}
		}

		private void listchars_Drop(object sender, DragEventArgs e)
		{
			List<BlamCharacter> chars = (List<BlamCharacter>)(e.Data.GetData(typeof(List<BlamCharacter>)));
			BlamFont font = (BlamFont)(e.Data.GetData(typeof(BlamFont)));

			if (chars == null || font == Font)
				return;

			int overwritecount = chars.Count(f=> Font.FindCharacter(f.UnicIndex) != -1);
			if (overwritecount > 0)
			{
				var res = MessageBox.Show("The dragged character(s) will overwrite " + overwritecount.ToString() + " existing characters. Overwrite?", "Confirm Overwrite", MessageBoxButton.OKCancel);

				if (res != MessageBoxResult.OK)
					return;
			}

			foreach (BlamCharacter bc in chars)
			{
				BlamCharacter bcnew = new BlamCharacter(bc.UnicIndex);
				bcnew.DisplayWidth = bc.DisplayWidth;
				bcnew.Height = bc.Height;
				bcnew.Width = bc.Width;
				bcnew.OriginX = bc.OriginX;
				bcnew.OriginY = bc.OriginY;
				bcnew.CompressedData = new byte[bc.CompressedSize];

				Array.Copy(bc.CompressedData, bcnew.CompressedData, bcnew.CompressedSize);

				Font.AddCharacter(bcnew);
			}
			UpdateCharacterList();
		}

		private void listchars_PreviewDrag(object sender, DragEventArgs e)
		{
			List<BlamCharacter> chars = (List<BlamCharacter>)(e.Data.GetData(typeof(List<BlamCharacter>)));
			BlamFont font = (BlamFont)(e.Data.GetData(typeof(BlamFont)));

			if (chars == null || font == Font)
			{
				e.Effects = DragDropEffects.None;
				e.Handled = true;
			}
		}
		
		private void charitem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			ListBoxItem q = (ListBoxItem)sender;

			if (q.IsSelected)
			{
				if (e.ClickCount == 1)
				{
					itemwasclicked = true;
					e.Handled = true;
				}
				else if (e.ClickCount == 2)
				{
					BlamCharacter bc = (BlamCharacter)q.DataContext;
					Clipboard.SetText(Convert.ToChar(bc.UnicIndex).ToString());
				}
			}
		}

		private void charitem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (itemwasclicked)
			{
				BlamCharacter bc = (BlamCharacter)((ListBoxItem)sender).DataContext;

				itemwasclicked = false;

				if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
				{
					if (listchars.SelectedItems.Contains(bc))
						listchars.SelectedItems.Remove(bc);
				}
				else
					listchars.SelectedItem = bc;
			}
		}

		private void charitem_MouseLeave(object sender, MouseEventArgs e)
		{
			itemwasclicked = false;
		}

		#endregion


	}

	public class CharacterRange : INotifyPropertyChanged
	{
		public int Index { get; set; }

		public string Name
		{
			get
			{
				if (Index == -1)
					return "Full Range [" + Count + "]";
				else
					return Minimum.ToString("X4") + " - " + Maximum.ToString("X4") + " [" + Count + "]";
			}
		}

		int _count;
		public int Count
		{
			get { return _count; }
			set { _count = value; NotifyPropertyChanged("Count"); NotifyPropertyChanged("Name"); }
		}

		public CharacterRange(int index)
		{
			if (index < -1 || index > 15)
				throw new ArgumentException("Given index for CharacterRange is unexpected.");
			Index = index;
		}

		public int Minimum
		{
			get
			{
				int result = 0;
				if (Index > 0)
					result = Index << 12;

				return result;
			}

		}

		public int Maximum
		{
			get
			{
				int result = 0xFFFF;
				if (Index > -1)
					result = ((Index + 1) << 12) - 1;

				return result;
			}

		}

		public bool IsEnabled
		{
			get
			{
				if (Index == -1)
					return Count < 0x1000;
				else
					return true;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

}
