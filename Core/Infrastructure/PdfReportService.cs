using System.Globalization;
using Core.Domain.Models;
using Core.Infrastructure.Helpers;
using Core.Services;
using ErrorOr;
using Microsoft.Extensions.Logging;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScottPlot;
using ScottPlot.TickGenerators;
using Colors = QuestPDF.Helpers.Colors;

namespace Core.Infrastructure;

internal sealed class PdfReportService(ILogger<PdfReportService> logger) : IPdfReportService
{
    public async Task<ErrorOr<byte[]>> GenerateDailyReportAsync(
        DailyExchangeRatesReport report,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            logger.LogInformation("Генерация PDF-отчёта за {Date:dd.MM.yyyy}", report.CurrentDate);

            var pdfBytes = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                return Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Element(c => ComposeHeader(c, report));
                        page.Content().Element(c => ComposeContent(c, report));

                        page.Footer().AlignCenter().Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                            x.Span(" из ");
                            x.TotalPages();
                        });
                    });
                }).GeneratePdf();
            }, cancellationToken);

            logger.LogInformation(
                "PDF-отчёт за {Date:dd.MM.yyyy} успешно создан ({Size} байт)",
                report.CurrentDate,
                pdfBytes.Length
            );

            return pdfBytes;
        }
        catch (DocumentDrawingException ex)
        {
            logger.LogError(ex, "Ошибка отрисовки или шрифтов при создании PDF за {Date:dd.MM.yyyy}",
                report.CurrentDate);
            return Error.Failure(description: "Ошибка отрисовки PDF: в системе отсутствует необходимый шрифт.");
        }
        catch (DocumentLayoutException ex)
        {
            logger.LogError(ex, "Ошибка вёрстки при создании PDF за {Date:dd.MM.yyyy}", report.CurrentDate);
            return Error.Failure(description: "Ошибка разметки при формировании PDF-отчёта.");
        }
        catch (InitializationException ex)
        {
            logger.LogError(ex, "Ошибка инициализации движка QuestPDF при создании отчёта за {Date:dd.MM.yyyy}",
                report.CurrentDate);
            return Error.Failure(description: "Не удалось инициализировать генератор PDF.");
        }
    }

    private static void ComposeHeader(IContainer container, DailyExchangeRatesReport report)
    {
        container.PaddingBottom(12).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Отчёт по курсам валют ЦБ РФ").FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                col.Item()
                    .PaddingTop(4)
                    .Text(report.FormatReportDatesLine())
                    .FontSize(11).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private static void ComposeContent(IContainer container, DailyExchangeRatesReport report)
    {
        container.PaddingVertical(10).Column(col =>
        {
            col.Spacing(14);

            col.Item().Element(c => ComposeSummaryBlock(c, report));
            col.Item().Element(c => ComposeWeeklyChartBlock(c, report));
            col.Item().Element(c => ComposeRatesTable(c, report));
        });
    }

    private static void ComposeSummaryBlock(IContainer container, DailyExchangeRatesReport report)
    {
        container.Background(Colors.Grey.Lighten4).Padding(12).Column(col =>
        {
            col.Spacing(8);

            col.Item().Text("Итоги дня").FontSize(14).Bold();

            col.Item().Text(report.FormatAveragePercentChange()).SemiBold();

            col.Item().Row(row =>
            {
                row.Spacing(16);

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Топ-3 роста:").Bold().FontColor(Colors.Green.Darken2);
                    foreach (var item in report.TopGrown)
                        c.Item().Text(item.FormatSummaryLine());
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Топ-3 падения:").Bold().FontColor(Colors.Red.Darken2);
                    foreach (var item in report.TopFallen)
                        c.Item().Text(item.FormatSummaryLine());
                });
            });
        });
    }

    private static readonly string[] SeriesHexColors =
    [
        "#1565C0",
        "#2E7D32",
        "#C62828",
        "#6A1B9A",
        "#EF6C00"
    ];

    private static void ComposeWeeklyChartBlock(IContainer container, DailyExchangeRatesReport report)
    {
        container.Background(Colors.Grey.Lighten4).Padding(12).Column(col =>
        {
            col.Spacing(8);

            col.Item().Text(report.FormatWeeklyChartTitle()).FontSize(14).Bold();

            if (report.FormatWeeklyChartNote() is { } chartNote)
            {
                col.Item()
                    .Text(chartNote)
                    .FontSize(9.5f)
                    .Italic()
                    .FontColor(Colors.Grey.Darken2);
            }

            if (report.WeeklyTrends.Count > 0)
            {
                col.Item()
                    .Height(210)
                    .Svg(size => RenderWeeklyChartSvg(report, size));
            }
        });
    }

    private static string RenderWeeklyChartSvg(DailyExchangeRatesReport report, Size size)
    {
        using var plot = new Plot();

        var dates = report.WeeklyTrends
            .SelectMany(t => t.Points)
            .Select(p => p.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToArray();

        var dateIndexMap = dates
            .Select((d, i) => (Date: d, Index: (double)i))
            .ToDictionary(x => x.Date, x => x.Index);

        for (var i = 0; i < report.WeeklyTrends.Count; i++)
        {
            var trend = report.WeeklyTrends[i];
            var hexColor = SeriesHexColors[i % SeriesHexColors.Length];
            AddCurrencySeries(plot, trend, dateIndexMap, hexColor);
        }

        var ticks = dates
            .Select((d, i) => new Tick(i, d.ToString("dd.MM", CultureInfo.InvariantCulture)))
            .ToArray();

        plot.Axes.Bottom.TickGenerator = new NumericManual(ticks);
        plot.Axes.Bottom.MajorTickStyle.Length = 4;
        plot.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.18, top: 0.35);

        plot.Font.Set("Arial");
        plot.ShowLegend(Edge.Top);

        return plot.GetSvgXml((int)size.Width, (int)size.Height);
    }

    private static void AddCurrencySeries(
        Plot plot,
        CurrencyRateTrend trend,
        IReadOnlyDictionary<DateOnly, double> dateIndexMap,
        string hexColor
    )
    {
        var count = trend.Points.Count;
        if (count == 0)
            return;

        var color = ScottPlot.Color.FromHex(hexColor);
        var xs = new double[count];
        var ys = new double[count];

        for (var i = 0; i < count; i++)
        {
            var point = trend.Points[i];
            xs[i] = dateIndexMap[point.Date];
            ys[i] = (double)point.UnitRate;
        }

        var scatter = plot.Add.Scatter(xs, ys);
        scatter.LegendText = trend.Currency.FormatChartLegend();
        scatter.Color = color;
        scatter.LineWidth = 2.5f;
        scatter.MarkerSize = 7;

        for (var i = 0; i < count; i++)
        {
            var label = plot.Add.Text(
                trend.Points[i].UnitRate.FormatShortRate(),
                xs[i],
                ys[i]
            );
            label.LabelFontColor = color;
            label.LabelFontSize = 9;
            label.LabelBold = true;
            label.LabelAlignment = Alignment.LowerCenter;
            label.OffsetY = -6;
        }
    }

    private static void ComposeRatesTable(IContainer container, DailyExchangeRatesReport report)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(45);
                columns.RelativeColumn(3);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.3f);
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Код", alignRight: false);
                HeaderCell(header.Cell(), "Валюта", alignRight: false);
                HeaderCell(header.Cell(), "Курс (руб.)", alignRight: true);
                HeaderCell(header.Cell(), "Пред. (руб.)", alignRight: true);
                HeaderCell(header.Cell(), "Изм. (руб.)", alignRight: true);
                HeaderCell(header.Cell(), "Изм. (%)", alignRight: true);
            });

            foreach (var rate in report.AllRates)
            {
                var changeColor = rate.PercentChange switch
                {
                    > 0 => Colors.Green.Darken2,
                    < 0 => Colors.Red.Darken2,
                    _ => Colors.Black
                };

                BodyCell(table.Cell(), rate.Currency.CharCode);
                BodyCell(table.Cell(), rate.Currency.Name);
                BodyCell(table.Cell(), rate.CurrentUnitRate.FormatRate(), alignRight: true);
                BodyCell(table.Cell(), rate.PreviousUnitRate.FormatRate(), alignRight: true);
                BodyCell(table.Cell(), rate.AbsoluteChange.FormatSigned(), alignRight: true, color: changeColor);
                BodyCell(table.Cell(), rate.PercentChange.FormatSigned("%"), alignRight: true, color: changeColor);
            }
        });
    }

    private static void HeaderCell(IContainer container, string text, bool alignRight = false)
    {
        var styled = container
            .Background(Colors.Blue.Darken3)
            .PaddingVertical(6)
            .PaddingHorizontal(4);

        if (alignRight)
            styled = styled.AlignRight();

        styled.Text(text).Bold().FontColor(Colors.White).FontSize(9);
    }

    private static void BodyCell(IContainer container, string text, bool alignRight = false, string? color = null)
    {
        var styled = container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(4);

        if (alignRight)
            styled = styled.AlignRight();

        var textDescriptor = styled.Text(text).FontSize(9);
        if (color is not null)
            textDescriptor.FontColor(color);
    }
}