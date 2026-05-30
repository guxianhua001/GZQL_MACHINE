using System.Globalization;
using System.Windows.Data;
using System;

public class BooleanToSingleStepTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "Completed";
    public string FalseText { get; set; } = "Pending";
    public string NullText { get; set; } = "Not Started";

    // For different step states
    public string InProgressText { get; set; } = "In Progress";
    public string SkippedText { get; set; } = "Skipped";

    public bool UseIcons { get; set; }
    public string TrueIcon { get; set; } = "✓";
    public string FalseIcon { get; set; } = "⋯";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string baseText;

        switch (value)
        {
            case bool boolValue:
                baseText = boolValue ? TrueText : FalseText;
                break;
            case null:
                baseText = NullText;
                break;
            default:
                baseText = FalseText;
                break;
        }

        // Handle special states via parameter
        if (parameter is string state)
        {
            baseText = state.ToLower() switch
            {
                "inprogress" or "in_progress" => InProgressText,
                "skipped" => SkippedText,
                "completed" => TrueText,
                "pending" => FalseText,
                _ => baseText
            };
        }

        if (UseIcons)
        {
            string icon = value switch
            {
                bool b when b => TrueIcon,
                bool b when !b => FalseIcon,
                _ => FalseIcon
            };

            return $"{icon} {baseText}";
        }

        return baseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue.StartsWith(TrueIcon) ||
                   stringValue.Contains(TrueText) ||
                   stringValue.Equals("Completed", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}