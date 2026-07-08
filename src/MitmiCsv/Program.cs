using System.Text;
using Microsoft.AspNetCore.Hosting;
using MitmiCsv;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey)))
{
    builder.WebHost.UseUrls("http://localhost:8080");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IModbusReader, NModbusTcpReader>();

var app = builder.Build();

app.MapGet("/", () => Results.Text(
    "mitmi-csv\nGET /read?host=<host>&unit=<unit>&fc=<1|2|3|4>&address=<zero-based>&count=<count>\n",
    "text/plain",
    Encoding.UTF8));

app.MapGet("/read", ReadEndpoint.HandleAsync);

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
}
