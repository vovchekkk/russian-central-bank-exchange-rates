using System.Globalization;
using Core.Domain.Models;

namespace Core.Infrastructure.Helpers;

public static class FormattingExtensions
{
    private static readonly CultureInfo RuCulture = new("ru-RU");

    public static string FormatReportDatesLine(this DailyExchangeRatesReport report)
        => report.RequestedDate == report.CurrentDate
            ? $"Дата курса ЦБ РФ: {report.CurrentDate:dd.MM.yyyy} (в сравнении с {report.PreviousDate:dd.MM.yyyy})"
            : $"На дату {report.RequestedDate:dd.MM.yyyy} действует курс от {report.CurrentDate:dd.MM.yyyy} (в сравнении с {report.PreviousDate:dd.MM.yyyy})";

    public static string FormatPdfFileName(this DailyExchangeRatesReport report)
        => $"cbr-rates-{report.RequestedDate:yyyy-MM-dd}.pdf";

    public static string FormatAveragePercentChange(this DailyExchangeRatesReport report)
        => $"Среднее изменение по всем валютам: {report.AveragePercentChange.FormatSigned("%", "Н/Д")}";

    public static string FormatSummaryLine(this CurrencyRateDynamics item)
        =>
            $"• {item.Currency.CharCode} ({item.Currency.Name}): {item.PercentChange.FormatSigned("%")} ({item.AbsoluteChange.FormatSigned(" руб.")})";

    public static string FormatWeeklyChartTitle(this DailyExchangeRatesReport report)
    {
        var currencies = report.WeeklyTrends.Count > 0
            ? report.WeeklyTrends.Select(t => t.Currency.CharCode)
            : DailyExchangeRatesReport.DefaultTrendCurrencies.Select(c => c.CharCode);

        var codes = string.Join(" и ", currencies);
        var daysCount = report.WeeklyTrends.Max(t => (int?)t.Points.Count)
                        ?? DailyExchangeRatesReport.DefaultTrendDaysCount;

        return $"Динамика курса {codes} за последние {daysCount} дн. (руб.)";
    }

    public static string? FormatWeeklyChartNote(this DailyExchangeRatesReport report)
    {
        if (report.WeeklyTrends.Count == 0)
        {
            var expectedCodes = string.Join(" и ", DailyExchangeRatesReport.DefaultTrendCurrencies.Select(c => c.CharCode));
            return $"Данные по валютам {expectedCodes} за выбранный период отсутствуют в базе ЦБ РФ — график не построен.";
        }

        var notes = new List<string>();

        var missingCurrencies = DailyExchangeRatesReport.DefaultTrendCurrencies
            .Where(expected => report.WeeklyTrends.All(t => !t.Currency.Equals(expected)))
            .Select(c => $"{c.CharCode} ({c.Name})")
            .ToList();

        if (missingCurrencies.Count > 0)
        {
            notes.Add($"котировки {string.Join(", ", missingCurrencies)} за выбранный период отсутствуют в базе ЦБ РФ");
        }

        var totalDays = report.WeeklyTrends
            .SelectMany(t => t.Points)
            .Select(p => p.Date)
            .Distinct()
            .Count();

        foreach (var trend in report.WeeklyTrends)
        {
            if (trend.Points.Count < totalDays && trend.Points.Count > 0)
            {
                var firstDate = trend.Points[0].Date;
                var lastDate = trend.Points[^1].Date;
                notes.Add(
                    $"котировки {trend.Currency.CharCode} ({trend.Currency.Name}) представлены частично — за {trend.Points.Count} из {totalDays} дн. (с {firstDate:dd.MM.yyyy} по {lastDate:dd.MM.yyyy})");
            }
        }

        return notes.Count > 0
            ? $"Примечание: {string.Join("; ", notes)}."
            : null;
    }

    public static string FormatChartLegend(this Currency currency)
        => $"{currency.CharCode} ({currency.Name})";

    public static string FormatSigned(this decimal value, string suffix = "")
    {
        var sign = value > 0 ? "+" : "";
        return $"{sign}{value.ToString("N4", RuCulture)}{suffix}";
    }

    public static string FormatSigned(this decimal? value, string suffix = "", string nullPlaceholder = "-")
        => value.HasValue ? value.Value.FormatSigned(suffix) : nullPlaceholder;

    public static string FormatRate(this decimal value, string suffix = "")
        => $"{value.ToString("N4", RuCulture)}{suffix}";

    public static string FormatShortRate(this decimal value, string suffix = "")
        => $"{value.ToString("N2", RuCulture)}{suffix}";

    public static string FormatRate(this decimal? value, string suffix = "", string nullPlaceholder = "-")
        => value.HasValue ? value.Value.FormatRate(suffix) : nullPlaceholder;
}