using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Ticket.Application.Abstractions;
using Ticket.Web.Setup;

namespace Ticket.Web.Auth;

public sealed class SetupAwareCookieEvents : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var setup = context.HttpContext.RequestServices.GetRequiredService<ISetupState>();

        // DB yoksa / setup değilse: DB'ye dokunmadan cookie'yi iptal et
        if (!setup.IsConfigured)
        {
            context.RejectPrincipal();

            // Identity application cookie'yi temizle
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        // Setup OK ise normal davranış (security stamp validation)
        try
        {
            await SecurityStampValidator.ValidatePrincipalAsync(context);
        }
        catch
        {
            // DB bir anda giderse yine patlamasın, cookie'yi düşür
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }
}
