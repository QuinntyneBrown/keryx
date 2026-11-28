using System.Text;
using Keryx.Application.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Keryx.Api.Catalogue;

/// <summary>
/// Writes an export to the response as it reads it (L2-054 AC2): the header row, then each row
/// with every value escaped by <see cref="CsvCell"/>, records ending in CRLF, as the rows are read.
/// </summary>
public sealed class ArticleCsvResult(ArticleExport export) : IActionResult
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var response = context.HttpContext.Response;
        var cancellationToken = context.HttpContext.RequestAborted;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = export.FileName }.ToString();

        await using var writer = new StreamWriter(response.Body, Utf8, leaveOpen: true);
        await WriteRecordAsync(writer, ArticleExportRowDto.Columns).ConfigureAwait(false);
        // The writer hands each full buffer to the body, so rows leave as they are read.
        await foreach (var row in export.Rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            await WriteRecordAsync(writer, row.Cells()).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteRecordAsync(StreamWriter writer, IEnumerable<string?> values)
    {
        await writer.WriteAsync(string.Join(',', values.Select(CsvCell.Escape))).ConfigureAwait(false);
        await writer.WriteAsync("\r\n").ConfigureAwait(false);
    }
}
