﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using FontPackager.Dialogs;
using FontPackager.Classes;
using System;
using System.Windows.Data;
using System.Globalization;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public ObservableCollection<BlamFont> Fonts { get; set; }
		public ObservableCollection<EngineOrderItem> EngineOrdering { get; set; }

		public FileFormat TargetFormat { get { return (FileFormat)((ComboBoxItem)cmbFmt.SelectedItem).Tag; } }

		private string LastFilePath = "";

		private List<FontEditor> OpenEditors;

		bool isdropping_reorder = false;
		FontCreator fc = null;
		
		public MainWindow()
		{
			InitializeComponent();
			OpenEditors = new List<FontEditor>();
		}

		#region general

		private void CopyCollection(BlamFont font)
		{
			CopyCollection(new List<BlamFont>() { font });
		}
		private void CopyCollection(List<BlamFont> fonts)
		{
			foreach (BlamFont f in fonts)
				Fonts.Add(f);

			RefreshFontList();
		}
		private void CopyOrders(List<int> orders)
		{
			for (int i = 0; i < 64; i++)
			{
				if (orders != null && i < orders.Count && orders[i] != -1)
					EngineOrdering.Add(new EngineOrderItem(Fonts[orders[i]]));
				else
					EngineOrdering.Add(new EngineOrderItem(null));
			}

			RefreshOrderList();
		}
		private List<int> CreateOrderList()
		{
			List<int> orders = new List<int>();
			foreach (EngineOrderItem o in EngineOrdering)
				orders.Add(Fonts.IndexOf(o.Font));
			return orders;
		}

		private void RefreshFontList()
		{
			listfonts.ItemsSource = null;
			listfonts.ItemsSource = Fonts;
		}

		private void RefreshOrderList()
		{
			listengineorders.ItemsSource = null;
			listengineorders.ItemsSource = EngineOrdering;
		}
		
		public bool VerifyFonts(FileFormat format)
		{
			string result = "";
			using (StringWriter sw = new StringWriter())
			{
				foreach (BlamFont font in Fonts)
				{
					string f = font.Verify(format);
					if (!string.IsNullOrEmpty(f))
					{
						sw.WriteLine("~" + font.Name);
						sw.Write(f);
						sw.WriteLine();
					}
				}

				result = sw.ToString();
			}

			if (string.IsNullOrEmpty(result))
				return true;

			ListDialog ve = new ListDialog(result, true);
			ve.ShowDialog();

			return ve.IgnoreErrors;
		}

		private void CloseEditors()
		{
			foreach (FontEditor e in OpenEditors)
			{
				e.Closing -= Editor_Closing;
				e.Close();
			}

			OpenEditors.Clear();
		}

		private void ClearLists()
		{
			listengineorders.ItemsSource = null;
			listfonts.ItemsSource = null;

			if (EngineOrdering != null)
				EngineOrdering.Clear();

			if (Fonts != null)
				Fonts.Clear();

			Fonts = new ObservableCollection<BlamFont>();
			EngineOrdering = new ObservableCollection<EngineOrderItem>();
		}

		#endregion

		#region loading
		private void FinishLoading(FileFormat format, List<BlamFont> fonts, List<int> orders)
		{
			CloseEditors();

			ClearLists();

			switch(format)
			{
				case FileFormat.H2X:
					cmbFmt.SelectedIndex = 0;
					break;
				case FileFormat.H2V:
					cmbFmt.SelectedIndex = 1;
					break;
				case FileFormat.H3B:
					cmbFmt.SelectedIndex = 2;
					break;
				case FileFormat.H2MCC:
					cmbFmt.SelectedIndex = 3;
					break;

				default:
				case FileFormat.Package:
					cmbFmt.SelectedIndex = 4;
					break;
				case FileFormat.H4B:
					cmbFmt.SelectedIndex = 5;
					break;
				case FileFormat.H4:
					cmbFmt.SelectedIndex = 6;
					break;
				case FileFormat.MCC:
					cmbFmt.SelectedIndex = 7;
					break;
				case FileFormat.H2AMCC:
					cmbFmt.SelectedIndex = 8;
					break;
			}

			CopyCollection(fonts);
			CopyOrders(orders);
			
			fname.Text = Path.GetFileName(LastFilePath);
			fname.ToolTip = LastFilePath;
			
			MessageBox.Show("\"" + Path.GetFileName(LastFilePath) + "\" has been loaded successfully with " + Fonts.Count + " fonts.");
			
			menuSaveAs.IsEnabled = true;
			menuTools.IsEnabled = true;
		}

		private static Tuple<string, FileFormat, List<BlamFont>, List<int>> OpenAndLoadPackage()
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Package";
			ofd.Filter = "Font Package (*.bin)|*.bin";
			if (!(bool)ofd.ShowDialog())
				return null;

			string filename = ofd.FileName;
			var res = PackageIO.Read(ofd.FileName);

			switch (res.Item1)
			{
				case IOError.None:
					return new Tuple<string, FileFormat, List<BlamFont>, List<int>>
						(filename, res.Item2, res.Item3, res.Item4);
				case IOError.BadVersion:
					MessageBox.Show("Package \"" + ofd.SafeFileName + "\" has an invalid header version value and was not loaded.");
					return null;
				case IOError.Empty:
					MessageBox.Show("Package \"" + ofd.SafeFileName + "\" has a font count of 0 and was not loaded.");
					return null;
				default:
					MessageBox.Show("An unknown error occurred loading package \"" + ofd.SafeFileName + "\".");
					return null;
			}
		}

		private static Tuple<string, FileFormat, List<BlamFont>, List<int>> OpenAndLoadTable()
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Table";
			ofd.Filter = "Font Table (*.txt)|*.txt";
			if (!(bool)ofd.ShowDialog())
				return null;

			string filename = ofd.FileName;
			var res = TableIO.ReadTable(ofd.FileName);

			switch (res.Item1)
			{
				case IOError.None:
					return new Tuple<string, FileFormat, List<BlamFont>, List<int>>
						(filename, res.Item2, res.Item3, res.Item4);
				case IOError.BadVersion:
					MessageBox.Show("A font within list \"" + ofd.SafeFileName + "\" had an invalid header version value and loading was cancelled.");
					return null;
				case IOError.Empty:
					MessageBox.Show("List \"" + ofd.SafeFileName + "\" has no valid fonts.");
					return null;
				default:
					MessageBox.Show("An unknown error occurred loading list \"" + ofd.SafeFileName + "\".");
					return null;
			}
		}

		private static List<BlamFont> OpenAndImportLooseFonts()
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Font Files";
			ofd.Filter = "Single H2 Font (*)|*";
			ofd.Multiselect = true;
			if (!(bool)ofd.ShowDialog() || ofd.FileNames.Length == 0)
				return null;

			List<BlamFont> fonts = new List<BlamFont>();

			foreach (string fn in ofd.FileNames)
			{
				var res = TableIO.ReadLooseFile(fn);

				switch (res.Item1)
				{
					case IOError.None:
						fonts.Add(res.Item3);
						break;
					case IOError.BadVersion:
						MessageBox.Show("Font \"" + Path.GetFileName(fn) + "\" had an invalid header version value and was not loaded.");
						break;
					default:
						MessageBox.Show("An unknown error occurred loading font file \"" + Path.GetFileName(fn) + "\".");
						break;
				}
				continue;
			}

			if (fonts.Count == 0)
				return null;

			return fonts;
		}

		private static Tuple<string, List<BlamFont>> OpenAndImportCacheFonts()
		{
			Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Title = "Open Cache File";
			ofd.Filter = "Halo CE Cache File (*.map)|*.map";
			if (!(bool)ofd.ShowDialog())
				return null;

			string filename = ofd.FileName;
			var res = TagIO.Read(ofd.FileName);

			switch (res.Item1)
			{
				case IOError.None:
					return new Tuple<string, List<BlamFont>>
						(filename, res.Item2);
				case IOError.BadVersion:
					MessageBox.Show("Cache \"" + ofd.SafeFileName + "\" has an invalid header version (Not an Xbox CE map) and was not loaded.");
					return null;
				case IOError.Empty:
					MessageBox.Show("Cache \"" + ofd.SafeFileName + "\" has no font tags.");
					return null;
				default:
					MessageBox.Show("An unknown error occurred loading cache \"" + ofd.SafeFileName + "\".");
					return null;
			}
		}

		private static Tuple<string, List<BlamFont>> OpenAndImportDirectory()
		{
			using (System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog())
			{
				System.Windows.Forms.DialogResult result = fbd.ShowDialog();
				if (!(result.ToString() == "OK"))
					return null;

				string folder = Path.GetFileName(fbd.SelectedPath);
				var res = TableIO.ReadDirectory(fbd.SelectedPath);

				switch (res.Item1)
				{
					case IOError.None:
						return new Tuple<string, List<BlamFont>>
							(folder, res.Item2);
					case IOError.BadVersion:
						MessageBox.Show("A font within folder \"\\" + folder + "\" had an invalid header version value and loading was cancelled.");
						return null;
					case IOError.Empty:
						MessageBox.Show("Folder \"\\" + folder + "\" contained no valid fonts.");
						return null;
					default:
						MessageBox.Show("An unknown error occurred loading folder \"\\" + folder + "\".");
						return null;
				}
			}

		}
		#endregion

		#region saving
		private bool SavePackage(FileFormat format)
		{
			string defaultname = "font_package";
			if (!string.IsNullOrEmpty(fname.Text) && Path.GetExtension(fname.Text) == ".bin")
				defaultname = Path.GetFileNameWithoutExtension(fname.Text);

			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Font Package";
			sfd.Filter = "Font Package (*.bin)|*.bin";
			sfd.FileName = defaultname;
			if (!(bool)sfd.ShowDialog())
				return false;

			if (!VerifyFonts(format))
				return false;

			PackageIO.Write(Fonts.ToList(), CreateOrderList(), sfd.FileName, format);

			LastFilePath = sfd.FileName;

			fname.Text = Path.GetFileName(LastFilePath);
			fname.ToolTip = LastFilePath;

			return true;
		}

		private bool SaveTable(FileFormat format)
		{
			string defaultname = "font_table";
			if (!string.IsNullOrEmpty(fname.Text) && Path.GetExtension(fname.Text) == ".txt")
				defaultname = Path.GetFileNameWithoutExtension(fname.Text);

			Microsoft.Win32.SaveFileDialog sfd = new Microsoft.Win32.SaveFileDialog();
			sfd.RestoreDirectory = true;
			sfd.Title = "Save Font File";
			sfd.Filter = "Font Table (*.txt)|*.txt";
			sfd.FileName = defaultname;
			if (!(bool)sfd.ShowDialog())
				return false;

			if (!VerifyFonts(format))
				return false;

			TableIO.WriteTable(Fonts.ToList(), CreateOrderList(), sfd.FileName, format);

			LastFilePath = sfd.FileName;

			fname.Text = Path.GetFileName(LastFilePath);
			fname.ToolTip = LastFilePath;

			return true;
		}

		#endregion

		#region menus
		private void btnNew_Click(object sender, RoutedEventArgs e)
		{

			if (Fonts != null && Fonts.Count > 0)
			{
				var res = MessageBox.Show("Are you sure you want to create a new collection? All fonts will be removed including any unsaved changes!", "Confirm New Collection", MessageBoxButton.OKCancel);
				if (res != MessageBoxResult.OK)
					return;
			}

			CloseEditors();

			ClearLists();

			fname.Text = string.Empty;
			fname.ToolTip = null;

			listfonts.ItemsSource = Fonts;
			CopyOrders(null);
			
			menuSaveAs.IsEnabled = true;
			menuTools.IsEnabled = true;
		}

		private void btnOpen_Click(object sender, RoutedEventArgs e)
		{
			switch ((string)((MenuItem)sender).Tag)
			{
				case "package":
					{
						var res = OpenAndLoadPackage();
						if (res == null)
							return;

						LastFilePath = res.Item1;
						FinishLoading(res.Item2, res.Item3, res.Item4);
					}
					break;
				case "table":
					{
						var res = OpenAndLoadTable();
						if (res == null)
							return;

						FileFormat fmt = res.Item2;
						if (fmt == FileFormat.Table)
						{
							FontTablePickGame picker = new FontTablePickGame();
							picker.ShowDialog();

							if (picker.DialogResult == false)
								return;

							fmt = picker.Game;
						}

						LastFilePath = res.Item1;
						FinishLoading(fmt, res.Item3, res.Item4);
					}
					break;
				default:
					throw new NotImplementedException();
			}

			
		}
		
		private void btnSave_Click(object sender, RoutedEventArgs e)
		{
			if (Fonts.Count == 0)
			{
				MessageBox.Show("Add at least 1 font to save a collection.");
				return;
			}

			bool success = false;

			if (TargetFormat.HasFlag(FileFormat.Table))
			{
				if (Fonts.Count > 12)
				{
					MessageBox.Show("The table format only supports up to 12 fonts. Remove some to save as this format.");
					return;
				}
				success = SaveTable(TargetFormat);
			}
				
			else if (TargetFormat.HasFlag(FileFormat.Package))
			{
				int max = TargetFormat.HasFlag(FileFormat.Max64) ? 64 : 16;

				if (Fonts.Count > max)
				{
					MessageBox.Show("The chosen package format only supports up to " + max + " fonts. Remove some to save as this format.");
					return;
				}
				success = SavePackage(TargetFormat);
			}

			if (success)
				MessageBox.Show("\"" + Path.GetFileName(LastFilePath) + "\" has been saved successfully.");
		}

		private void btnImport_Click(object sender, RoutedEventArgs e)
		{
			List<BlamFont> fonts = null;
			string filename = "";
			bool skipdialog = false;
			switch ((string)((MenuItem)sender).Tag)
			{
				case "package":
					{
						var res = OpenAndLoadPackage();
						if (res == null)
							return;

						filename = res.Item1;
						fonts = res.Item3;
					}
					break;
				case "table":
					{
						var res = OpenAndLoadTable();
						if (res == null)
							return;

						filename = res.Item1;
						fonts = res.Item3;
					}
					break;
				case "loose":
					{
						var res = OpenAndImportLooseFonts();
						if (res == null)
							return;

						skipdialog = true;
						fonts = res;
					}
					break;
				case "cache":
					{
						var res = OpenAndImportCacheFonts();
						if (res == null)
							return;

						filename = res.Item1;
						fonts = res.Item2;
					}
					break;
				default:
					throw new NotImplementedException();
			}

			if (skipdialog)
			{
				CopyCollection(fonts);
				MessageBox.Show("Successfully imported " + fonts.Count + " font files.");
			}
			else
			{
				string shortname = Path.GetFileName(filename);
				FontImport importer = new FontImport(fonts, shortname);
				importer.ShowDialog();

				if (importer.DialogResult == false)
				{
					fonts = null;
					importer = null;
					return;
				}
				
				CopyCollection(importer.SelectedFonts);

				MessageBox.Show("Successfully imported " + importer.SelectedFonts.Count + " fonts from \"" + shortname + "\".");
				importer = null;
			}

			fonts = null;
		}

		private void PCImport_Click(object sender, RoutedEventArgs e)
		{
			if (fc != null)
			{
				fc.Focus();
				return;
			}
				
			fc = new FontCreator();
			fc.Closing += FontCreator_Closing;
			fc.Owner = this;
			fc.Show();
		}

		#endregion

		private void window_Closing(object sender, CancelEventArgs e)
		{
			CloseEditors();
		}

		private void FontCreator_Closing(object sender, CancelEventArgs e)
		{
			if (fc.Font != null)
				CopyCollection(fc.Font);

			fc.Closing -= FontCreator_Closing;
			fc = null;
		}

		public void ImportCreatedFont(BlamFont font)
		{
			if (font != null)
				CopyCollection(font);
			fc = null;
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
		}

		#region font list
		private void listfonts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (listfonts.SelectedIndex == -1)
				return;

			FontEditor existing = OpenEditors.FirstOrDefault(x => x.Font == Fonts[listfonts.SelectedIndex]);
			if (existing != null)
			{
				existing.Focus();
				return;
			}
			
			FontEditor editor = new FontEditor(Fonts[listfonts.SelectedIndex]);
			OpenEditors.Add(editor);
			editor.Closing += Editor_Closing;
			editor.Show();
		}

		private void Editor_Closing(object sender, CancelEventArgs e)
		{
			OpenEditors.Remove((FontEditor)sender);
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			RemoveSelectedFont();	
		}

		private void RemoveSelectedFont()
		{
			if (listfonts.SelectedItem == null)
				return;

			BlamFont f = (BlamFont)listfonts.SelectedItem;
			var res = MessageBox.Show("This will remove " + f.Name + " from the current collection and cannot be undone. Continue?", "Confirm Remove", MessageBoxButton.OKCancel);

			if (res != MessageBoxResult.OK)
				return;

			for (int i = 0; i < EngineOrdering.Count; i++)
			{
				if (EngineOrdering[i].Font == f)
					EngineOrdering[i].Font = null;
			}

			FontEditor editor = OpenEditors.FirstOrDefault(x => x.Font == f);
			if (editor != null)
			{
				editor.Closing -= Editor_Closing;
				editor.Close();
				OpenEditors.Remove(editor);
			}

			Fonts.Remove(f);
			RefreshFontList();
			RefreshOrderList();
		}

		private void listfonts_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (Keyboard.IsKeyDown(Key.Delete))
			{
				RemoveSelectedFont();
			}
		}
		#endregion

		#region font list drag n drop
		private void listfonts_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				if (sender is ListBoxItem)
				{
					ListBoxItem item = (ListBoxItem)sender;
					DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
					item.IsSelected = true;
				}
			}
		}

		private void listfonts_Drop(object sender, DragEventArgs e)
		{
			BlamFont dropped = (BlamFont)e.Data.GetData(typeof(BlamFont));
			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			int target = -1;

			if (isdropping_reorder)
			{
				isdropping_reorder = false;
				e.Handled = true;
				return;
			}
			target = listfonts.Items.Count - 1;
			
			int orig = listfonts.Items.IndexOf(dropped);

			Fonts.Move(orig, target);

			RefreshFontList();
		}

		private void listfont_Drop(object sender, DragEventArgs e)
		{
			BlamFont dropped = (BlamFont)e.Data.GetData(typeof(BlamFont));
			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			int target = -1;

			BlamFont send = (BlamFont)((ListBoxItem)sender).DataContext;
			target = listfonts.Items.IndexOf(send);
			isdropping_reorder = true;

			int orig = listfonts.Items.IndexOf(dropped);

			Fonts.Move(orig, target);

			RefreshFontList();
		}

		private void listfonts_PreviewDrag(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(typeof(BlamFont)) && !e.Data.GetDataPresent(typeof(List<BlamCharacter>)))
				return;

			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}
		#endregion

		#region order list
		private void listengineorders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (listengineorders.SelectedIndex == -1)
				return;
			EngineOrdering[listengineorders.SelectedIndex].Font = null;
			RefreshOrderList();
		}

		private void listengineorders_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				BlamFont sel = (BlamFont)((ListBoxItem)sender).DataContext;

				if (sel != null)
				{
					int ff = Fonts.IndexOf(sel);
					listfonts.SelectedIndex = ff;
					
				}
				else
					listfonts.UnselectAll();

			}
		}

		#endregion

		#region order list drag n drop
		private void listengineorders_Drop(object sender, DragEventArgs e)
		{
			BlamFont dropped = ((BlamFont)e.Data.GetData(typeof(BlamFont)));
			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			int target = EngineOrdering.IndexOf((EngineOrderItem)((ListBoxItem)sender).DataContext);

			EngineOrdering[target].Font = dropped;

			RefreshOrderList();
		}

		#endregion

	}
}

