using System.Diagnostics;
using System.Security.Claims;
using Keryx.Application.Session;
using MediatR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Keryx.Api.Session;

internal static class StaffOidc
{
    /// <summary>Where the provider returns; it has no endpoint of its own, so it is also the refusal's route.</summary>
    private const string CallbackPath = "/admin/v1/auth/callback";

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
                options.CallbackPath = CallbackPath;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.MapInboundClaims = false;
                options.Events.OnRemoteFailure = context =>
                {
                    SignInFailed(context.HttpContext);
                    context.HandleResponse();
                    context.Response.Redirect($"/sign-in?state=error&ref={Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier}");
                    return Task.CompletedTask;
                };
                options.Scope.Add("profile");
                options.Events.OnTicketReceived = async context =>
                {
                    var identity = context.Principal;
                    var subject = identity?.FindFirst("sub")?.Value;
                    if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255)
                    {
                        SignInFailed(context.HttpContext);
                        context.HandleResponse();
                        context.Response.Redirect($"/sign-in?state=error&ref={context.HttpContext.TraceIdentifier}");
                        return;
                    }

                    var staffValue = settings.Value.StaffClaim;
                    var claims = new List<Claim> { new("sub", subject), new(ClaimTypes.NameIdentifier, subject) };
                    if (identity!.HasClaim("roles", staffValue))
                    {
                        var name = identity.FindFirst("name")?.Value ?? "Staff";
                        var role = identity.FindFirst("role")?.Value ?? "Staff";
                        var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();
                        await sender.Send(new RecordStaffSignIn(subject, name, role), context.HttpContext.RequestAborted).ConfigureAwait(false);
                        claims.Add(new Claim("roles", staffValue));
                    }

                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                };
            });
    }

    /// <summary>A sign-in the provider or the callback refused: counted and logged by route, never by its state or code (L2-085 AC3).</summary>
    private static void SignInFailed(HttpContext context) =>
        context.RequestServices.GetRequiredService<AccessRefusals>()
            .Record(StatusCodes.Status401Unauthorized, "signin_failed", CallbackPath.TrimStart('/'));

    private sealed class ProviderSettings
    {
        public string? Authority { get; set; }
        public string? ClientId { get; set; }
        public string StaffClaim { get; set; } = "KeryxStaff";
        public string? ClientSecret { get; set; }
    }
}
