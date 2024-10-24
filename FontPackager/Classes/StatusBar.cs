using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FontPackager.Classes
{
	public class StatusBar : INotifyPropertyChanged
	{
		string _status;
		public string StatusText
		{
			get { return _status; }
			set { _status = value; NotifyPropertyChanged("StatusText"); }
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public StatusBar()
		{
			_status = "Initialized.";
		}
	}
}
