namespace Keryx.Application.Errors;

/// <summary>A page before the first, or a page size outside 1 to 100, on an admin list (L2-048 AC5).</summary>
public sealed class InvalidPageException() : KeryxException(400, "invalid_page", "The page or page size is out of range.")
{
    public const int MaxPageSize = 100;

    public static void ThrowIfOutOfRange(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            throw new InvalidPageException();
        }
    }
}
