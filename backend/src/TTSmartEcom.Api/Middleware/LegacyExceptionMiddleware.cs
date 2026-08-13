using MongoDB.Driver;
using TTSmartEcom.Application.Common.Errors;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.Api.Middleware;

public sealed partial class LegacyExceptionMiddleware(RequestDelegate next, ILogger<LegacyExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (TtsApplicationException exception)
        {
            await WriteErrorAsync(context, exception.Error, exception);
        }
        catch (FormatException exception)
        {
            await WriteErrorAsync(context, new ApplicationError("TTS-API-0001", 1001, 400, "Invalid request"), exception);
        }
        catch (MongoException exception)
        {
            await WriteErrorAsync(context, new ApplicationError("TTS-MONGO-0001", 9001, 503, "Service unavailable", true), exception);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(context, new ApplicationError("TTS-API-0000", 1000, 500, "Internal server error"), exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, ApplicationError error, Exception exception)
    {
        LogRequestFailure(logger, exception, error.Code, error.EventId);
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = error.HttpStatus;
        context.Response.Headers["X-Error-Code"] = error.Code;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = error.ClientMessage });
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Request failed with {ErrorCode} (EventId {EventId})")]
    private static partial void LogRequestFailure(ILogger logger, Exception exception, string errorCode, int eventId);
}
