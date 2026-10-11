using System.Text.RegularExpressions;
using R2WAI.Application.Features.Assistants.Commands;

namespace R2WAI.Application.Common.AI;

/// <summary>
/// Knowledge-base excerpts as model context, each labelled [n] to match CitationDto.Index, plus the
/// instruction to mark sentences with [^n]. Only for surfaces that render those markers (the embed
/// widget turns them into footnotes); plain-text channels like the webhook would show them raw.
/// </summary>
public static partial class CitationContext
{
    /// <summary>
    /// Removes [^n] markers, e.g. before a reply is stored as session history: the numbers refer to
    /// that turn's excerpts only, and a later turn would read them as citing its own.
    /// </summary>
    public static string StripMarkers(string reply) => Marker().Replace(reply, string.Empty);

    [GeneratedRegex(@"\[\^\d{1,3}\]")]
    private static partial Regex Marker();

    public static string Format(IReadOnlyList<CitationDto> citations)
    {
        var excerpts = citations.Select(c => $"[{c.Index}] (source: {c.SourceName})\n{c.Content}");
        return "When a sentence of your answer relies on excerpt [n] below, end that sentence with the marker [^n], "
            + "for example \"Fees are waived for senior citizens.[^2]\". Use only the numbers shown here and never invent one.\n\n"
            + string.Join("\n\n", excerpts);
    }
}
