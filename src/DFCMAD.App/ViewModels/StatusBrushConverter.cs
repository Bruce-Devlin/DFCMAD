using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DFCMAD.Core.Models;
using MediaColor = System.Windows.Media.Color;

namespace DFCMAD.App.ViewModels;

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            EnforcementStatus.Enforcing => new SolidColorBrush(MediaColor.FromRgb(34, 197, 94)),
            EnforcementStatus.Paused => new SolidColorBrush(MediaColor.FromRgb(251, 191, 36)),
            EnforcementStatus.WaitingForDevice => new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)),
            EnforcementStatus.Error => new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)),
            _ => new SolidColorBrush(MediaColor.FromRgb(148, 163, 184))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
