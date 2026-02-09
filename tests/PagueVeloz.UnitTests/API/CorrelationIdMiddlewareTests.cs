using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PagueVeloz.API.Middleware;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace PagueVeloz.UnitTests.API;

public class CorrelationIdMiddlewareTests : IDisposable
{
    private readonly InMemoryCorrelationSink _sink;

    public CorrelationIdMiddlewareTests()
    {
        _sink = new InMemoryCorrelationSink();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Sink(_sink)
            .CreateLogger();
    }

    public void Dispose()
    {
        Log.CloseAndFlush();
    }

    [Fact]
    public async Task InvokeAsync_WithNoHeader_ShouldGenerateCorrelationId()
    {
        var context = new DefaultHttpContext();
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"]?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedCorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(capturedCorrelationId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithExistingHeader_ShouldUseProvidedCorrelationId()
    {
        var context = new DefaultHttpContext();
        var expectedId = "my-custom-correlation-id";
        context.Request.Headers["X-Correlation-Id"] = expectedId;
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"]?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedCorrelationId.Should().Be(expectedId);
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetCorrelationIdInHttpContextItems()
    {
        var context = new DefaultHttpContext();

        var middleware = new CorrelationIdMiddleware(next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("CorrelationId");
        context.Items["CorrelationId"]!.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_ShouldEnrichSerilogLogContextWithCorrelationId()
    {
        var context = new DefaultHttpContext();
        var expectedId = "trace-me-123";
        context.Request.Headers["X-Correlation-Id"] = expectedId;

        var middleware = new CorrelationIdMiddleware(next: _ =>
        {
            Log.Information("Inside middleware pipeline");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var logEvent = _sink.LogEvents
            .FirstOrDefault(e => e.MessageTemplate.Text == "Inside middleware pipeline");

        logEvent.Should().NotBeNull();
        logEvent!.Properties.Should().ContainKey("CorrelationId");
        logEvent.Properties["CorrelationId"].ToString().Should().Contain("trace-me-123");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyHeader_ShouldGenerateNewCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "";
        string? capturedCorrelationId = null;

        var middleware = new CorrelationIdMiddleware(next: ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"]?.ToString();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        capturedCorrelationId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(capturedCorrelationId, out _).Should().BeTrue();
    }
}

public sealed class InMemoryCorrelationSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    private readonly object _lock = new();

    public IReadOnlyList<LogEvent> LogEvents
    {
        get
        {
            lock (_lock)
                return _events.ToList();
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
            _events.Add(logEvent);
    }
}
