using Keryx.Application.Catalogue;

namespace Keryx.Application.Tests.Catalogue;

// Unit Test
// Traces to: L2-054 AC4
// Description: Every value is quoted per RFC 4180, with its quotes doubled, and a value that
// begins with =, +, -, @, tab or carriage return is prefixed with a single quote so a
// spreadsheet does not run it as a formula. A missing value is an empty quoted cell.
public sealed class CsvCellTests
{
    [Theory]
    [InlineData("Running clinic", "\"Running clinic\"")]
    [InlineData("", "\"\"")]
    [InlineData(null, "\"\"")]
    [InlineData("Grace, Barbell", "\"Grace, Barbell\"")]
    [InlineData("The \"Fit\" Church", "\"The \"\"Fit\"\" Church\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1 race", "\"'+1 race\"")]
    [InlineData("-5K", "\"'-5K\"")]
    [InlineData("@home", "\"'@home\"")]
    [InlineData("\tTab", "\"'\tTab\"")]
    [InlineData("\rReturn", "\"'\rReturn\"")]
    [InlineData("a=b", "\"a=b\"")]
    [InlineData(" =spaced", "\" =spaced\"")]
    [InlineData("'quoted", "\"'quoted\"")]
    public void A_value_is_written_quoted_and_guarded(string? value, string cell) =>
        Assert.Equal(cell, CsvCell.Escape(value));
}
