namespace TTSmartEcom.Application.Common.Errors;

public sealed record ApplicationError(
    string Code,
    int EventId,
    int HttpStatus,
    string ClientMessage,
    bool Retryable = false);
