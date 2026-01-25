namespace TgChannelSearch;

public record SearchResult(string Link, int Confidence, DateTime DT, int TotalCount)
{
    public SearchResultError Error { get; }

    public static SearchResult PromptTooShort { get; } = new SearchResult(SearchResultError.PromptTooShort);
    public static SearchResult NothingFound { get; } = new SearchResult(SearchResultError.NothingFound);

    private SearchResult(SearchResultError error) : this(null, 0, DateTime.MinValue, 0)
    {
        Error = error;
    }
}

