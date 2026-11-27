using System.Security.Cryptography;
using Keryx.Application.Clients;
using Microsoft.Extensions.Hosting;

namespace Keryx.Infrastructure.Clients;

/// <summary>
/// <c>kx_live_</c> in production and <c>kx_test_</c> in every other environment, then 24
/// characters drawn from the 62 letters and digits by <see cref="RandomNumberGenerator"/> (about
/// 143 bits): URL-safe, and easy to select and copy whole.
/// </summary>
internal sealed class ApiKeyGenerator(IHostEnvironment environment) : IApiKeyGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int RandomLength = 24;

    public string NewKey() =>
        (environment.IsProduction() ? "kx_live_" : "kx_test_") + RandomNumberGenerator.GetString(Alphabet, RandomLength);
}
