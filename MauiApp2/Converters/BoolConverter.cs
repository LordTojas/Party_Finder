using System;
using System.Collections;
using System.Globalization;
using System.Linq;

namespace MauiApp2.Converters
{
    public class BoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            if (value is IEnumerable enumerable)
                return enumerable.Cast<object>().Any();

            
            if (value is int count)
                return count > 0;

            
            if (value is bool b)
                return b;

            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
