using System.Diagnostics;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PagueVeloz.API.Extensions;
using PagueVeloz.API.Middleware;
using PagueVeloz.Application;
using PagueVeloz.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console());

    builder.Services.AddApiServices();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.MapControllers();
    app.MapHealthChecks("/health");

    if (app.Environment.IsDevelopment())
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var url = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
                var swaggerUrl = $"{url}/swagger";
                Process.Start(new ProcessStartInfo(swaggerUrl) { UseShellExecute = true });
            }
            catch
            {
                // no browser available...
            }
        });
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
