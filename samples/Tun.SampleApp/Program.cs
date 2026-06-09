var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/health"));

app.MapGet("/health", () => Results.Ok(new
{
    service = "Tun.SampleApp",
    status = "ok",
    utc = DateTimeOffset.UtcNow
}));

app.MapMethods("/echo/{**path}", ["GET", "POST", "PUT", "PATCH"], async (HttpContext context) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync(context.RequestAborted);

    return Results.Ok(new
    {
        method = context.Request.Method,
        path = context.Request.Path.Value,
        query = context.Request.QueryString.Value,
        bodyLength = body.Length,
        xTest = context.Request.Headers["X-Test"].ToString()
    });
});

app.Run();
