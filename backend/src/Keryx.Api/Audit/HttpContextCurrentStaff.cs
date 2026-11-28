using Keryx.Application.Audit;

namespace Keryx.Api.Audit;

/// <summary>The staff member of the current request, from the session's <c>sub</c> claim.</summary>
internal sealed class HttpContextCurrentStaff(IHttpContextAccessor accessor) : ICurrentStaff
{
    public string SubjectId =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("A staff action was recorded outside a staff session.");
}
