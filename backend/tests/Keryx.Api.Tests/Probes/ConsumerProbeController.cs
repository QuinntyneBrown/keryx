using Keryx.Api.Consumer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace Keryx.Api.Tests.Probes;

/// <summary>
/// Consumer endpoints that exist only in the tests' host: one that fails with a 500 and one that
/// takes as long as asked, by moving the fixture's clock, so usage can be counted through the real
/// pipeline.
/// </summary>
[Authorize(Policy = ApiKeyDefaults.AnyClient)]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("v1/probe")]
public sealed class ConsumerProbeController(TimeProvider time) : ControllerBase
{
    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException($"The probe failed ({Request.Method}).");

    [HttpGet("slow/{milliseconds:int}")]
    public IActionResult Slow(int milliseconds)
    {
        ((FakeTimeProvider)time).Advance(TimeSpan.FromMilliseconds(milliseconds));
        return Ok(new ProbeItem(milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
