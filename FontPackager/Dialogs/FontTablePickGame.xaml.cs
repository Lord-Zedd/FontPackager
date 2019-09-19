using System.Windows;
using System.Windows.Controls;
using FontPackager.Classes;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for FontTablePickGame.xaml
	/// </summary>
	public partial class FontTablePickGame : Window
	{
		public FileFormat Game { get; set; }

		public FontTablePickGame()
		{
			InitializeComponent();
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			switch((string)((Button)sender).Tag)
			{
				default:
					Game = 0;
					DialogResult = false;
					break;
				case "h2x":
					Game = FileFormat.H2X;
					break;
				case "h2v":
					Game = FileFormat.H2V;
					break;
				case "h3b":
					Game = FileFormat.H3B;
					break;
			}

			DialogResult = true;
			Close();
		}
	}
}
