namespace R2WAI.Infrastructure.AI.Prompts;

public static class DocumentPrompts
{
    public const string SummarizeDocument = """
        Analyze the following document and provide:
        1. Executive Summary (2-3 paragraphs)
        2. Key Points (bullet points)
        3. Key Findings
        4. Recommendations (if applicable)

        Document:
        {{$document}}
        """;

    public const string ExtractData = """
        Extract structured data from the following document according to this schema:

        Schema:
        {{$schema}}

        Document:
        {{$document}}
        """;

    public const string CompareDocuments = """
        Compare the following two documents and provide:
        1. Overall Comparison
        2. Key Differences
        3. Key Similarities
        4. Recommendations

        Document 1:
        {{$sourceDocument}}

        Document 2:
        {{$targetDocument}}
        """;

    public const string AskDocument = """
        Answer the following question based solely on the provided document content.
        If the answer cannot be found in the document, say so.

        Document:
        {{$document}}

        Question: {{$question}}
        """;
}
