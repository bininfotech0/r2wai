using R2WAI.Domain.Common;

namespace R2WAI.Domain.ValueObjects;

public sealed class Citation : ValueObject
{
    public string SourceId { get; }
    public string SourceName { get; }
    public int? PageNumber { get; }
    public string? Text { get; }
    public double Relevance { get; }

    public Citation(string sourceId, string sourceName, double relevance,
                    int? pageNumber = null, string? text = null)
    {
        SourceId = sourceId;
        SourceName = sourceName;
        Relevance = relevance;
        PageNumber = pageNumber;
        Text = text;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceId;
        yield return SourceName;
        yield return PageNumber;
        yield return Text;
        yield return Relevance;
    }
}
