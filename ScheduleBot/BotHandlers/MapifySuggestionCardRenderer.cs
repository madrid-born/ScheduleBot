using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.BotHandlers;

/// <summary>Creates a visual suggestion card for Telegram.</summary>
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
                    column.Item().Background(Ink).PaddingHorizontal(14).PaddingVertical(9).Row(header =>
                    {
                        header.RelativeItem().Text($"MAPIFY PICK  ·  #{rank}").FontSize(7).SemiBold().FontColor("#C4B5FD").LetterSpacing(0.1f);
                        header.AutoItem().Text(FormatDistance(distanceKilometers)).FontSize(8).Bold().FontColor("#FFFFFF");
                    });

                    column.Item().Padding(12).Column(body =>
                    {
                        body.Spacing(8);
                        body.Item().Element(container => ComposeOverview(container, suggestion, linkPreview));
                        body.Item().Element(container => ComposeMap(container, mapImage));
                        body.Item().AlignCenter().Text("Map data © OpenStreetMap contributors")
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

    public static byte[] RenderMapOnly(MapifyMapImage mapImage)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(root =>
        {
            root.Page(page =>
            {
                page.ContinuousSize(270, Unit.Point);
                page.Margin(0);
                page.PageColor("#E6EEF6");
                page.Content().Width(270).Height(210).Svg(mapImage.Svg);
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

    private static void ComposeOverview(IContainer container, MapifyLocationSuggestion suggestion, MapifyLinkPreview? preview)
    {
        var location = suggestion.Location;
        var visited = location.IsVisited
            ? $"Visited · {location.Score:0.#}/10"
            : "Not visited yet";
        var statusColor = location.IsVisited ? "#0F766E" : "#B45309";
        var description = RemoveUrls(location.Description);

        container.Row(row =>
        {
            row.RelativeItem().MinHeight(150).Background(Card).CornerRadius(9).Border(1).BorderColor("#E4EAF1").Padding(10).Column(details =>
            {
                details.Spacing(5);
                details.Item().Text(location.Name).FontSize(14).Bold().FontColor(Ink);
                details.Item().Text(string.Join(" · ", suggestion.CategoryNames)).FontSize(7.2f).FontColor(Accent);
                details.Item().PaddingTop(5).Background(location.IsVisited ? "#ECFDF5" : "#FFF7ED").CornerRadius(5).Padding(6)
                    .Text(visited).FontSize(8.2f).SemiBold().FontColor(statusColor);
                if (!string.IsNullOrWhiteSpace(description))
                    details.Item().PaddingTop(3).Text(TrimCardText(description)).FontSize(7.5f).FontColor(Muted);
            });

            row.ConstantItem(8);
            row.ConstantItem(112).Height(150).Background("#EDE9FE").CornerRadius(9).Padding(3).Element(previewContainer =>
            {
                if (preview?.Image != null)
                {
                    previewContainer.CornerRadius(7).Image(preview.Image).FitArea();
                    return;
                }

                previewContainer.AlignCenter().AlignMiddle().Column(empty =>
                {
                    empty.Item().AlignCenter().Text("▣").FontSize(24).FontColor("#A78BFA");
                    empty.Item().AlignCenter().Text("No preview").FontSize(7).FontColor(Muted);
                });
            });
        });
    }

    private static string? RemoveUrls(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var withoutUrls = System.Text.RegularExpressions.Regex.Replace(text, "https?://[^\\s<>\\\"']+", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(withoutUrls) ? null : withoutUrls.Trim();
    }

    private static string TrimCardText(string text)
    {
        const int maximumLength = 220;
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maximumLength ? normalized : $"{normalized[..(maximumLength - 1)]}…";
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
