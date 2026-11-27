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

    public const int MaxPlaceLength = 200;

    private const int MaxTimeLength = 40;

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    /// <summary>The RSS 1.0 event module: an item's start, end and location.</summary>
    private static readonly XNamespace Ev = "http://purl.org/rss/1.0/modules/event/";

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
        IEnumerable<(string? Title, string? Link, string? Summary, string? Date, ItemEvent? Event)> entries = root?.Name.LocalName switch
        {
            "rss" => root.Element("channel")?.Elements("item").Select(item => (
                item.Element("title")?.Value,
                item.Element("link")?.Value,
                item.Element("description")?.Value,
                item.Element("pubDate")?.Value,
                EventOf(item))) ?? [],
            "feed" when root.Name.Namespace == Atom => root.Elements(Atom + "entry").Select(entry => (
                entry.Element(Atom + "title")?.Value,
                (entry.Elements(Atom + "link").FirstOrDefault(link => (string?)link.Attribute("rel") is null or "alternate") ?? entry.Element(Atom + "link"))?.Attribute("href")?.Value,
                (entry.Element(Atom + "summary") ?? entry.Element(Atom + "content"))?.Value,
                (entry.Element(Atom + "updated") ?? entry.Element(Atom + "published"))?.Value,
                (ItemEvent?)null)),
            _ => null!,
        };
        if (entries is null)
        {
            return null;
        }

        var items = new List<FeedItem>();
        var rejected = 0;
        foreach (var (title, link, summary, date, itemEvent) in entries)
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
                DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var published) ? published : null,
                itemEvent));
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

    /// <summary>An item's event, when it publishes a start time; its end and place may be empty.</summary>
    private static ItemEvent? EventOf(XElement item)
    {
        var start = TextSanitizer.PlainText(item.Element(Ev + "startdate")?.Value, MaxTimeLength);
        return start.Length == 0
            ? null
            : new ItemEvent(
                start,
                TextSanitizer.PlainText(item.Element(Ev + "enddate")?.Value, MaxTimeLength),
                TextSanitizer.PlainText(item.Element(Ev + "location")?.Value, MaxPlaceLength));
    }
}
