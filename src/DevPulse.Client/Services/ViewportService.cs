using DevPulse.Shared.Constants;
using Microsoft.JSInterop;

namespace DevPulse.Client.Services;

public interface IViewportService
{
    Task<bool> IsMobileAsync();
}

public sealed class ViewportService : IViewportService
{
    private readonly IJSRuntime _js;

    public ViewportService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> IsMobileAsync()
    {
        try
        {
            var module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/layout.js");
            try
            {
                return await module.InvokeAsync<bool>("isMobileViewport");
            }
            finally
            {
                await module.DisposeAsync();
            }
        }
        catch (JSException)
        {
            return false;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

public static class AppLanding
{
    public const string Dashboard = "dashboard";
    public const string Attendance = "attendance";

    public static string DefaultPath(string? role, bool isMobile) =>
        isMobile || role != AppRoles.Admin ? Attendance : Dashboard;

    public static bool IsRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "login")
        {
            return true;
        }

        var value = path.Trim();
        var queryIndex = value.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            value = value[..queryIndex];
        }

        return value is "" or "/";
    }
}
