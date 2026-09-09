using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.BotHandlers;

/// <summary>
/// Builds the visual Telegram route card. It contains presentation logic only.
/// </summary>
internal static class MetroRouteCardRenderer
{
    private const string FontName = "MetroVazirmatn";
    private const string Ink = "#132238";
    private const string Muted = "#64748B";
    private const string Paper = "#F3F6F9";
    private const string Card = "#FFFFFF";
    private const string Teal = "#008C8C";
    private static readonly object FontLock = new();
    private static bool _fontRegistered;

    public static byte[] Render(MetroNavigationResult result)
    {
        EnsureFont();
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        var document = Document.Create(root =>
        {
            root.Page(page =>
            {
                // A route can contain a variable number of transfers. Continuous mode
                // grows one page to fit the content instead of flowing into extra pages.
                page.ContinuousSize(270, Unit.Point);
                page.Margin(0);
                page.PageColor(Paper);
                page.DefaultTextStyle(style => style
                    .FontFamily(FontName)
                    .FontColor(Ink)
                    .FontSize(9));

                page.Content().Column(layout =>
                {
                    layout.Item().Element(container => ComposeHeader(container, result));
                    layout.Item().PaddingHorizontal(16).PaddingTop(12).Column(body =>
                    {
                        body.Spacing(9);
                        body.Item().Element(container => ComposeSummary(container, result));
                        body.Item().Text("YOUR ROUTE").FontSize(7).SemiBold().FontColor(Muted).LetterSpacing(0.12f);
                        body.Item().Element(container => ComposeWalk(
                            container,
                            "START WITH A WALK",
                            $"Head to {result.OriginStation.NameEn}",
                            result.OriginWalkMeters,
                            result.OriginWalkTime,
                            Teal));

                        if (result.Legs.Count == 0)
                        {
                            body.Item().Background("#FFF7DD").CornerRadius(8).Padding(10)
                                .Text("Both pins are closest to the same station. Walking directly is likely the smoother trip.")
                                .FontSize(9).FontColor("#7A5400");
                        }

                        foreach (var leg in result.Legs)
                        {
                            if (leg.RequiresTransfer)
                                body.Item().PaddingLeft(8).Text(
                                        $"TRANSFER AT {leg.FromStationName.ToUpperInvariant()}  ·  {FormatDuration(TimeSpan.FromSeconds(leg.TransferWalkSeconds))} between platforms")
                                    .FontSize(7).SemiBold().FontColor(Muted);
                            body.Item().Element(container => ComposeTrainLeg(container, leg));
                        }

                        body.Item().Element(container => ComposeWalk(
                            container,
                            "FINISH ON FOOT",
                            $"{result.DestinationStation.NameEn} to your destination",
                            result.DestinationWalkMeters,
                            result.DestinationWalkTime,
                            "#F59E0B"));
                        var timetable = string.Join(" / ", result.Legs.Select(x => x.ServiceDayType).Distinct().Select(x => x switch
                        {
                            "friday" => "Friday/holiday",
                            "thursday" => "Thursday",
                            _ => "Saturday–Wednesday"
                        }));
                        body.Item().PaddingTop(2).AlignCenter()
                            .Text($"{(string.IsNullOrEmpty(timetable) ? "Scheduled" : timetable)} timetable · Walking distance is estimated")
                            .FontSize(6.5f).FontColor(Muted);
                    });
                });
            });
        });

        return document.GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 288
        }).Single();
    }

    private static void ComposeHeader(IContainer container, MetroNavigationResult result)
    {
        container.Background(Ink).PaddingHorizontal(17).PaddingVertical(14).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("TEHRAN METRO")
                    .FontSize(8).SemiBold().FontColor("#76E4E4").LetterSpacing(0.12f);
                row.AutoItem().Text($"PLAN · {result.RequestedAtTehran:HH:mm}")
                    .FontSize(7).FontColor("#B6C3D3");
            });
            column.Item().Text($"{result.OriginStation.NameEn}  →  {result.DestinationStation.NameEn}")
                .FontSize(18).Bold().FontColor(Card);
            column.Item().Text("A calmer way across the city.")
                .FontSize(8).FontColor("#B6C3D3");
        });
    }

    private static void ComposeSummary(IContainer container, MetroNavigationResult result)
    {
        container.Row(row =>
        {
            row.Spacing(6);
            row.RelativeItem().Element(box => SummaryBox(box, "TOTAL", FormatDuration(result.TotalTime), Teal));
            row.RelativeItem().Element(box => SummaryBox(box, "CHANGES", Math.Max(0, result.Legs.Count - 1).ToString(), "#8B5CF6"));
            row.RelativeItem().Element(box => SummaryBox(box, "ARRIVE", result.EstimatedArrival.ToString("HH:mm"), "#F59E0B"));
        });
    }

    private static void SummaryBox(IContainer container, string label, string value, string color)
    {
        container.Background(Card).CornerRadius(8).Padding(8).Column(column =>
        {
            column.Item().Text(label).FontSize(6.5f).SemiBold().FontColor(Muted).LetterSpacing(0.08f);
            column.Item().Text(value).FontSize(13).Bold().FontColor(color);
        });
    }

    private static void ComposeWalk(
        IContainer container,
        string title,
        string description,
        double meters,
        TimeSpan duration,
        string color)
    {
        container.Background(Card).CornerRadius(8).BorderLeft(4).BorderColor(color).Padding(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(title).FontSize(7).SemiBold().FontColor(color).LetterSpacing(0.06f);
                column.Item().Text(description).FontSize(10).SemiBold();
            });
            row.AutoItem().AlignMiddle().Column(column =>
            {
                column.Item().AlignRight().Text(FormatDistance(meters)).FontSize(10).Bold();
                column.Item().AlignRight().Text(FormatDuration(duration)).FontSize(7).FontColor(Muted);
            });
        });
    }

    private static void ComposeTrainLeg(IContainer container, MetroJourneyLeg leg)
    {
        var lineColor = NormalizeColor(leg.LineColor, leg.LineId);
        var time = leg.ScheduleFound
            ? $"{leg.Departure:HH:mm}  →  {leg.Arrival:HH:mm}"
            : $"ABOUT {FormatDuration(leg.EstimatedRideTime)}";
        var detail = leg.ScheduleFound
            ? $"wait {FormatDuration(leg.WaitTime)}  ·  ride {FormatDuration(leg.EstimatedRideTime)}"
            : "No matching departure in the timetable";

        container.Background(Card).CornerRadius(9).BorderLeft(6).BorderColor(lineColor).Padding(10).Row(row =>
        {
            row.ConstantItem(30).Height(30).CornerRadius(15).Background(lineColor)
                .AlignCenter().AlignMiddle().Text(leg.LineId.ToString())
                .FontSize(13).Bold().FontColor(ContrastColor(lineColor));
            row.ConstantItem(8);
            row.RelativeItem().Column(column =>
            {
                column.Item().Text($"LINE {leg.LineId}  ·  TOWARD {leg.DirectionName.ToUpperInvariant()}")
                    .FontSize(7).SemiBold().FontColor(lineColor);
                column.Item().Text($"{leg.FromStationName}  →  {leg.ToStationName}")
                    .FontSize(10.5f).Bold();
                column.Item().Text(detail).FontSize(7).FontColor(Muted);
            });
            row.AutoItem().AlignMiddle().Text(time).FontSize(9).Bold().FontColor(lineColor);
        });
    }

    private static void EnsureFont()
    {
        if (_fontRegistered) return;
        lock (FontLock)
        {
            if (_fontRegistered) return;
            var regular = Path.Combine(AppContext.BaseDirectory, "Fonts", "Vazirmatn-Regular.ttf");
            var bold = Path.Combine(AppContext.BaseDirectory, "Fonts", "Vazirmatn-Bold.ttf");
            using (var stream = File.OpenRead(regular)) FontManager.RegisterFontWithCustomName(FontName, stream);
            using (var stream = File.OpenRead(bold)) FontManager.RegisterFontWithCustomName(FontName, stream);
            _fontRegistered = true;
        }
    }

    private static string NormalizeColor(string? color, byte lineId) =>
        !string.IsNullOrWhiteSpace(color) && color.StartsWith('#') ? color : lineId switch
        {
            1 => "#E31E24",
            2 => "#1D70B7",
            3 => "#00A6D6",
            4 => "#F4C430",
            5 => "#43A047",
            6 => "#EC407A",
            7 => "#7E57C2",
            _ => Teal
        };

    private static string ContrastColor(string color) =>
        color.Equals("#F4C430", StringComparison.OrdinalIgnoreCase) ||
        color.Equals("#FFDD00", StringComparison.OrdinalIgnoreCase)
            ? Ink
            : Card;

    private static string FormatDistance(double meters) =>
        meters < 1000 ? $"{Math.Round(meters / 10) * 10:N0} m" : $"{meters / 1000:0.0} km";

    private static string FormatDuration(TimeSpan duration)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));
        return minutes < 60 ? $"{minutes} min" : $"{minutes / 60}h {minutes % 60}m";
    }
}
