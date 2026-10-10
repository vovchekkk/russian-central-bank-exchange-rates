using System.Globalization;
using Core.Domain.Models;

namespace Core.Infrastructure.Helpers;

public static class FormattingExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public static string FormatReportDatesLine(this DailyExchangeRatesReport report)
        => $"Дата отчёта: {report.CurrentDate:dd.MM.yyyy} (в сравнении с {report.PreviousDate:dd.MM.yyyy})";

    public static string FormatAveragePercentChange(this DailyExchangeRatesReport report)
        => $"Среднее изменение по всем валютам: {report.AveragePercentChange.FormatSigned("%", "Н/Д")}";

    public static string FormatSummaryLine(this CurrencyRateDynamics item)
        =>
            $"• {item.CharCode} ({item.Name}): {item.PercentChange.FormatSigned("%")} ({item.AbsoluteChange.FormatSigned(" руб.")})";

    public static string FormatSigned(this decimal value, string suffix = "")
    {
        var sign = value > 0 ? "+" : "";
        return $"{sign}{value.ToString("N4", RuCulture)}{suffix}";
    }

    public static string FormatSigned(this decimal? value, string suffix = "", string nullPlaceholder = "-")
        => value.HasValue ? value.Value.FormatSigned(suffix) : nullPlaceholder;

    public static string FormatRate(this decimal value, string suffix = "")
        => $"{value.ToString("N4", RuCulture)}{suffix}";

    public static string FormatRate(this decimal? value, string suffix = "", string nullPlaceholder = "-")
        => value.HasValue ? value.Value.FormatRate(suffix) : nullPlaceholder;
}