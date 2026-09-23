using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScheduleBot.Models;

namespace ScheduleBot.Services;

/// <summary>Creates the complete comparison report that precedes Telegram navigation.</summary>
public sealed class SurveyReportService
{
    private const string Navy = "#16233A";
    private const string Blue = "#2F67D8";
    private const string Pale = "#EEF3FF";
    private const string Gray = "#667085";

    public byte[] CreateComparisonPdf(SurveyComparisonData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(34);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Navy));
                page.Header().Column(header =>
                {
                    header.Item().Text(data.SurveyName).FontSize(22).Bold().FontColor(Blue);
                    header.Item().PaddingTop(3).Text($"Survey comparison | {data.Participants.Count} shared response set{(data.Participants.Count == 1 ? "" : "s")} | {(data.IsPrivate ? "private JSON comparison" : "public member comparison")}")
                        .FontSize(9).FontColor(Gray);
                });
                page.Content().PaddingVertical(16).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Background(Pale).Padding(12).Column(summary =>
                    {
                        summary.Item().Text("How to read this report").Bold().FontSize(11);
                        summary.Item().PaddingTop(4).Text("Each question can define its own two perspectives. For those questions, each person's first-state answer is compared with every other person's second-state answer, and vice versa. Questions without states are shown together normally.");
                        if (data.IsPrivate)
                            summary.Item().PaddingTop(3).Text("Only the JSON files supplied for this comparison are included; no other member answers were read.");
                    });
                    for (var i = 0; i < data.Questions.Count; i++)
                    {
                        var questionNumber = i + 1;
                        var question = data.Questions[i];
                        column.Item().EnsureSpace(90).Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().BorderBottom(1).BorderColor(Blue).PaddingBottom(5)
                                .Text($"{questionNumber}. {question.Title}").FontSize(13).SemiBold();
                            if (question.Options.Count > 0)
                            {
                                var all = data.Participants.SelectMany(p => Answers(p, questionNumber)).ToList();
                                section.Item().Text("Choice totals: " + string.Join(" | ", question.Options.Select(o => $"{o}: {all.Count(a => a.Value == o)}")))
                                    .FontColor(Gray);
                            }
                            if (question.States.Count == 2) RenderStatePairs(section, data, questionNumber, question.Type, question.States);
                            else RenderAnswers(section, data, questionNumber, question.Type);
                        });
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("ScheduleBot survey report  |  ").FontColor(Gray);
                    text.CurrentPageNumber().FontColor(Gray);
                    text.Span(" / ").FontColor(Gray);
                    text.TotalPages().FontColor(Gray);
                });
            });
        }).GeneratePdf();
    }

    private static void RenderAnswers(ColumnDescriptor section, SurveyComparisonData data, int question, string type)
    {
        section.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(5);
                columns.RelativeColumn(2);
            });
            table.Header(header =>
            {
                Header(header.Cell(), "Participant");
                Header(header.Cell(), "Answer");
                Header(header.Cell(), "Comparison");
            });
            var baseline = data.Participants.SelectMany(p => Answers(p, question)).FirstOrDefault()?.Value;
            foreach (var person in data.Participants)
            {
                var answer = Answers(person, question).FirstOrDefault()?.Value;
                Cell(table.Cell(), person.Name);
                Cell(table.Cell(), answer ?? "Not answered");
                Cell(table.Cell(), answer == null || baseline == null ? "-" : CompareValues(type, baseline, answer));
            }
        });
    }

    private static void RenderStatePairs(ColumnDescriptor section, SurveyComparisonData data, int question, string type, IReadOnlyList<string> states)
    {
        var first = states[0];
        var second = states[1];
        foreach (var person in data.Participants)
        {
            var firstValue = Answer(person, question, first);
            var secondValue = Answer(person, question, second);
            section.Item().PaddingTop(5).Text(person.Name).Bold().FontColor(Blue);
            if (data.Participants.Count == 1)
                section.Item().Text($"{first}: {firstValue ?? "Not answered"} | {second}: {secondValue ?? "Not answered"}");
            foreach (var other in data.Participants.Where(p => p.Key != person.Key))
            {
                var otherSecond = Answer(other, question, second);
                var otherFirst = Answer(other, question, first);
                section.Item().BorderLeft(2).BorderColor(Pale).PaddingLeft(8).Column(pair =>
                {
                    pair.Item().Text($"{person.Name} as {first}: {firstValue ?? "Not answered"}");
                    pair.Item().Text($"vs {other.Name} as {second}: {otherSecond ?? "Not answered"}  [{CompareNullable(type, firstValue, otherSecond)}]").FontColor(Gray);
                    pair.Item().PaddingTop(2).Text($"{person.Name} as {second}: {secondValue ?? "Not answered"}");
                    pair.Item().Text($"vs {other.Name} as {first}: {otherFirst ?? "Not answered"}  [{CompareNullable(type, secondValue, otherFirst)}]").FontColor(Gray);
                });
            }
        }
    }

    private static IEnumerable<SurveyExportAnswer> Answers(SurveyComparisonParticipant person, int question) =>
        person.Answers.Where(a => a.Question == question);

    private static string? Answer(SurveyComparisonParticipant person, int question, string state) =>
        person.Answers.FirstOrDefault(a => a.Question == question && a.State == state)?.Value;

    public static string CompareNullable(string type, string? first, string? second) =>
        first == null || second == null ? "Incomplete" : CompareValues(type, first, second);

    public static string CompareValues(string type, string first, string second)
    {
        if (type == SurveyTypes.Number && decimal.TryParse(first, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
            decimal.TryParse(second, NumberStyles.Number, CultureInfo.InvariantCulture, out var b))
            return a == b ? "Same" : $"Difference {Math.Abs(a - b):G29}";
        return string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase) ? "Same" : "Different";
    }

    private static void Header(IContainer cell, string text) => cell.Background(Navy).Padding(5).Text(text).SemiBold().FontColor(Colors.White);
    private static void Cell(IContainer cell, string text) => cell.BorderBottom(0.5f).BorderColor("#D0D5DD").Padding(5).Text(text);
}
