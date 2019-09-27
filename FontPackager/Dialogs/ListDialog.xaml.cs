using System.Windows;

namespace FontPackager.Dialogs
{
	/// <summary>
	/// Interaction logic for ListDialog.xaml
	/// </summary>
	public partial class ListDialog : Window
	{
		public ListDialog(string results)
		{
			InitializeComponent();
			Title = "Verification Errors Found";
			msgtxt.Text = "The current action failed verification and could not be completed. Details below:";
			resulttxt.Text = results;
		}

		public ListDialog(string title, string message, string results)
		{
			InitializeComponent();
			Title = title;
			msgtxt.Text = message;
			resulttxt.Text = results;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
