using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Extensions;
using TTSmartEcom.Api.Realtime;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);
builder.Configuration.AddLegacyEnvironmentAliases();
builder.Services.AddTtsmartApi(builder.Configuration);
builder.Services.AddTtsmartSocketIoRealtime(builder.Configuration);

WebApplication app = builder.Build();
app.UseTtsmartPipeline();
app.Run();

public partial class Program;
