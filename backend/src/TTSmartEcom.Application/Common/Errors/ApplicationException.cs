namespace TTSmartEcom.Application.Common.Errors;

public sealed class ApplicationException : Exception
{
    public ApplicationException(ApplicationError error, Exception? innerException = null)
        : base(error.ClientMessage, innerException)
    {
        Error = error;
    }

    public ApplicationError Error { get; }
}
