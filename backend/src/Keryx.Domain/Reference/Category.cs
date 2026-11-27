namespace Keryx.Domain.Reference;

/// <summary>One of the six fixed topics an article is classified under (L2-015).</summary>
public sealed class Category
{
    public Category(string slug, string name, int rank)
    {
        Slug = slug;
        Name = name;
        Rank = rank;
    }

    /// <summary>The stable key (<c>races-challenges</c>).</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    /// <summary>The place in the list: a lower rank comes first.</summary>
    public int Rank { get; private set; }
}
