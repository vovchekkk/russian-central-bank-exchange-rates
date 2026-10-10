using System.Globalization;
using Core.Domain.Models;
using Core.Infrastructure.Helpers;
using Core.Services;
using Microsoft.Extensions.Logging;
using QuestPDF.Drawing.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ErrorOr;

namespace Core.Infrastructure;

public class PdfReportService(ILogger<PdfReportService> logger) : IPdfReportService
{
    public async Task<ErrorOr<byte[]>> GenerateDailyReportAsync(
        DailyExchangeRatesReport report,
        CancellationToken cancellationToken = default
    )
    {
        if (report.AllRates.Count == 0)
            return Error.Validation(description: "Нет данных для формирования PDF-отчёта.");

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

                BodyCell(table.Cell(), rate.CharCode);
                BodyCell(table.Cell(), rate.Name);
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