namespace TgChannelSearch;

public record SearchQuery(string Prompt = "", int Offset = 0, int Count = 0) : ISearchQuery;
