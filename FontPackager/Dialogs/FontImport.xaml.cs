using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FontPackager.Classes;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for FontImport.xaml
	/// </summary>
	public partial class FontImport : Window
	{
		List<BlamFont> Fonts { get; set; }

		public List<BlamFont> SelectedFonts { get; set; }

		public FontImport(List<BlamFont> fonts, string file)
		{
			InitializeComponent();
			Fonts = fonts;
			listfonts.ItemsSource = Fonts;
			listfonts.SelectAll();

			importtext.Text = "Select the fonts you want to import from \"" + file + "\".";
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			SelectedFonts = new List<BlamFont>();
			foreach (BlamFont f in listfonts.SelectedItems)
				SelectedFonts.Add(f);

			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void listfonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			btnImport.IsEnabled = (listfonts.SelectedItems.Count > 0);
		}

		private void listfonts_MouseDown(object sender, MouseButtonEventArgs e)
		{
			listfonts.UnselectAll();
		}
	}
}