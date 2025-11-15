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
            // Ha kollekció: true ha van benne legalább egy elem
            if (value is IEnumerable enumerable)
                return enumerable.Cast<object>().Any();

            // Ha szám (pl. Count)
            if (value is int count)
                return count > 0;

            // Egyéb esetben: csak akkor true, ha nem null és igaz értékű
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
