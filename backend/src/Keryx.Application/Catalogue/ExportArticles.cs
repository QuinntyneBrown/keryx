using System.Globalization;
using System.Text;
using FluentValidation;
using Keryx.Application.Abstractions;
using Keryx.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Keryx.Application.Catalogue;

/// <summary>
/// A row of the export (L2-054 AC2): facts and a link, never a copy of a story. Text came from a
/// source; it is written through <see cref="CsvCell"/>.
/// </summary>
public sealed record ArticleExportRowDto(
    string Id, string? Title, string? Organization, string Category, string Region, decimal Relevance, string Status,
    DateTimeOffset? PublishedAt, DateTimeOffset CollectedAt, string Url)
{
    /// <summary>The header row's names, in the order of <see cref="Cells"/>.</summary>
    public static readonly IReadOnlyList<string> Columns =
        ["id", "title", "organization", "category", "region", "relevance", "status", "published_at", "collected_at", "url"];

    /// <summary>The row's values as text: relevance with two decimals, times in UTC, a missing value empty.</summary>
    public IReadOnlyList<string?> Cells() =>
    [
        Id, Title, Organization, Category, Region, Relevance.ToString("0.00", CultureInfo.InvariantCulture), Status,
        Time(PublishedAt), Time(CollectedAt), Url,
    ];

    private static string? Time(DateTimeOffset? at) =>
        at?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>The export's file name and its rows, read as they are written.</summary>
public sealed record ArticleExport(string FileName, IAsyncEnumerable<ArticleExportRowDto> Rows);

/// <summary>Every article the staff list would show for this filter, newest collected first, as a CSV file (L2-054).</summary>
public sealed record ExportArticles(string? Status, string? Q, string? Region, string? Category) : IRequest<ArticleExport>, IAdminArticleFilter;

internal sealed class ExportArticlesValidator : AbstractValidator<ExportArticles>
{
    public ExportArticlesValidator(IAppDbContext db) => Include(new AdminArticleFilterValidator(db));
}

/// <summary>
/// Narrows the articles as the list does and returns their rows unread: the caller streams them,
/// so the whole file is never built in memory. The file is named for today's date in Toronto.
/// </summary>
internal sealed class ExportArticlesHandler(IAppDbContext db, TimeProvider time) : IRequestHandler<ExportArticles, ArticleExport>
{
    public Task<ArticleExport> Handle(ExportArticles request, CancellationToken cancellationToken)
    {
        var today = TimeZoneInfo.ConvertTime(time.GetUtcNow(), TorontoTime.Zone);
        var rows = AdminArticleFilter.Apply(db, request)
            .OrderByDescending(article => article.CollectedAt).ThenByDescending(article => article.Id)
            .Select(article => new
            {
                article.PublicId,
                article.Title,
                Organization = article.Organization == null ? null : article.Organization.Name,
                article.Category,
                article.RegionSlug,
                article.Score,
                article.Status,
                article.PublishedAt,
                article.CollectedAt,
                article.Url,
            })
            .AsAsyncEnumerable()
            .Select(row => new ArticleExportRowDto(
                row.PublicId, row.Title, row.Organization, row.Category, row.RegionSlug, row.Score,
                GetAdminArticleHandler.StatusWord(row.Status), row.PublishedAt, row.CollectedAt, row.Url));
        var name = string.Create(CultureInfo.InvariantCulture, $"articles-{today:yyyy-MM-dd}.csv");
        return Task.FromResult(new ArticleExport(name, rows));
    }
}

/// <summary>
/// A CSV cell per RFC 4180 (L2-054 AC4): every value quoted, its quotes doubled. A value that
/// begins with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return is prefixed with a
/// single quote, so a spreadsheet does not run it as a formula.
/// </summary>
public static class CsvCell
{
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

    public static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        var cell = new StringBuilder(text.Length + 3).Append('"');
        if (text.Length > 0 && FormulaStarts.Contains(text[0]))
        {
            cell.Append('\'');
        }

        return cell.Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"').ToString();
    }
}
