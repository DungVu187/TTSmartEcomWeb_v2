namespace TTSmartEcom.Api.Middleware;

public sealed class ApiPrefixCompatibilityMiddleware(RequestDelegate next)
{
    public const string PrefixedItemKey = "ApiPrefixRequest";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/api" || context.Request.Path.StartsWithSegments("/api"))
        {
            context.Items[PrefixedItemKey] = true;
            PathString remainder = context.Request.Path.Value!.Length == 4
                ? "/"
                : context.Request.Path.Value[4..];
            context.Request.Path = new PathString(remainder);
        }

        await next(context);
    }
}
