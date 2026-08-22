using R2WAI.Infrastructure.AI.DynamicTools;

namespace R2WAI.Infrastructure.Tests.AI;

/// <summary>
/// Covers DynamicToolFunctionFactory.SanitizeFunctionName — the pure mapping from an admin-entered
/// ToolDefinition.Name (free text) to a valid Semantic Kernel function name (letters/digits/underscore
/// only). Kept free of Kernel/EF types so it doesn't need a live database to test.
/// </summary>
public class DynamicToolFunctionFactoryTests
{
    [Theory]
    [InlineData("Get Property Tax Status", "Get_Property_Tax_Status")]
    [InlineData("lookup-invoice.by#id", "lookup_invoice_by_id")]
    [InlineData("already_valid_name", "already_valid_name")]
    public void SanitizeFunctionName_ReplacesInvalidCharsWithUnderscore(string input, string expected)
    {
        Assert.Equal(expected, DynamicToolFunctionFactory.SanitizeFunctionName(input));
    }

    [Fact]
    public void SanitizeFunctionName_LeadingDigit_GetsPrefixed()
    {
        var result = DynamicToolFunctionFactory.SanitizeFunctionName("123 Lookup");
        Assert.False(char.IsDigit(result[0]));
        Assert.StartsWith("tool_", result);
    }

    [Fact]
    public void SanitizeFunctionName_WhitespaceOnly_GetsPrefixed()
    {
        // Regex.Replace substitutes each invalid character with '_' rather than removing it, so a
        // name of only invalid characters (e.g. "###") sanitizes to "___", not "". Only a name
        // that's empty (or whitespace-only, after Trim()) actually hits the empty-name guard.
        var result = DynamicToolFunctionFactory.SanitizeFunctionName("   ");
        Assert.Equal("tool_", result);
    }

    [Fact]
    public void SanitizeFunctionName_LongName_TruncatedTo64Chars()
    {
        var longName = new string('a', 100);
        var result = DynamicToolFunctionFactory.SanitizeFunctionName(longName);
        Assert.Equal(64, result.Length);
    }
}
