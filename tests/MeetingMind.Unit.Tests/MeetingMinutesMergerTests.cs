using MeetingMind.Application.Meetings;

namespace MeetingMind.Unit.Tests;

public sealed class MeetingMinutesMergerTests
{
    private readonly MeetingMinutesMerger _merger = new();

    [Fact]
    public void MergePreservesAllSectionsAndDeduplicatesNormalizedStrings()
    {
        var merged = _merger.Merge(
        [
            CreateMinutes(
                "Weekly sync",
                "First summary",
                attendees: [" Hasham ", "Alice"],
                discussionPoints: ["Release   planning"],
                decisions: ["Ship Friday"],
                risks: ["API delay"],
                nextSteps: ["Prepare release"]),
            CreateMinutes(
                "weekly SYNC",
                "Second summary",
                attendees: ["hasham", "Bob"],
                discussionPoints: ["release planning", "Support readiness"],
                decisions: ["ship friday", "Notify support"],
                risks: ["api delay", "Staffing"],
                nextSteps: ["prepare release", "Send announcement"])
        ]);

        Assert.Equal("Weekly sync", merged.Title);
        Assert.Equal("First summary\n\nSecond summary", merged.Summary);
        Assert.Equal(["Hasham", "Alice", "Bob"], merged.Attendees);
        Assert.Equal(["Release planning", "Support readiness"], merged.DiscussionPoints);
        Assert.Equal(["Ship Friday", "Notify support"], merged.Decisions);
        Assert.Equal(["API delay", "Staffing"], merged.Risks);
        Assert.Equal(["Prepare release", "Send announcement"], merged.NextSteps);
    }

    [Fact]
    public void MergeFillsMissingActionMetadataAndPreservesConflicts()
    {
        var first = CreateMinutes("Title", "Summary", actionItems:
        [
            new MeetingActionItem("Prepare report", null, "Friday"),
            new MeetingActionItem("Book room", "Alice", null)
        ]);
        var second = CreateMinutes("Title", "Summary", actionItems:
        [
            new MeetingActionItem(" prepare   report ", "Hasham", null),
            new MeetingActionItem("Book room", "Bob", null),
            new MeetingActionItem("Book room", "Alice", "Monday")
        ]);

        var merged = _merger.Merge([first, second]);

        Assert.Contains(
            new MeetingActionItem("Prepare report", "Hasham", "Friday"),
            merged.ActionItems);
        Assert.Contains(
            new MeetingActionItem("Book room", "Alice", "Monday"),
            merged.ActionItems);
        Assert.Contains(
            new MeetingActionItem("Book room", "Bob", null),
            merged.ActionItems);
        Assert.Equal(3, merged.ActionItems.Count);
    }

    private static MeetingMinutesContent CreateMinutes(
        string title,
        string summary,
        IReadOnlyList<string>? attendees = null,
        IReadOnlyList<string>? discussionPoints = null,
        IReadOnlyList<string>? decisions = null,
        IReadOnlyList<MeetingActionItem>? actionItems = null,
        IReadOnlyList<string>? risks = null,
        IReadOnlyList<string>? nextSteps = null)
    {
        return new MeetingMinutesContent(
            title,
            summary,
            attendees ?? [],
            discussionPoints ?? [],
            decisions ?? [],
            actionItems ?? [],
            risks ?? [],
            nextSteps ?? []);
    }
}
