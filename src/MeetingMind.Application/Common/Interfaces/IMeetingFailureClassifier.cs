using MeetingMind.Application.Common.Failures;

namespace MeetingMind.Application.Common.Interfaces;

public interface IMeetingFailureClassifier
{
    MeetingFailureClassification Classify(Exception exception);
}
