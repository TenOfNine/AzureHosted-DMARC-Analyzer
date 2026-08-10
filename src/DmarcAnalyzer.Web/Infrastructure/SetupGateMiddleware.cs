using DmarcAnalyzer.Core.Abstractions;

namespace DmarcAnalyzer.Web.Infrastructure;

/// <summary>
/// Redirects every request to the setup wizard until first-run configuration (Graph connection,
/// at least one domain, at least one mailbox, retention) is complete. This is what makes the same
/// deployed codebase usable for a fresh customer without any code change — everything tenant-specific
/// is captured through the wizard, not baked in at deploy time.
/// </summary>
public class SetupGateMiddleware(RequestDelegate next)
{
    // Once true, never re-checked — setup does not "un-complete" itself, so this avoids a DB
    // round-trip on every request for the lifetime of the process.
    private static volatile bool _setupComplete;

    public async Task InvokeAsync(HttpContext context, ISetupStateService setupStateService)
    {
        if (_setupComplete || context.Request.Path.StartsWithSegments("/Setup"))
        {
            await next(context);
            return;
        }

        _setupComplete = await setupStateService.IsSetupCompleteAsync(context.RequestAborted);
        if (!_setupComplete)
        {
            context.Response.Redirect("/Setup/Welcome");
            return;
        }

        await next(context);
    }
}
