using MeetingMind.Application.Meetings;

namespace MeetingMind.Domain.Tests;

public class MeetingMinutesFormatterTests
{
    [Fact]
    public void ToMarkdownIncludesStructuredMeetingSections()
    {
        var minutes = new MeetingMinutesContent(
            "Sprint Planning",
            "The team planned the next sprint.",
            new[] { "Hasham", "Alex" },
            new[] { "Scope review" },
            new[] { "Prioritize upload flow" },
            new[] { new MeetingActionItem("Add minutes endpoint", "Hasham", "Friday") },
            new[] { "OpenAI API key missing locally" },
            new[] { "Run end-to-end test" });

        var markdown = MeetingMinutesFormatter.ToMarkdown(minutes);

        Assert.Contains("# Sprint Planning", markdown);
        Assert.Contains("## Summary", markdown);
        Assert.Contains("- Hasham", markdown);
        Assert.Contains("- Add minutes endpoint (Owner: Hasham, Due: Friday)", markdown);
        Assert.Contains("## Next Steps", markdown);
    }
}
