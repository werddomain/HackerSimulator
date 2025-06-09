using System.Globalization;

namespace ProxyServer.GUI.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string colors)
            {
                var colorOptions = colors.Split(',');
                if (colorOptions.Length >= 2)
                {
                    var colorName = boolValue ? colorOptions[0] : colorOptions[1];
                    return Color.FromArgb(colorName);
                }
            }
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string options)
            {
                var textOptions = options.Split(',');
                if (textOptions.Length >= 2)
                {
                    return boolValue ? textOptions[0] : textOptions[1];
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string logLevel)
            {
                return logLevel.ToUpper() switch
                {
                    "ERROR" => Colors.Red,
                    "WARNING" => Colors.Orange,
                    "INFO" => Colors.Blue,
                    "DEBUG" => Colors.Gray,
                    "TRACE" => Colors.LightGray,
                    _ => Colors.Black
                };
            }
            return Colors.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue && parameter is string comparison)
            {
                bool invert = comparison.StartsWith("!");
                string actualComparison = invert ? comparison.Substring(1) : comparison;
                
                bool result = int.TryParse(actualComparison, out int compareValue) && intValue == compareValue;
                return invert ? !result : result;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
