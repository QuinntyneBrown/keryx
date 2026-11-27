using Keryx.Application.Abstractions;
using Keryx.Domain.Staff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Keryx.Api.Session;

internal sealed class StaffTicketStore(IServiceScopeFactory scopes, IDataProtectionProvider protection,
    TimeProvider clock, IOptions<SessionLimits> limits) : ITicketStore
{
    private readonly IDataProtector _protector = protection.CreateProtector("Keryx.StaffSession.v1");
    public const string Expired = "Keryx.SessionExpired";

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var subject = ticket.Principal.FindFirst("sub")?.Value ?? throw new InvalidOperationException("Missing session subject.");
        var session = new StaffSession(Guid.NewGuid().ToString("N"), subject,
            _protector.Protect(TicketSerializer.Default.Serialize(ticket)), clock.GetUtcNow());
        db.StaffSessions.Add(session);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return session.Id;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => ReadAsync(key, null, CancellationToken.None);

    public Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext context, CancellationToken cancellationToken) =>
        ReadAsync(key, context, cancellationToken);

    private async Task<AuthenticationTicket?> ReadAsync(string key, HttpContext? context, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var session = await db.StaffSessions.FindAsync([key], cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                return null;
            }

            var now = clock.GetUtcNow();
            var expired = session.IsExpired(now, TimeSpan.FromMinutes(limits.Value.IdleMinutes), TimeSpan.FromHours(limits.Value.LifetimeHours));
            if (expired)
            {
                db.StaffSessions.Remove(session);
            }
            else
            {
                session.Seen(now);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A concurrent visit, expiry or sign-out won. Re-read its committed state.
                continue;
            }

            if (!expired)
            {
                return TicketSerializer.Default.Deserialize(_protector.Unprotect(session.ProtectedTicket));
            }

            if (context is not null)
            {
                context.Items[Expired] = true;
            }

            return null;
        }
    }

    // A second sign-in can arrive while the old session cookie is still present.
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var session = await db.StaffSessions.FindAsync(key).ConfigureAwait(false);
        if (session is null || session.SubjectId != ticket.Principal.FindFirst("sub")?.Value)
        {
            // An existing session id must never become a different person's credential.
            throw new InvalidOperationException("A new account requires a new session.");
        }

        session.ReplaceTicket(_protector.Protect(TicketSerializer.Default.Serialize(ticket)), clock.GetUtcNow());
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var session = await db.StaffSessions.FindAsync(key).ConfigureAwait(false);
        while (session is not null)
        {
            db.StaffSessions.Remove(session);
            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await ex.Entries.Single().ReloadAsync().ConfigureAwait(false);
                session = await db.StaffSessions.FindAsync(key).ConfigureAwait(false);
            }
        }
    }
}
