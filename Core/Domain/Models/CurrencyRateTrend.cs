using ErrorOr;

namespace Core.Domain.Models;

public sealed record CurrencyRateTrend
{
    public Currency Currency { get; }
    public IReadOnlyList<CurrencyRatePoint> Points { get; }

    private CurrencyRateTrend(Currency currency, IReadOnlyList<CurrencyRatePoint> points)
    {
        Currency = currency;
        Points = points;
    }

    internal static ErrorOr<CurrencyRateTrend> Create(
        Currency? currency,
        DateOnly endDate,
        IEnumerable<ExchangeRateReport>? historicalReports,
        int daysCount = DailyExchangeRatesReport.DefaultTrendDaysCount)
    {
        if (currency is null)
            return Error.Validation(
                description: "Отсутствует информация о валюте для построения динамики курса.");

        var dateValidation = DailyExchangeRatesReport.ValidateRequestedDate(endDate);
        if (dateValidation.IsError)
            return dateValidation.Errors;

        if (daysCount <= 0)
            return Error.Validation(
                description: "Количество дней для построения динамики курса должно быть больше нуля.");

        if (historicalReports is null)
            return Error.Validation(
                description: "Отсутствуют исторические срезы курсов валют для построения графика.");

        var reportsList = historicalReports.ToList().AsReadOnly();
        if (reportsList.Count == 0)
            return Error.Validation(
                description: "Список исторических срезов курсов валют не может быть пустым.");

        if (reportsList[0].Date > endDate)
            return Error.Validation(
                description: $"Дата актуального курса ({reportsList[0].Date:dd.MM.yyyy}) не может быть позже конечной даты графика ({endDate:dd.MM.yyyy}).");

        for (var i = 1; i < reportsList.Count; i++)
        {
            if (reportsList[i - 1].Date <= reportsList[i].Date)
                return Error.Validation(
                    description: $"Нарушена хронология срезов курсов валют: {reportsList[i - 1].Date:dd.MM.yyyy} и {reportsList[i].Date:dd.MM.yyyy}.");
        }

        var startDate = endDate.AddDays(-(daysCount - 1));
        if (startDate < ExchangeRateReport.MinCbrDate)
            startDate = ExchangeRateReport.MinCbrDate;

        var points = new List<CurrencyRatePoint>(daysCount);
        ExchangeRateReport? lastEffectiveReport = null;
        CurrencyRate? lastCurrencyRate = null;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var effectiveReport = reportsList.FirstOrDefault(r => r.Date <= date);
            if (effectiveReport is null)
                continue;

            if (!ReferenceEquals(effectiveReport, lastEffectiveReport))
            {
                lastEffectiveReport = effectiveReport;
                lastCurrencyRate = effectiveReport.Rates.FirstOrDefault(r => r.Currency.Equals(currency));
            }

            if (lastCurrencyRate is null)
                continue;

            var pointResult = CurrencyRatePoint.Create(date, lastCurrencyRate.UnitRate);
            if (pointResult.IsError)
                return pointResult.Errors;

            points.Add(pointResult.Value);
        }

        if (points.Count == 0)
            return Error.NotFound(
                description: $"Валюта '{currency.CharCode}' не найдена в срезах курсов ЦБ РФ.");

        return new CurrencyRateTrend(currency, points.AsReadOnly());
    }

    public bool Equals(CurrencyRateTrend? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        return Currency.Equals(other.Currency)
               && Points.SequenceEqual(other.Points);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Currency);
        foreach (var point in Points)
            hash.Add(point);
        return hash.ToHashCode();
    }
}