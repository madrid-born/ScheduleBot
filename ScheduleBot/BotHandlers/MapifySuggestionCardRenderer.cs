using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.BotHandlers;

/// <summary>Creates a visual, map-first suggestion card for Telegram.</summary>
internal static class MapifySuggestionCardRenderer
{
    private const string FontName = "MapifyVazirmatn";
    private const string Ink = "#102A43";
    private const string Muted = "#627D98";
    private const string Paper = "#F4F7FB";
    private const string Card = "#FFFFFF";
    private const string Accent = "#6D28D9";
    private static readonly object FontLock = new();
    private static bool _fontRegistered;

    public static byte[] Render(int rank, MapifyLocationSuggestion suggestion, double distanceKilometers, MapifyMapImage? mapImage, MapifyLinkPreview? linkPreview)
    {
        EnsureFont();
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        var location = suggestion.Location;
        var document = Document.Create(root =>
        {
            root.Page(page =>
            {
                page.ContinuousSize(270, Unit.Point);
                page.Margin(0);
                page.PageColor(Paper);
                page.DefaultTextStyle(style => style.FontFamily(FontName).FontColor(Ink).FontSize(9));
                page.Content().Column(column =>
                {
                    column.Item().Background(Ink).PaddingHorizontal(16).PaddingVertical(13).Column(header =>
                    {
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"MAPIFY PICK  ·  #{rank}").FontSize(7).SemiBold().FontColor("#C4B5FD").LetterSpacing(0.1f);
                            row.AutoItem().Text(FormatDistance(distanceKilometers)).FontSize(8).Bold().FontColor("#FFFFFF");
                        });
                        header.Item().PaddingTop(3).Text(location.Name).FontSize(17).Bold().FontColor("#FFFFFF");
                        header.Item().Text(string.Join(" · ", suggestion.CategoryNames)).FontSize(7.5f).FontColor("#D9E2EC");
                    });

                    column.Item().Padding(12).Column(body =>
                    {
                        body.Spacing(8);
                        body.Item().Element(container => ComposeMap(container, mapImage));
                        body.Item().Text("THE PINNED PLACE").FontSize(6.5f).SemiBold().FontColor(Accent).LetterSpacing(0.12f);
                        body.Item().Element(container => ComposeDetails(container, suggestion));
                        if (linkPreview != null) body.Item().Element(container => ComposePreview(container, linkPreview));
                        body.Item().AlignCenter().Text("Map data © OpenStreetMap contributors · pin marks the saved place")
                            .FontSize(5.7f).FontColor(Muted);
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

    private static void ComposeMap(IContainer container, MapifyMapImage? mapImage)
    {
        var map = container.Background(Card).CornerRadius(9).Border(1).BorderColor("#D9E2EC").Padding(3).Height(160);
        if (mapImage != null)
        {
            map.Svg(mapImage.Svg);
            return;
        }

        map.Background("#E6EEF6").AlignCenter().AlignMiddle().Column(column =>
        {
            column.Item().AlignCenter().Text("📍").FontSize(22);
            column.Item().AlignCenter().Text("Map preview unavailable").FontSize(8).FontColor(Muted);
        });
    }

    private static void ComposeDetails(IContainer container, MapifyLocationSuggestion suggestion)
    {
        var location = suggestion.Location;
        var visited = location.IsVisited
            ? $"Visited · {location.Score:0.#}/10"
            : "Not visited yet";
        var accent = location.IsVisited ? "#0F766E" : "#B45309";
        container.Background(Card).CornerRadius(9).BorderLeft(4).BorderColor(accent).Padding(10).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(visited).FontSize(9).SemiBold().FontColor(accent);
            if (!string.IsNullOrWhiteSpace(location.Description))
                column.Item().Text(location.Description).FontSize(8.3f).FontColor(Muted);
        });
    }

    private static void ComposePreview(IContainer container, MapifyLinkPreview preview)
    {
        container.Background("#F5F3FF").CornerRadius(9).Padding(8).Row(row =>
        {
            if (preview.Image != null)
            {
                row.ConstantItem(66).Height(66).CornerRadius(6).Image(preview.Image).FitArea();
                row.ConstantItem(8);
            }
            row.RelativeItem().Column(column =>
            {
                column.Spacing(3);
                column.Item().Text("SHARED LINK").FontSize(6.5f).SemiBold().FontColor(Accent).LetterSpacing(0.1f);
                if (!string.IsNullOrWhiteSpace(preview.Title)) column.Item().Text(preview.Title).FontSize(8.5f).SemiBold();
                if (!string.IsNullOrWhiteSpace(preview.Summary)) column.Item().Text(preview.Summary).FontSize(7.3f).FontColor(Muted);
            });
        });
    }

    private static string FormatDistance(double kilometers) => kilometers < 1
        ? $"{Math.Round(kilometers * 1000 / 10) * 10:N0} m"
        : $"{kilometers:0.0} km";

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
}

internal sealed record MapifyLinkPreview(string? Title, string? Summary, byte[]? Image);
internal sealed record MapifyMapImage(string Svg);
