using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using XArtSkinEditor.Core;

namespace XArtSkinEditor.Mcp;

/// <summary>Streamable-HTTP MCP server living inside the editor process, bound to loopback only. Off until started.</summary>
public sealed class McpHost : IAsyncDisposable
{
    public const int DefaultPort = 5199;

    private WebApplication? _app;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; } = DefaultPort;
    public string Url => $"http://127.0.0.1:{Port}/mcp";
    public string? Error { get; private set; }

    /// <summary>Raised after the running state changes (may come from a background thread).</summary>
    public event Action? StateChanged;

    public async Task<bool> StartAsync(Workspace workspace, Palette palette, int port)
    {
        if (_app is not null) return true;
        Error = null;
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
            builder.Services.AddSingleton(workspace);
            builder.Services.AddSingleton(palette);
            builder.Services.AddMcpServer()
                .WithHttpTransport()
                .WithTools<SkinTools>();

            app = builder.Build();
            app.MapMcp("/mcp");
            await app.StartAsync();
            _app = app;
            Port = port;
            StateChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Error = IsAddressInUse(ex)
                ? Loc.T("mcp.busy", port)
                : ex.InnerException?.Message ?? ex.Message;
            if (app is not null)
            {
                try { await app.DisposeAsync(); } catch { /* already failed */ }
            }
            StateChanged?.Invoke();
            return false;
        }
    }

    private static bool IsAddressInUse(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
            if (ex is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse })
                return true;
        return false;
    }

    public async Task StopAsync()
    {
        var app = _app;
        if (app is null) return;
        _app = null;
        Error = null;
        try
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
