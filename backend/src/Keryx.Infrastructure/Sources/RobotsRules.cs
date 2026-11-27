namespace Keryx.Infrastructure.Sources;

/// <summary>
/// The rules of one site's robots.txt for Keryx (L2-021): the group naming Keryx if there is one,
/// otherwise the group for <c>*</c>. The longest matching rule wins and Allow wins a tie, as RFC
/// 9309 says; <c>*</c> and <c>$</c> in a rule are honoured.
/// </summary>
internal sealed class RobotsRules
{
    public static readonly RobotsRules AllowAll = new([], null);

    private readonly IReadOnlyList<(bool Allow, string Pattern)> _rules;

    private RobotsRules(IReadOnlyList<(bool Allow, string Pattern)> rules, TimeSpan? crawlDelay)
    {
        _rules = rules;
        CrawlDelay = crawlDelay;
    }

    public TimeSpan? CrawlDelay { get; }

    public static RobotsRules Parse(string text, string agent)
    {
        var groups = new List<(List<string> Agents, List<(bool, string)> Rules, TimeSpan? Delay)>();
        (List<string> Agents, List<(bool, string)> Rules, TimeSpan? Delay)? current = null;
        var lastWasAgent = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#', 2)[0].Trim();
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var field = line[..colon].Trim().ToUpperInvariant();
            var value = line[(colon + 1)..].Trim();
            if (field == "USER-AGENT")
            {
                if (current is null || !lastWasAgent)
                {
                    current = ([], [], null);
                    groups.Add(current.Value);
                }

                current.Value.Agents.Add(value);
                lastWasAgent = true;
                continue;
            }

            lastWasAgent = false;
            if (current is not { } group)
            {
                continue;
            }

            switch (field)
            {
                case "ALLOW" when value.Length > 0:
                    group.Rules.Add((true, value));
                    break;
                case "DISALLOW" when value.Length > 0:
                    group.Rules.Add((false, value));
                    break;
                case "CRAWL-DELAY" when double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0:
                    var index = groups.FindIndex(candidate => ReferenceEquals(candidate.Agents, group.Agents));
                    groups[index] = (group.Agents, group.Rules, TimeSpan.FromSeconds(Math.Min(seconds, 30)));
                    current = groups[index];
                    break;
            }
        }

        var named = groups.Where(group => group.Agents.Any(name => name.Equals(agent, StringComparison.OrdinalIgnoreCase))).ToList();
        var chosen = named.Count > 0 ? named : groups.Where(group => group.Agents.Contains("*")).ToList();
        return new RobotsRules(chosen.SelectMany(group => group.Rules).ToList(), chosen.Select(group => group.Delay).Max());
    }

    public bool Allows(string pathAndQuery)
    {
        var best = (Allow: true, Length: -1);
        foreach (var (allow, pattern) in _rules)
        {
            if (Matches(pattern, pathAndQuery) && (pattern.Length > best.Length || (pattern.Length == best.Length && allow)))
            {
                best = (allow, pattern.Length);
            }
        }

        return best.Allow;
    }

    private static bool Matches(string pattern, string path)
    {
        var anchored = pattern.EndsWith('$');
        var parts = (anchored ? pattern[..^1] : pattern).Split('*');
        if (!path.StartsWith(parts[0], StringComparison.Ordinal))
        {
            return false;
        }

        var position = parts[0].Length;
        for (var i = 1; i < parts.Length; i++)
        {
            var found = path.IndexOf(parts[i], position, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            position = found + parts[i].Length;
        }

        return !anchored || position == path.Length || (parts.Length > 1 && path.EndsWith(parts[^1], StringComparison.Ordinal));
    }
}
