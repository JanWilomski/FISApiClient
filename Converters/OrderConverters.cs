using System;
using System.Globalization;
using System.Windows.Data;

namespace FISApiClient.Converters
{
    /// <summary>
    /// Konwertuje kod Side (0/1) na czytelny tekst (Buy/Sell)
    /// </summary>
    public class SideConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "N/A";
            
            string side = value.ToString() ?? "";
            return side switch
            {
                "0" => "BUY",
                "1" => "SELL",
                _ => side
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwertuje kod Modality na czytelny tekst
    /// </summary>
    public class ModalityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "N/A";
            
            string modality = value.ToString() ?? "";
            return modality switch
            {
                "L" => "Limit",
                "M" => "Market",
                "S" => "Stop",
                "P" => "Pegged",
                _ => modality
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Konwertuje kod Validity na czytelny tekst
    /// </summary>
    public class ValidityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "N/A";
            
            string validity = value.ToString() ?? "";
            return validity switch
            {
                "J" => "Day",
                "K" => "FOK",
                "I" => "IOC",
                "G" => "GTC",
                _ => validity
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
