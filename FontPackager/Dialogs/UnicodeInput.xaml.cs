using System.Windows;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for UnicodeInput.xaml
	/// </summary>
	public partial class UnicodeInput : Window
	{
		public ushort Unicode { get; set; }

		public UnicodeInput(string fontname)
		{
			InitializeComponent();
			desc.Text = "Enter the unicode index (ex: E100) you would like to add to " + fontname + ". If it is already in use it will be replaced.";
			unicbox.Focus();
		}

		private void Import_Click(object sender, RoutedEventArgs e)
		{
			bool parsed = ushort.TryParse(unicbox.Text, System.Globalization.NumberStyles.HexNumber, null, out ushort unic);

			if (!parsed || unic == 0xFFFF)
			{
				MessageBox.Show("Please enter a valid hexidecimal unicode index 0000-FFFE");
				return;
			}

			Unicode = unic;

			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
