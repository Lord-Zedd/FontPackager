using System.Windows;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for ListDialog.xaml
	/// </summary>
	public partial class ListDialog : Window
	{
		public bool IgnoreErrors { get; set; }

		public ListDialog(string results, bool showIgnore)
		{
			InitializeComponent();
			Title = "Verification Errors Found";
			msgtxt.Text = "The current action failed verification and could not be completed. Depending on the error you may be able to ignore it, but may not display correctly ingame. Details below:";
			resulttxt.Text = results;
			IgnoreErrors = false;
			if (showIgnore) ignorebtn.Visibility = Visibility.Visible;
		}

		public ListDialog(string title, string message, string results, bool showIgnore)
		{
			InitializeComponent();
			Title = title;
			msgtxt.Text = message;
			resulttxt.Text = results;
			IgnoreErrors = false;
			if (showIgnore) ignorebtn.Visibility = Visibility.Visible;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			IgnoreErrors = true;
			Close();
		}
	}
}
