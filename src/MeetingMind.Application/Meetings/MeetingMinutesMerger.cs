using System.Text.RegularExpressions;

namespace MeetingMind.Application.Meetings;

public sealed partial class MeetingMinutesMerger
{
    public MeetingMinutesContent Merge(IEnumerable<MeetingMinutesContent> minutesCollection)
    {
        var minutes = minutesCollection.ToArray();
        if (minutes.Length == 0)
        {
            throw new ArgumentException("At least one minutes result is required.", nameof(minutesCollection));
        }

        var titles = MergeStrings(minutes.Select(item => item.Title));
        var summaries = MergeStrings(minutes.Select(item => item.Summary));

        return new MeetingMinutesContent(
            titles.FirstOrDefault() ?? "Meeting minutes",
            string.Join("\n\n", summaries),
            MergeStrings(minutes.SelectMany(item => item.Attendees)),
            MergeStrings(minutes.SelectMany(item => item.DiscussionPoints)),
            MergeStrings(minutes.SelectMany(item => item.Decisions)),
            MergeActionItems(minutes.SelectMany(item => item.ActionItems)),
            MergeStrings(minutes.SelectMany(item => item.Risks)),
            MergeStrings(minutes.SelectMany(item => item.NextSteps)));
    }

    private static IReadOnlyList<string> MergeStrings(IEnumerable<string> values)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var normalized = Normalize(value);
            if (normalized.Length > 0 && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static IReadOnlyList<MeetingActionItem> MergeActionItems(
        IEnumerable<MeetingActionItem> actionItems)
    {
        var result = new List<MeetingActionItem>();

        foreach (var actionItem in actionItems)
        {
            var candidate = new MeetingActionItem(
                Normalize(actionItem.Description),
                NormalizeOptional(actionItem.Owner),
                NormalizeOptional(actionItem.DueDate));

            if (candidate.Description.Length == 0)
            {
                continue;
            }

            var compatibleIndex = result.FindIndex(existing =>
                EqualsNormalized(existing.Description, candidate.Description) &&
                Compatible(existing.Owner, candidate.Owner) &&
                Compatible(existing.DueDate, candidate.DueDate));

            if (compatibleIndex < 0)
            {
                result.Add(candidate);
                continue;
            }

            var compatible = result[compatibleIndex];
            result[compatibleIndex] = compatible with
            {
                Owner = compatible.Owner ?? candidate.Owner,
                DueDate = compatible.DueDate ?? candidate.DueDate
            };
        }

        return result;
    }

    private static bool Compatible(string? first, string? second)
    {
        return string.IsNullOrWhiteSpace(first) ||
               string.IsNullOrWhiteSpace(second) ||
               EqualsNormalized(first, second);
    }

    private static bool EqualsNormalized(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : WhitespaceRegex().Replace(value.Trim(), " ");

    private static string? NormalizeOptional(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0 ? null : normalized;
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
