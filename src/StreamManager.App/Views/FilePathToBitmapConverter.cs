using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace StreamManager.App.Views;

// Binding-time converter for the §6.8 thumbnail preview. Takes an absolute
// file path (string) and returns a Bitmap for Image.Source. Returns null on
// any failure — the VM's ShowUnreachablePlaceholder flag drives the
// fallback card in that case, so a quiet null here is the right behavior.
public sealed class FilePathToBitmapConverter : IValueConverter
{
    public static readonly FilePathToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return null;
        }
        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
