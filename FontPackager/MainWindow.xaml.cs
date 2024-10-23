using System.Collections.Generic;
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
using Microsoft.Win32;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public ObservableCollection<BlamFont> Fonts { get; set; }
		public ObservableCollection<EngineOrderItem> EngineOrdering { get; set; }

		public FormatInformation TargetFormat { get { return (FormatInformation)((ComboBoxItem)cmbFmt.SelectedItem).Tag; } }

		private string LastFilePath = "";

		private List<Window> ChildWindows;

		bool isdropping_reorder = false;
		FontCreator fc = null;
		
		public MainWindow()
		{
			InitializeComponent();
			ChildWindows = new List<Window>();
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
		
		public bool VerifyFonts(FormatInformation info)
		{
			string result = "";
			bool canIgnore = true;
			using (StringWriter sw = new StringWriter())
			{
				foreach (BlamFont font in Fonts)
				{
					var results = font.Verify(info);
					if (results.Count > 0)
					{
						sw.WriteLine("~" + font.Name);
						foreach (VerificationResult res in results)
						{
							sw.WriteLine(res.Message);
							if (res.IsCritical)
								canIgnore = false;
						}
							
						sw.WriteLine();
					}
				}

				result = sw.ToString();
			}

			if (string.IsNullOrEmpty(result))
				return true;

			ListDialog ve = new ListDialog(result, canIgnore);
			ve.ShowDialog();

			return ve.IgnoreErrors;
		}

		private void CloseChildWindows()
		{
			foreach (Window e in ChildWindows)
			{
				e.Closing -= Child_Closing;
				e.Close();
			}

			ChildWindows.Clear();
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
		private void FinishLoading(FormatInformation info, List<BlamFont> fonts, List<int> orders)
		{
			CloseChildWindows();
			ClearLists();

			if (info == FormatInformation.H2X)
				cmbFmt.SelectedIndex = 0;
			else if (info == FormatInformation.H2V)
				cmbFmt.SelectedIndex = 1;
			else if (info == FormatInformation.H3B)
				cmbFmt.SelectedIndex = 2;
			else if (info == FormatInformation.H2MCC)
				cmbFmt.SelectedIndex = 3;
			//4 is the fallback
			else if (info == FormatInformation.H4B)
				cmbFmt.SelectedIndex = 5;
			else if (info == FormatInformation.H4)
				cmbFmt.SelectedIndex = 6;
			else if (info == FormatInformation.GenericMCC)
				cmbFmt.SelectedIndex = 7;
			else if (info == FormatInformation.H4MCC)
				cmbFmt.SelectedIndex = 8;
			else if (info == FormatInformation.H2AMCC)
				cmbFmt.SelectedIndex = 9;
			else
				cmbFmt.SelectedIndex = 4;

			CopyCollection(fonts);
			CopyOrders(orders);
			
			fname.Text = Path.GetFileName(LastFilePath);
			fname.ToolTip = LastFilePath;
			
			MessageBox.Show("\"" + Path.GetFileName(LastFilePath) + "\" has been loaded successfully with " + Fonts.Count + " fonts.");
			
			menuSaveAs.IsEnabled = true;
			menuTools.IsEnabled = true;
		}

		private static Tuple<string, FormatInformation, List<BlamFont>, List<int>> OpenAndLoadPackage()
		{
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Font Package",
				Filter = "Font Package (*.bin)|*.bin"
			};
			if (!(bool)ofd.ShowDialog())
				return null;

			return LoadPackageFromPath(ofd.FileName);
		}

		private static Tuple<string, FormatInformation, List<BlamFont>, List<int>> LoadPackageFromPath(string path)
		{
			string fullPath = path;
			string filename = Path.GetFileName(fullPath);
			var res = PackageIO.Read(fullPath);

			switch (res.Item1)
			{
				case IOError.None:
					return new Tuple<string, FormatInformation, List<BlamFont>, List<int>>
						(fullPath, res.Item2, res.Item3, res.Item4);
				case IOError.BadVersion:
					MessageBox.Show("Package \"" + filename + "\" has an invalid header version value and was not loaded.");
					return null;
				case IOError.UnknownBlock:
					MessageBox.Show("Cannot determine Block Size for Package \"" + filename + "\" and was not loaded.");
					return null;
				case IOError.Empty:
					MessageBox.Show("Package \"" + filename + "\" has a font count of 0 and was not loaded.");
					return null;
				default:
					MessageBox.Show("An unknown error occurred loading package \"" + filename + "\".");
					return null;
			}
		}

		private void HandlePackageLoad(string path)
		{
			var res = LoadPackageFromPath(path);
			if (res == null)
				return;

			LastFilePath = res.Item1;
			FinishLoading(res.Item2, res.Item3, res.Item4);
		}

		private static Tuple<string, List<BlamFont>, List<int>> OpenAndLoadTable()
		{
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Font Table",
				Filter = "Font Table (*.txt)|*.txt"
			};
			if (!(bool)ofd.ShowDialog())
				return null;

			return LoadTableFromPath(ofd.FileName);
		}

		private static Tuple<string, List<BlamFont>, List<int>> LoadTableFromPath(string path)
		{
			string fullPath = path;
			string filename = Path.GetFileName(fullPath);
			var res = TableIO.ReadTable(fullPath);

			switch (res.Item1)
			{
				case IOError.None:
					return new Tuple<string, List<BlamFont>, List<int>>
						(fullPath, res.Item2, res.Item3);
				case IOError.BadVersion:
					MessageBox.Show("A font within list \"" + filename + "\" had an invalid header version value and loading was cancelled.");
					return null;
				case IOError.Empty:
					MessageBox.Show("List \"" + filename + "\" has no valid fonts.");
					return null;
				default:
					MessageBox.Show("An unknown error occurred loading list \"" + filename + "\".");
					return null;
			}
		}

		private void HandleTableLoad(string path)
		{
			var res = LoadTableFromPath(path);
			if (res == null)
				return;

			FontTablePickGame picker = new FontTablePickGame();
			picker.ShowDialog();

			if (picker.DialogResult == false)
				return;

			FormatInformation info = picker.Game;

			LastFilePath = res.Item1;
			FinishLoading(info, res.Item2, res.Item3);
		}

		private void HandleCollectionFromPath(string path)
		{
			string ext = Path.GetExtension(path);
			if (ext.ToLowerInvariant() == ".bin")
				HandlePackageLoad(path);
			else if (ext.ToLowerInvariant() == ".txt")
				HandleTableLoad(path);
			else
				throw new NotImplementedException();
		}

		private static List<BlamFont> OpenAndImportLooseFonts()
		{
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Font Files",
				Filter = "Single H2 Font (*)|*",
				Multiselect = true
			};
			if (!(bool)ofd.ShowDialog() || ofd.FileNames.Length == 0)
				return null;

			List<BlamFont> fonts = new List<BlamFont>();

			foreach (string fn in ofd.FileNames)
			{
				var res = TableIO.ReadLooseFile(fn);

				switch (res.Item1)
				{
					case IOError.None:
						fonts.Add(res.Item2);
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
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Cache File",
				Filter = "Halo CE Cache File (*.map)|*.map"
			};
			if (!(bool)ofd.ShowDialog())
				return null;

			string filename = ofd.FileName;
			var res = TagIO.ReadCacheFile(ofd.FileName);

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

		private static List<BlamFont> OpenAndImportFontTags()
		{
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Cache File",
				Filter = "Halo CE Cache File (*.font)|*.font",
				Multiselect = true
			};
			if (!(bool)ofd.ShowDialog())
				return null;

			List<BlamFont> fonts = new List<BlamFont>();

			foreach (string fn in ofd.FileNames)
			{
				var res = TagIO.ReadTag(fn);

				switch (res.Item1)
				{
					case IOError.None:
						fonts.Add(res.Item2);
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

		private static Tuple<string, List<BlamFont>> OpenAndImportDirectory()
		{
			OpenFolderDialog ofd = new OpenFolderDialog()
			{
				Title = "Select Font Directory",
			};
			var result = ofd.ShowDialog();
			if (!(bool)result)
				return null;

			string folder = Path.GetFileName(ofd.SafeFolderName);
			var res = TableIO.ReadDirectory(ofd.FolderName);

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
		#endregion

		#region saving
		private bool SavePackage(FormatInformation info)
		{
			string defaultname = "font_package";
			if (!string.IsNullOrEmpty(fname.Text) && Path.GetExtension(fname.Text) == ".bin")
				defaultname = Path.GetFileNameWithoutExtension(fname.Text);

			SaveFileDialog sfd = new SaveFileDialog
			{
				RestoreDirectory = true,
				Title = "Save Font Package",
				Filter = "Font Package (*.bin)|*.bin",
				FileName = defaultname
			};
			if (!(bool)sfd.ShowDialog())
				return false;

			if (!VerifyFonts(info))
				return false;

			PackageIO.Write(Fonts.ToList(), CreateOrderList(), sfd.FileName, info);

			LastFilePath = sfd.FileName;

			fname.Text = Path.GetFileName(LastFilePath);
			fname.ToolTip = LastFilePath;

			return true;
		}

		private bool SaveTable(FormatInformation info)
		{
			string defaultname = "font_table";
			if (!string.IsNullOrEmpty(fname.Text) && Path.GetExtension(fname.Text) == ".txt")
				defaultname = Path.GetFileNameWithoutExtension(fname.Text);

			SaveFileDialog sfd = new SaveFileDialog
			{
				RestoreDirectory = true,
				Title = "Save Font File",
				Filter = "Font Table (*.txt)|*.txt",
				FileName = defaultname
			};
			if (!(bool)sfd.ShowDialog())
				return false;

			if (!VerifyFonts(info))
				return false;

			TableIO.WriteTable(Fonts.ToList(), CreateOrderList(), sfd.FileName, info);

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

			CloseChildWindows();
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
			OpenFileDialog ofd = new OpenFileDialog
			{
				RestoreDirectory = true,
				Title = "Open Font Collection",
				Filter = "All Supported Collections (*.bin,*.txt)|*.bin;*.txt;|Font Packages (*.bin)|*.bin;|Font Tables (*.txt)|*.txt;"
				//Filter = "Font Packages (*.bin)|*.bin;|Font Tables (*.txt)|*.txt;"
			};
			if (!(bool)ofd.ShowDialog())
				return;

			switch (ofd.FilterIndex)
			{
				case 1:
					{
						HandleCollectionFromPath(ofd.FileName);
						break;
					}
				case 2:
					{
						HandlePackageLoad(ofd.FileName);
						break;
					}
				case 3:
					{
						HandleTableLoad(ofd.FileName);
						break;

					}
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

			if (TargetFormat.Format == FileFormat.Table)
			{
				if (Fonts.Count > TargetFormat.MaximumFontCount)
				{
					MessageBox.Show("The table format only supports up to 12 fonts. Remove some to save as this format.");
					return;
				}
				success = SaveTable(TargetFormat);
			}
				
			else if (TargetFormat.Format == FileFormat.Package)
			{
				if (Fonts.Count > TargetFormat.MaximumFontCount)
				{
					MessageBox.Show("The chosen package format only supports up to " + TargetFormat.MaximumFontCount + " fonts. Remove some to save as this format.");
					return;
				}
				success = SavePackage(TargetFormat);
			}

			if (success)
				MessageBox.Show("\"" + Path.GetFileName(LastFilePath) + "\" has been saved successfully.");
		}

		private void btnImport_Click(object sender, RoutedEventArgs e)
		{
			List<BlamFont> fonts;
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
						fonts = res.Item2;
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
				case "tag":
					{
						var res = OpenAndImportFontTags();
						if (res == null)
							return;

						skipdialog = true;
						fonts = res;
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
					return;
				
				CopyCollection(importer.SelectedFonts);

				MessageBox.Show("Successfully imported " + importer.SelectedFonts.Count + " fonts from \"" + shortname + "\".");
			}
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

		private void Help_Click(object sender, RoutedEventArgs e)
		{
			FontHelp help = new FontHelp();
			help.ShowDialog();
		}
		#endregion

		private void window_Closing(object sender, CancelEventArgs e)
		{
			CloseChildWindows();
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

			FontEditor existing = ChildWindows.OfType<FontEditor>().FirstOrDefault(x => x.Font == Fonts[listfonts.SelectedIndex]);
			if (existing != null)
			{
				existing.Focus();
				return;
			}
			
			FontEditor editor = new FontEditor(Fonts[listfonts.SelectedIndex]);
			ChildWindows.Add(editor);
			editor.Closing += Child_Closing;
			editor.Show();
		}

		private void Child_Closing(object sender, CancelEventArgs e)
		{
			ChildWindows.Remove((Window)sender);
		}

		private void RemoveFont_Click(object sender, RoutedEventArgs e)
		{
			RemoveSelectedFont();	
		}

		private void PrintFont_Click(object sender, RoutedEventArgs e)
		{
			if (listfonts.SelectedIndex == -1)
				return;

			FontPrinter existing = ChildWindows.OfType<FontPrinter>().FirstOrDefault(x => x.Font == Fonts[listfonts.SelectedIndex]);
			if (existing != null)
			{
				existing.Focus();
				return;
			}

			FontPrinter printer = new FontPrinter(Fonts[listfonts.SelectedIndex]);
			ChildWindows.Add(printer);
			printer.Closing += Child_Closing;
			printer.Show();
		}

		private void RemoveSelectedFont()
		{
			if (listfonts.SelectedItem == null)
				return;

			BlamFont f = (BlamFont)listfonts.SelectedItem;

			FontEditor editor = ChildWindows.OfType<FontEditor>().FirstOrDefault(x => x.Font == f);
			FontPrinter printer = ChildWindows.OfType<FontPrinter>().FirstOrDefault(x => x.Font == f);

			string windowsopen = "";
			if (editor != null || printer != null)
				windowsopen = " There is currently an editor and/or printer window open for this font, these windows will be closed.";

			var res = MessageBox.Show("This will remove " + f.Name + " from the current collection and cannot be undone." + windowsopen + " Continue?", "Confirm Remove", MessageBoxButton.OKCancel);

			if (res != MessageBoxResult.OK)
				return;

			for (int i = 0; i < EngineOrdering.Count; i++)
			{
				if (EngineOrdering[i].Font == f)
					EngineOrdering[i].Font = null;
			}

			if (editor != null)
			{
				editor.Closing -= Child_Closing;
				editor.Close();
				ChildWindows.Remove(editor);
			}

			if (printer != null)
			{
				printer.Closing -= Child_Closing;
				printer.Close();
				ChildWindows.Remove(printer);
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
				if (sender is ListBoxItem item)
				{
					try
					{
						DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
						item.IsSelected = true;
					}
					catch (Exception)
					{
						MessageBox.Show("Error", "Cannot drag fonts between separate instances of FontPackager. Use the import option in the Tools menu.");
					}
				}
			}
		}

		private void listfonts_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				HandleFileDrop(sender, e);
				return;
			}

			BlamFont dropped;
			try
			{
				dropped = (BlamFont)e.Data.GetData(typeof(BlamFont));
			}
			catch (Exception)
			{
				MessageBox.Show("Error", "Cannot drag fonts between separate instances of FontPackager. Use the import option in the Tools menu.");
				return;
			}

			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			if (isdropping_reorder)
			{
				isdropping_reorder = false;
				e.Handled = true;
				return;
			}
			int target = listfonts.Items.Count - 1;
			
			int orig = listfonts.Items.IndexOf(dropped);

			Fonts.Move(orig, target);

			RefreshFontList();
		}

		private void listfont_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				HandleFileDrop(sender, e);
				return;
			}

			BlamFont dropped = (BlamFont)e.Data.GetData(typeof(BlamFont));
			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			BlamFont send = (BlamFont)((ListBoxItem)sender).DataContext;
			int target = listfonts.Items.IndexOf(send);
			isdropping_reorder = true;

			int orig = listfonts.Items.IndexOf(dropped);

			Fonts.Move(orig, target);

			RefreshFontList();
		}

		private void listfonts_PreviewDrag(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				return;
			else if (e.Data.GetDataPresent(typeof(BlamFont)) && !e.Data.GetDataPresent(typeof(List<BlamCharacter>)))
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
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				HandleFileDrop(sender, e);
				return;
			}
			
			BlamFont dropped = ((BlamFont)e.Data.GetData(typeof(BlamFont)));
			if (e.Data.GetDataPresent(typeof(List<BlamCharacter>)) || dropped == null)
				return;

			int target = EngineOrdering.IndexOf((EngineOrderItem)((ListBoxItem)sender).DataContext);

			EngineOrdering[target].Font = dropped;

			RefreshOrderList();
		}
		#endregion

		#region general drag and drop
		private void window_PreviewDrag(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
				return;

			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void window_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				HandleFileDrop(sender, e);
			}
		}

		private void HandleFileDrop(object sender, DragEventArgs e)
		{
			string[] draggedFiles = (string[])e.Data.GetData(DataFormats.FileDrop, true);
			if (draggedFiles.Length > 0)
			{
				if (draggedFiles.Length > 1)
				{
					MessageBox.Show("Please only drag one file.");
					return;
				}

				HandleCollectionFromPath(draggedFiles[0]);
				e.Handled |= true;
			}
		}
		#endregion
	}
}

