using System.Text.Json;
using MeetingMind.Application.Common.Interfaces;
using MeetingMind.Application.Common.Options;
using MeetingMind.Application.Meetings;
using OpenAI.Chat;

namespace MeetingMind.Infrastructure.OpenAI;

public class OpenAiMeetingMinutesService : IMeetingMinutesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ChatClient _chatClient;
    private readonly OpenAiOptions _openAiOptions;

    public OpenAiMeetingMinutesService(OpenAiOptions openAiOptions)
    {
        _openAiOptions = openAiOptions;

        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is required. Configure OpenAI:ApiKey or OPENAI_API_KEY.");
        }

        if (string.IsNullOrWhiteSpace(_openAiOptions.Model))
        {
            throw new InvalidOperationException("OpenAI model is required.");
        }

        _chatClient = new ChatClient(_openAiOptions.Model, _openAiOptions.ApiKey);
    }

    public async Task<MeetingMinutesContent> GenerateMinutesAsync(
        string transcriptText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            throw new InvalidOperationException("Transcript is empty; meeting minutes cannot be generated.");
        }

        if (transcriptText.Length > _openAiOptions.MaxTranscriptCharactersForMinutes)
        {
            throw new InvalidOperationException(
                "Transcript is too long for single-pass meeting minutes generation. Chunking is not implemented yet.");
        }

        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You generate concise, structured meeting minutes from transcripts. " +
                    "Use only facts present in the transcript. If a field is not identifiable, return an empty array."),
                new UserChatMessage(
                    "Generate structured meeting minutes for this transcript:\n\n" + transcriptText)
            };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "meeting_minutes",
                    jsonSchema: BinaryData.FromBytes("""
                        {
                          "type": "object",
                          "properties": {
                            "title": { "type": "string" },
                            "summary": { "type": "string" },
                            "attendees": {
                              "type": "array",
                              "items": { "type": "string" }
                            },
                            "discussionPoints": {
                              "type": "array",
                              "items": { "type": "string" }
                            },
                            "decisions": {
                              "type": "array",
                              "items": { "type": "string" }
                            },
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
                            "risks": {
                              "type": "array",
                              "items": { "type": "string" }
                            },
                            "nextSteps": {
                              "type": "array",
                              "items": { "type": "string" }
                            }
                          },
                          "required": [
                            "title",
                            "summary",
                            "attendees",
                            "discussionPoints",
                            "decisions",
                            "actionItems",
                            "risks",
                            "nextSteps"
                          ],
                          "additionalProperties": false
                        }
                        """u8.ToArray()),
                    jsonSchemaIsStrict: true)
            };

            var completion = await _chatClient.CompleteChatAsync(messages, options, cancellationToken);
            var json = completion.Value.Content[0].Text;
            var minutes = JsonSerializer.Deserialize<MeetingMinutesContent>(json, JsonOptions);

            return Validate(minutes);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"OpenAI meeting minutes generation failed: {exception.Message}",
                exception);
        }
    }

    private static MeetingMinutesContent Validate(MeetingMinutesContent? minutes)
    {
        if (minutes is null)
        {
            throw new InvalidOperationException("OpenAI meeting minutes response was empty or invalid.");
        }

        if (string.IsNullOrWhiteSpace(minutes.Title))
        {
            throw new InvalidOperationException("OpenAI meeting minutes response did not include a title.");
        }

        if (string.IsNullOrWhiteSpace(minutes.Summary))
        {
            throw new InvalidOperationException("OpenAI meeting minutes response did not include a summary.");
        }

        return minutes;
    }
}
