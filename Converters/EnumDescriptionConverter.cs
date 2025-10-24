using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace FISApiClient.Converters
{
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {if (value == null) return null;
            var type = value.GetType();
            if (!type.IsEnum) throw new InvalidOperationException("Only enum types are supported.");

            var memberInfo = type.GetMember(value.ToString());
            if (memberInfo.Length > 0)
            {
                var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attrs.Length > 0)
                {
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}