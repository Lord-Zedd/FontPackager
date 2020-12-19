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
		public FormatInformation Game { get; set; }

		public FontTablePickGame()
		{
			InitializeComponent();
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			switch((string)((Button)sender).Tag)
			{
				default:
					Game = null;
					DialogResult = false;
					break;
				case "h2x":
					Game = FormatInformation.H2X;
					break;
				case "h2v":
					Game = FormatInformation.H2V;
					break;
				case "h3b":
					Game = FormatInformation.H3B;
					break;
				case "h2mcc":
					Game = FormatInformation.H2MCC;
					break;
			}

			DialogResult = true;
			Close();
		}
	}
}
