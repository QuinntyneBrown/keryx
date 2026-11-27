using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Keryx.Api.Session;

internal static class StaffOidc
{
    public static void AddStaffOidc(this IServiceCollection services)
    {
        services.AddOptions<ProviderSettings>().BindConfiguration("Keryx:Oidc")
            .Validate(settings => Uri.TryCreate(settings.Authority, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps, "Missing or invalid HTTPS setting: Keryx__Oidc__Authority")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ClientId), "Missing required setting: Keryx__Oidc__ClientId")
            .ValidateOnStart();
        services.AddAuthentication().AddOpenIdConnect();
        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<ProviderSettings>>((options, settings) =>
            {
                options.Authority = settings.Value.Authority;
                options.ClientId = settings.Value.ClientId;
                options.ClientSecret = settings.Value.ClientSecret;
                options.CallbackPath = "/admin/v1/auth/callback";
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect($"/sign-in?state=error&ref={Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier}");
                    return Task.CompletedTask;
                };
                options.Events.OnTicketReceived = context =>
                {
                    // Never issue a client-side identity ticket. Session creation needs the server-side store.
                    var cookies = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
                    if (cookies.Get(CookieAuthenticationDefaults.AuthenticationScheme).SessionStore is null)
                    {
                        context.HandleResponse();
                        context.Response.Redirect($"/sign-in?state=error&ref={context.HttpContext.TraceIdentifier}");
                    }

                    return Task.CompletedTask;
                };
            });
    }

    private sealed class ProviderSettings
    {
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
    }
}
