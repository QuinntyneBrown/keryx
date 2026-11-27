using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Keryx.Application.Sources;
using Keryx.Domain.Sources;

namespace Keryx.Infrastructure.Sources;

/// <summary>
/// Reads RSS 2.0 and Atom feeds (L2-023). No DTD is processed and no entity or external resource
/// is resolved. An item whose link is not http or https is rejected; a feed of more than 200
/// items keeps the 200 newest.
/// </summary>
internal sealed class FeedParser : IFeedParser
{
    public const int MaxItems = 200;
    public const int MaxTitleLength = 300;
    public const int MaxSummaryLength = 2000;

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public ParsedFeed? Parse(SourceKind kind, string content) =>
        kind == SourceKind.Rss ? ParseFeed(content) : null;

    private static ParsedFeed? ParseFeed(string content)
    {
        XDocument document;
        try
        {
            using var text = new StringReader(content);
            using var reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        var root = document.Root;
        IEnumerable<(string? Title, string? Link, string? Summary, string? Date)> entries = root?.Name.LocalName switch
        {
            "rss" => root.Element("channel")?.Elements("item").Select(item => (
                item.Element("title")?.Value,
                item.Element("link")?.Value,
                item.Element("description")?.Value,
                item.Element("pubDate")?.Value)) ?? [],
            "feed" when root.Name.Namespace == Atom => root.Elements(Atom + "entry").Select(entry => (
                entry.Element(Atom + "title")?.Value,
                (entry.Elements(Atom + "link").FirstOrDefault(link => (string?)link.Attribute("rel") is null or "alternate") ?? entry.Element(Atom + "link"))?.Attribute("href")?.Value,
                (entry.Element(Atom + "summary") ?? entry.Element(Atom + "content"))?.Value,
                (entry.Element(Atom + "updated") ?? entry.Element(Atom + "published"))?.Value)),
            _ => null!,
        };
        if (entries is null)
        {
            return null;
        }

        var items = new List<FeedItem>();
        var rejected = 0;
        foreach (var (title, link, summary, date) in entries)
        {
            if (!Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                rejected++;
                continue;
            }

            items.Add(new FeedItem(
                TextSanitizer.PlainText(title, MaxTitleLength),
                uri,
                TextSanitizer.PlainText(summary, MaxSummaryLength),
                DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var published) ? published : null));
        }

        // The newest first; an item with no date keeps its place after the dated ones.
        var kept = items
            .Select((item, index) => (item, index))
            .OrderByDescending(entry => entry.item.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(entry => entry.index)
            .Take(MaxItems)
            .Select(entry => entry.item)
            .ToList();
        return new ParsedFeed(kept, rejected);
    }
}
