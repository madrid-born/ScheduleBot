using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

public sealed record MapPoint(double Lat, double Lon);
public sealed record MapRoad(long OsmId, string Name, string EnglishName, string RoadClass, MapPoint[] Points);

/// <summary>Real offline OSM geometry. Render only places the learner has unlocked.</summary>
public sealed class TehranMapCatalog
{
    private readonly Dictionary<string, MapRoad[]> imported = new();
    private readonly MapRoad[] roads;
    private readonly Dictionary<string, string[]> names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mirdamad Boulevard"] = ["بلوار میرداماد", "میرداماد"],
        ["Shariati Street"] = ["شریعتی", "دکتر علی شریعتی"],
        ["Modarres Expressway"] = ["بزرگراه آیت الله مدرس", "بزرگراه شهید آیت الله مدرس"],
        ["Hemat Expressway"] = ["بزرگراه همت", "بزرگراه شهید همت"],
        ["Sadr Expressway"] = ["بزرگراه صدر"],
        ["Shahid Beheshti Street"] = ["شهید بهشتی", "آیت الله بهشتی", "بهشتی"],
        ["Motahari Street"] = ["آیت الله مطهری", "مطهری"],
        ["Enghelab Street"] = ["انقلاب اسلامی"],
        ["Keshavarz Boulevard"] = ["بلوار کشاورز"]
    };
    public IReadOnlyList<Place> AdditionalPlaces { get; }

    public TehranMapCatalog(string path)
    {
        using var file = File.OpenRead(path);
        using var zip = new GZipStream(file, CompressionMode.Decompress);
        using var json = JsonDocument.Parse(zip);
        roads = json.RootElement.GetProperty("elements").EnumerateArray()
            .Where(e => e.TryGetProperty("geometry", out _) && e.TryGetProperty("tags", out _))
            .Select(e =>
            {
                var tags = e.GetProperty("tags");
                string Tag(string key) => tags.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
                return new MapRoad(e.GetProperty("id").GetInt64(), Tag("name"), Tag("name:en"), Tag("highway"),
                    e.GetProperty("geometry").EnumerateArray().Where(p => p.TryGetProperty("lat", out _))
                        .Select(p => new MapPoint(p.GetProperty("lat").GetDouble(), p.GetProperty("lon").GetDouble())).ToArray());
            }).Where(r => r.Points.Length >= 2).ToArray();
        var excluded = names.Values.SelectMany(n => n).ToHashSet();
        var candidates = roads.Where(r => IsStreet(r) && r.Name.Length > 2 && r.Name.Length <= 250 && !excluded.Contains(r.Name))
            .GroupBy(r => r.Name).Select(g => new { Name = g.Key, Roads = g.ToArray(), Length = g.Sum(Length) })
            .Where(g => g.Length >= .004).OrderByDescending(g => g.Length).ThenBy(g => g.Name, StringComparer.Ordinal);
        var added = new List<Place>();
        var displayNames = new HashSet<string>(names.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var english = candidate.Roads.Select(r => r.EnglishName).FirstOrDefault(n => n.Length is > 2 and <= 250);
            var name = english ?? candidate.Name;
            if (!displayNames.Add(name)) continue;
            var key = "road:" + candidate.Roads.Min(r => r.OsmId).ToString(CultureInfo.InvariantCulture);
            imported[key] = candidate.Roads;
            var center = Center(candidate.Roads);
            added.Add(new Place { Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("tehran-osm:" + key)).AsSpan(0, 16)),
                Name = name, PlaceType = TehranPlaceTypes.Street, Source = "osm_snapshot", ExternalId = key,
                Latitude = (decimal)center.Lat, Longitude = (decimal)center.Lon, Priority = 60,
                CreatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) });
            // Nine original streets plus 121 new streets give 130: an even number of two-place nights.
            if (added.Count == 121) break;
        }
        if (added.Count < 121) throw new InvalidOperationException("The road snapshot must supply at least 121 distinct named streets.");
        AdditionalPlaces = added;
    }

    private static bool IsStreet(MapRoad r) => r.RoadClass is "motorway" or "trunk" or "primary" or "secondary" or "tertiary";
    private static double Length(MapRoad r) => r.Points.Zip(r.Points.Skip(1), (a, b) =>
        Math.Sqrt(Math.Pow(a.Lat - b.Lat, 2) + Math.Pow((a.Lon - b.Lon) * .81, 2))).Sum();
    private MapRoad[] Target(Place place) => place.ExternalId != null && imported.TryGetValue(place.ExternalId, out var found) ? found :
        !names.TryGetValue(place.Name, out var aliases) ? [] : roads.Where(r => aliases.Contains(r.Name) && IsStreet(r)).ToArray();
    public bool CanRender(Place place) => Target(place).Length > 0;
    public bool IsHighway(Place place) => Target(place).Any(r => r.RoadClass is "motorway" or "trunk");
    public string PersianName(Place place) => Target(place).FirstOrDefault()?.Name ?? place.Name;
    private static (double Lat, double Lon) Center(MapRoad[] target)
    {
        var points = target.SelectMany(r => r.Points).ToArray();
        return ((points.Min(p => p.Lat) + points.Max(p => p.Lat)) / 2,
            (points.Min(p => p.Lon) + points.Max(p => p.Lon)) / 2);
    }
    public double Distance(Place a, Place b)
    {
        var x = Center(Target(a)); var y = Center(Target(b));
        return Math.Pow(x.Lat - y.Lat, 2) + Math.Pow((x.Lon - y.Lon) * .81, 2);
    }
    public string DirectionFrom(Place target, Place reference)
    {
        var a = Center(Target(target)); var b = Center(Target(reference));
        var north = a.Lat - b.Lat; var east = (a.Lon - b.Lon) * .81;
        if (Math.Abs(north) < 1e-8 && Math.Abs(east) < 1e-8) return "Same position";
        // Eight compass sectors: keep both components instead of discarding the smaller one.
        var angle = Math.Atan2(east, north) * 180 / Math.PI;
        var sector = (int)Math.Floor((angle + 360 + 22.5) / 45) % 8;
        return new[] { "North", "North-east", "East", "South-east", "South", "South-west", "West", "North-west" }[sector];
    }
    public string Orientation(Place place)
    {
        var points = Target(place).SelectMany(r => r.Points).ToArray();
        return points.Max(p => p.Lat) - points.Min(p => p.Lat) >= (points.Max(p => p.Lon) - points.Min(p => p.Lon)) * .81
            ? "Mostly north–south" : "Mostly east–west";
    }
    public string LearningText(Place place) => $"{PersianName(place)} — {place.Name}\n\nFollow the red street and notice its shape. It runs {Orientation(place).ToLowerInvariant()}.\nOnly places you have learned appear around it. Confirm below when you are ready.";

    public string RenderSvg(IReadOnlyList<Place> visiblePlaces, Place? target = null, Place? reference = null, bool showNames = false)
    {
        // The only exception to fog-of-war is the new street currently being taught.
        var places = visiblePlaces.Append(target).Append(reference).Where(p => p != null).Select(p => p!)
            .DistinctBy(p => p.Id).Where(CanRender).ToArray();
        var geometry = places.SelectMany(Target).ToArray();
        var center = geometry.Length == 0 ? (Lat: 35.74, Lon: 51.405) : Center(geometry);
        var points = geometry.SelectMany(r => r.Points).ToArray();
        var cos = Math.Cos(center.Lat * Math.PI / 180);
        var scale = points.Length == 0 ? 4000 : Math.Min(20000,
            Math.Min(760 / Math.Max(.015, (points.Max(p => p.Lon) - points.Min(p => p.Lon)) * cos),
                     500 / Math.Max(.012, points.Max(p => p.Lat) - points.Min(p => p.Lat))));
        (double X, double Y) Project(MapPoint p) => (450 + (p.Lon - center.Lon) * scale * cos, 370 - (p.Lat - center.Lat) * scale);
        string F(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);
        string Escape(string s) => SecurityElement.Escape(s) ?? "";
        var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"740\"><rect width=\"900\" height=\"740\" fill=\"#f1f0e9\"/><defs><clipPath id=\"map\"><rect x=\"0\" y=\"60\" width=\"900\" height=\"630\"/></clipPath></defs><g clip-path=\"url(#map)\">");
        foreach (var place in places.OrderBy(p => p.Id == target?.Id ? 2 : p.Id == reference?.Id ? 1 : 0))
        {
            var highway = IsHighway(place);
            var color = place.Id == target?.Id ? "#dc2626" : place.Id == reference?.Id ? "#2563eb" : highway ? "#b77b27" : "#647b92";
            var lines = Target(place).Select(road => string.Join(" ", road.Points.Select(p => { var xy = Project(p); return $"{F(xy.X)},{F(xy.Y)}"; }))).ToArray();
            svg.Append($"<g data-road-kind=\"{(highway ? "highway" : "street")}\">");
            // Draw casings first for the entire road so segment ends don't erase adjacent segments.
            foreach (var line in lines)
                svg.Append($"<polyline points=\"{line}\" fill=\"none\" stroke=\"white\" stroke-width=\"{(highway ? 14 : 9)}\" stroke-linecap=\"round\"/>");
            foreach (var line in lines)
            {
                svg.Append($"<polyline points=\"{line}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{(highway ? 10 : 5)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
            }
            if (highway)
                foreach (var line in lines)
                    svg.Append($"<polyline points=\"{line}\" fill=\"none\" stroke=\"#fff6e5\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
            svg.Append("</g>");
        }
        var labelBoxes = new List<LabelBox>();
        var labels = new List<(Place Place, double AnchorX, double AnchorY, LabelBox Box, string[] Lines)>();
        var useLegend = showNames && places.Length > 18;
        var occupiedRoadPoints = showNames && !useLegend ? points.Select(Project)
            .DistinctBy(p => (Math.Round(p.X / 8), Math.Round(p.Y / 8))).ToArray() : [];
        if (showNames && !useLegend)
            foreach (var place in places.OrderByDescending(p => p.Name.Length).ThenBy(p => p.Id))
            {
                var road = Target(place).OrderByDescending(Length).First();
                var xy = Project(road.Points[road.Points.Length / 2]);
                var lines = WrapName(place.Name);
                var width = Math.Max(90, lines.Max(l => l.Length) * 8.5 + 20);
                var height = lines.Length * 19 + 12;
                var candidates = new List<LabelBox>();
                for (var y = 88d; y + height < 666; y += 22)
                    for (var x = 18d; x + width < 882; x += 24)
                    {
                        var box = new LabelBox(x, y, width, height);
                        if (!box.Intersects(new(815, 72, 70, 84)) && !labelBoxes.Any(b => box.Intersects(b, 10))) candidates.Add(box);
                    }
                if (candidates.Count == 0) { useLegend = true; break; }
                var chosen = candidates.OrderBy(b => Math.Pow(b.X + b.Width / 2 - xy.X, 2) +
                    Math.Pow(b.Y + b.Height / 2 - xy.Y, 2) + 5000 * occupiedRoadPoints.Count(p =>
                        p.X >= b.X - 8 && p.X <= b.X + b.Width + 8 && p.Y >= b.Y - 8 && p.Y <= b.Y + b.Height + 8)).First();
                labelBoxes.Add(chosen);
                labels.Add((place, xy.X, xy.Y, chosen, lines));
            }
        if (showNames && !useLegend)
        {
            // Leaders identify the road even when a label has to move away from a busy junction.
            foreach (var label in labels)
            {
                var box = label.Box;
                var endX = Math.Clamp(label.AnchorX, box.X, box.X + box.Width);
                var endY = Math.Clamp(label.AnchorY, box.Y, box.Y + box.Height);
                svg.Append($"<path d=\"M{F(label.AnchorX)} {F(label.AnchorY)} L{F(endX)} {F(endY)}\" stroke=\"#64748b\" stroke-width=\"1.2\" fill=\"none\"/><circle cx=\"{F(label.AnchorX)}\" cy=\"{F(label.AnchorY)}\" r=\"2.5\" fill=\"#172b40\"/>");
            }
            foreach (var label in labels)
            {
                var box = label.Box;
                svg.Append($"<g data-label-box=\"{F(box.X)},{F(box.Y)},{F(box.Width)},{F(box.Height)}\"><rect x=\"{F(box.X)}\" y=\"{F(box.Y)}\" width=\"{F(box.Width)}\" height=\"{F(box.Height)}\" rx=\"5\" fill=\"#fffdf8\" stroke=\"#d4d9dc\"/>");
                // Keep a full-name text element for accessibility and tests; tspans wrap it visually.
                svg.Append($"<text x=\"{F(box.X + 10)}\" y=\"{F(box.Y + 20)}\" font-family=\"sans-serif\" font-size=\"15\" fill=\"#172b40\">");
                for (var i = 0; i < label.Lines.Length; i++)
                    svg.Append($"<tspan x=\"{F(box.X + 10)}\" dy=\"{(i == 0 ? 0 : 19)}\">{Escape(label.Lines[i])}{(i + 1 < label.Lines.Length ? " " : "")}</tspan>");
                svg.Append("</text></g>");
            }
        }
        if (useLegend)
            for (var i = 0; i < places.Length; i++)
            {
                var road = Target(places[i]).OrderByDescending(Length).First();
                var xy = Project(road.Points[road.Points.Length / 2]);
                svg.Append($"<circle cx=\"{F(xy.X)}\" cy=\"{F(xy.Y)}\" r=\"11\" fill=\"white\" stroke=\"#172b40\"/><text x=\"{F(xy.X)}\" y=\"{F(xy.Y + 4)}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"11\">{i + 1}</text>");
            }
        svg.Append("</g><rect width=\"900\" height=\"60\" fill=\"#172b40\"/><text x=\"25\" y=\"38\" font-family=\"sans-serif\" font-size=\"24\" fill=\"white\">Your learned Tehran map</text><path d=\"M852 85 L842 109 L862 109 Z\" fill=\"#172b40\"/><text x=\"846\" y=\"133\" font-family=\"sans-serif\" font-size=\"20\">N</text><rect y=\"690\" width=\"900\" height=\"50\" fill=\"white\"/>");
        svg.Append($"<path d=\"M25 711 V720 H{F(25 + scale / 111.32)} V711\" fill=\"none\" stroke=\"#172b40\" stroke-width=\"2\"/><text x=\"25\" y=\"705\" font-family=\"sans-serif\" font-size=\"12\">1 km</text><text x=\"875\" y=\"732\" text-anchor=\"end\" font-family=\"sans-serif\" font-size=\"12\">Map data © OpenStreetMap contributors · ODbL</text>");
        svg.Append("<path d=\"M300 705 H340\" stroke=\"#647b92\" stroke-width=\"5\"/><text x=\"350\" y=\"710\" font-family=\"sans-serif\" font-size=\"13\">Street</text><path d=\"M470 705 H510\" stroke=\"#b77b27\" stroke-width=\"10\"/><path d=\"M470 705 H510\" stroke=\"#fff6e5\" stroke-width=\"3\"/><text x=\"520\" y=\"710\" font-family=\"sans-serif\" font-size=\"13\">Highway</text>");
        var heightPixels = 740;
        if (useLegend)
        {
            heightPixels += 36 + places.Length * 28;
            svg.Append($"<rect y=\"740\" width=\"900\" height=\"{heightPixels - 740}\" fill=\"white\"/>");
            for (var i = 0; i < places.Length; i++)
                svg.Append($"<text x=\"25\" y=\"{775 + i * 28}\" font-family=\"sans-serif\" font-size=\"16\"><tspan>{i + 1}. </tspan><tspan>{Escape(places[i].Name)}</tspan></text>");
        }
        svg.Append("</svg>");
        svg.Replace("height=\"740\"", $"height=\"{heightPixels}\"", 0, svg.ToString().IndexOf('>'));
        return svg.ToString();
    }
    public byte[] RenderPng(IReadOnlyList<Place> visiblePlaces, Place? target = null, Place? reference = null, bool showNames = false)
    {
        var svg = RenderSvg(visiblePlaces, target, reference, showNames);
        QuestPDF.Settings.License = LicenseType.Community;
        var height = float.Parse(System.Xml.Linq.XDocument.Parse(svg).Root!.Attribute("height")!.Value, CultureInfo.InvariantCulture);
        return Document.Create(root => root.Page(page =>
        { page.Size(450, height / 2); page.Margin(0); page.Content().Svg(svg); }))
            .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 144 }).Single();
    }

    private sealed record LabelBox(double X, double Y, double Width, double Height)
    {
        public bool Intersects(LabelBox b, double gap = 0) => X < b.X + b.Width + gap && X + Width + gap > b.X &&
            Y < b.Y + b.Height + gap && Y + Height + gap > b.Y;
    }
    private static string[] WrapName(string name)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in name.Split(' '))
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > 27) { lines.Add(line); line = ""; }
            line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) lines.Add(line);
        return lines.ToArray();
    }
}
