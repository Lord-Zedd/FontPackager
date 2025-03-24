using FontPackager.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for UnicodeInput.xaml
	/// </summary>
	public partial class UnicodeInput : Window
	{
		private readonly BlamFont _font;
		public ushort Unicode { get; set; }

		public UnicodeInput(BlamFont font)
		{
			InitializeComponent();
			_font = font;
			desc.Text = "Enter the unicode index (ex: E100) you would like to add to " + _font.Name + ". If it is already in use it will be replaced.";
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

		private void Suggest_Click(object sender, RoutedEventArgs e)
		{
			HashSet<ushort> unicodes = new HashSet<ushort>(_font.Characters.Select(x => x.UnicIndex));
			ushort next = 0xE000;
			ushort max = 0xFFFF;

			while (unicodes.Contains(next) && next < max)
				next++;

			if (next == max)
			{
				MessageBox.Show("You managed to fill every unicode? Manually try something lower than E000 I guess.");
				return;
			}

			unicbox.Text = next.ToString("X4");
		}

	}
}
