using System.Text;

namespace MeetingMind.Application.Meetings;

public static class MeetingMinutesFormatter
{
    public static string ToMarkdown(MeetingMinutesContent minutes)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"# {minutes.Title}");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine(minutes.Summary);
        builder.AppendLine();

        AppendStringList(builder, "Attendees", minutes.Attendees);
        AppendStringList(builder, "Discussion Points", minutes.DiscussionPoints);
        AppendStringList(builder, "Decisions", minutes.Decisions);
        AppendActionItems(builder, minutes.ActionItems);
        AppendStringList(builder, "Risks / Blockers", minutes.Risks);
        AppendStringList(builder, "Next Steps", minutes.NextSteps);

        return builder.ToString();
    }

    private static void AppendStringList(StringBuilder builder, string heading, IReadOnlyList<string> values)
    {
        builder.AppendLine($"## {heading}");

        if (values.Count == 0)
        {
            builder.AppendLine("- None identified");
        }
        else
        {
            foreach (var value in values)
            {
                builder.AppendLine($"- {value}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendActionItems(StringBuilder builder, IReadOnlyList<MeetingActionItem> actionItems)
    {
        builder.AppendLine("## Action Items");

        if (actionItems.Count == 0)
        {
            builder.AppendLine("- None identified");
            builder.AppendLine();
            return;
        }

        foreach (var actionItem in actionItems)
        {
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(actionItem.Owner))
            {
                details.Add($"Owner: {actionItem.Owner}");
            }

            if (!string.IsNullOrWhiteSpace(actionItem.DueDate))
            {
                details.Add($"Due: {actionItem.DueDate}");
            }

            var suffix = details.Count == 0 ? string.Empty : $" ({string.Join(", ", details)})";
            builder.AppendLine($"- {actionItem.Description}{suffix}");
        }

        builder.AppendLine();
    }
}
