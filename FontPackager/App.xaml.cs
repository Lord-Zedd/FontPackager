using FontPackager.Classes;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FontPackager
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
	}

	// Inverts the x origin value to display accurately
	public class XOriginInverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return -((short)value / DPIHelper.DPIScale);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	// Math so that characters and their values display in the UI as 1:1, not scaled by DPI
	public class ScaleConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			int v = System.Convert.ToInt32(value);
			return (v / DPIHelper.DPIScale);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class NullOrderFontConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			EngineOrderItem v = (EngineOrderItem)value;
			if (v.Font == null)
				return true;
			else
				return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
