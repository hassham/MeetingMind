namespace MeetingMind.Application.Common.Failures;

public static class MeetingErrorCodes
{
    public const string UploadValidation = "upload_validation";
    public const string Configuration = "processing_configuration";
    public const string ProviderRejected = "provider_rejected";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string DatabaseUnavailable = "database_unavailable";
    public const string DatabaseFailure = "database_failure";
    public const string RequiredResourceMissing = "required_resource_missing";
    public const string ResourceAccessDenied = "resource_access_denied";
    public const string StorageFull = "storage_full";
    public const string TemporaryInterruption = "temporary_interruption";
    public const string StorageUnavailable = "storage_unavailable";
    public const string UnsupportedMedia = "unsupported_media";
    public const string InvalidInput = "invalid_input";
    public const string InvalidProviderResponse = "invalid_provider_response";
    public const string UnexpectedFailure = "unexpected_failure";
}
