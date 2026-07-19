using MeetingMind.Application.Common.Exceptions;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using OpenAI.Chat;
using System.Text.Json;

namespace MeetingMind.Infrastructure.OpenAI;

public sealed class OpenAiMeetingMinutesGenerationClient : IMeetingMinutesGenerationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly BinaryData MinutesSchema = BinaryData.FromBytes("""
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "summary": { "type": "string" },
            "attendees": { "type": "array", "items": { "type": "string" } },
            "discussionPoints": { "type": "array", "items": { "type": "string" } },
            "decisions": { "type": "array", "items": { "type": "string" } },
            "actionItems": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "description": { "type": "string" },
                  "owner": { "type": ["string", "null"] },
                  "dueDate": { "type": ["string", "null"] }
                },
                "required": ["description", "owner", "dueDate"],
                "additionalProperties": false
              }
            },
            "risks": { "type": "array", "items": { "type": "string" } },
            "nextSteps": { "type": "array", "items": { "type": "string" } }
          },
          "required": [
            "title", "summary", "attendees", "discussionPoints", "decisions",
            "actionItems", "risks", "nextSteps"
          ],
          "additionalProperties": false
        }
        """u8.ToArray());

    private readonly ChatClient _chatClient;

    public OpenAiMeetingMinutesGenerationClient(OpenAiOptions openAiOptions)
    {
        if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
        {
            throw new PermanentMeetingProcessingException(
                "OpenAI API key is required. Configure OpenAI:ApiKey.");
        }

        if (string.IsNullOrWhiteSpace(openAiOptions.Model))
        {
            throw new PermanentMeetingProcessingException("OpenAI model is required.");
        }

        _chatClient = new ChatClient(openAiOptions.Model, openAiOptions.ApiKey);
    }

    public Task<MeetingMinutesContent> GenerateFromTranscriptAsync(
        string transcriptText,
        int chunkNumber,
        int chunkCount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new PermanentMeetingProcessingException(
                "Transcript is empty; meeting minutes cannot be generated.");
        }

        var chunkContext = chunkCount > 1
            ? $"This is transcript chunk {chunkNumber} of {chunkCount}. Adjacent chunks may overlap, so avoid treating repeated context as separate facts."
            : "This request contains the complete meeting transcript.";

        return CompleteAsync(
            "You generate concise, structured meeting minutes from transcript text. " +
            "Use only facts present in the supplied text. If a field is not identifiable, return an empty array. " +
            chunkContext,
            "Generate the complete structured meeting-minutes schema from this transcript text:\n\n" + transcriptText,
            cancellationToken);
    }

    public Task<MeetingMinutesContent> AggregateAsync(
        MeetingMinutesContent mergedMinutes,
        int tier,
        int groupNumber,
        int groupCount,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(mergedMinutes, JsonOptions);
        return CompleteAsync(
            "You consolidate structured partial meeting minutes without inventing facts. " +
            "Produce one coherent title and summary, preserve every supported section, and remove semantic duplicates. " +
            $"This is aggregation tier {tier}, group {groupNumber} of {groupCount}.",
            "Consolidate this deterministically pre-merged structured content into the complete meeting-minutes schema:\n\n" + json,
            cancellationToken);
    }

    private async Task<MeetingMinutesContent> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "meeting_minutes",
                    jsonSchema: MinutesSchema,
                    jsonSchemaIsStrict: true)
            };

            var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var content = completion.Value.Content.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new PermanentMeetingProcessingException(
                    "OpenAI meeting minutes response was empty or invalid.");
            }

            return Validate(JsonSerializer.Deserialize<MeetingMinutesContent>(content, JsonOptions));
        }
        catch (Exception exception) when (exception is not PermanentMeetingProcessingException)
        {
            throw new InvalidOperationException(
                $"OpenAI meeting minutes generation failed: {exception.Message}",
                exception);
        }
    }

    private static MeetingMinutesContent Validate(MeetingMinutesContent? minutes)
    {
        if (minutes is null || string.IsNullOrWhiteSpace(minutes.Title))
        {
            throw new PermanentMeetingProcessingException(
                "OpenAI meeting minutes response did not include a title.");
        }

        if (string.IsNullOrWhiteSpace(minutes.Summary))
        {
            throw new PermanentMeetingProcessingException(
                "OpenAI meeting minutes response did not include a summary.");
        }

        return minutes;
    }
}
