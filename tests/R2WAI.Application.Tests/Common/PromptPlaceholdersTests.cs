using R2WAI.Application.Common.AI;

namespace R2WAI.Application.Tests.Common;

public class PromptPlaceholdersTests
{
    private static readonly Dictionary<string, string> Values = new()
    {
        [PromptPlaceholders.TenantName] = "Acme",
        [PromptPlaceholders.WorkspaceName] = "Employee Widget",
        [PromptPlaceholders.UserName] = "Guest",
        [PromptPlaceholders.UserRole] = "Guest",
        [PromptPlaceholders.CurrentDate] = "2026-10-09",
    };

    [Fact]
    public void Render_FillsEveryKnownPlaceholder()
    {
        var template = "Tenant: {{tenant.name}}\nWorkspace: {{workspace.name}}\nUser: {{user.name}} ({{user.role}})\nDate: {{current_date}}";

        var rendered = PromptPlaceholders.Render(template, Values);

        Assert.Equal("Tenant: Acme\nWorkspace: Employee Widget\nUser: Guest (Guest)\nDate: 2026-10-09", rendered);
    }

    [Fact]
    public void Render_ToleratesInnerWhitespaceAndCase()
    {
        Assert.Equal("Acme / Acme", PromptPlaceholders.Render("{{ tenant.name }} / {{Tenant.Name}}", Values));
    }

    [Fact]
    public void Render_LeavesUnknownPlaceholdersAndPlainBracesAlone()
    {
        const string template = "Use {{ticket.id}} and JSON like {\"a\": 1}.";

        Assert.Equal(template, PromptPlaceholders.Render(template, Values));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("No placeholders here.", false)]
    [InlineData("Hello {{user.name}}", true)]
    public void HasPlaceholders_DetectsTemplateSyntax(string? template, bool expected)
    {
        Assert.Equal(expected, PromptPlaceholders.HasPlaceholders(template));
    }
}
