using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace H2MLauncher.UI.Converters;

public partial class HostNameColorConverter : IValueConverter
{
    private static readonly FontFamily Font = new("Consolas");
    private static readonly SolidColorBrush LighterBlueBrush = new(Color.FromRgb(37, 62, 235));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hostname)
        {
            return Array.Empty<Run>();
        }

        Style? runStyle = parameter as Style;

        List<Run> runs = [];
        MatchCollection matches = ColorRegex().Matches(hostname);
        if (matches.Count != 0)
        {
            if (matches[0].Index != 0)
            {
                runs.Add(new Run()
                {
                    Text = hostname[..matches[0].Index],
                    Foreground = Brushes.White,
                    Style = runStyle
                });
            }
            foreach (Match match in matches)
            {
                string text = match.Groups[1].Value;
                Brush brush = text switch
                {
                    "^0" => Brushes.DimGray,
                    "^1" => Brushes.Red,
                    "^2" => Brushes.Green,
                    "^3" => Brushes.Yellow,
                    "^4" => LighterBlueBrush,
                    "^5" => Brushes.Cyan,
                    "^6" => Brushes.Magenta,
                    "^7" => Brushes.White,
                    "^8" => Brushes.DimGray,
                    _ => Brushes.White, // ^: rainbow
                };
                runs.Add(new Run()
                {
                    Text = match.Groups[2].Value,
                    Foreground = brush,
                    Style = runStyle
                });
            }
        }
        else
        {
            runs.Add(new Run()
            {
                Text = hostname,
                Foreground = Brushes.White,
                Style = runStyle
            });
        }
        return runs.ToArray();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    [GeneratedRegex(@"(\^\d|\^\:)([^\^]*?)(?=\^\d|\^:|$)")]
    private static partial Regex ColorRegex();
}
