using Microsoft.EntityFrameworkCore;
using Serilog;
using TgChannelLib;
using TgChannelLib.Model;

namespace TgChannelSearch;

public class SearchService(ILogger logger, IChannelInfo channelInfo, ChannelContext context)
{
    public const int MinPromptLength = 3;

    public async Task<SearchResult> GetResult(ISearchQuery query, SearchItem item = SearchItem.Post)
    {
        var prompt = query.Prompt;

        ArgumentNullException.ThrowIfNull(prompt);

        if (prompt.Length < MinPromptLength)
            return SearchResult.PromptTooShort;

        IQueryable<Item> posts = context.Posts.AsNoTracking();
        IQueryable<Item> comments = context.Comments.AsNoTracking();

        IQueryable<Item> items = item switch
        {
            SearchItem.Post => posts,
            SearchItem.Comment => comments,
            SearchItem.All => posts.Concat(comments),
            _ => throw new NotSupportedException($"Unexpected {nameof(SearchItem)}: {item.ToString()}")
        };

        var pattern = $"%{EscapeLike(prompt)}%";

        var results = items
            .Select(i => new
            {
                Item = i,
                Best = i.Media.SelectMany(m => m.Recognitions)
                    .Where(p => EF.Functions.Like(p.Text, pattern, @"\"))
                    .AsEnumerable()
                    .Select(r => (int?)(r.Confidence * 100))
                    .Max()
            })
            .Where(x => x.Best != null)
            .OrderByDescending(x => x.Best)
            .ThenByDescending(x => x.Item.DT);

        var totalCount = await results.CountAsync();

        var result = await results
            .Skip(query.Offset)
            .Take(1)
            .Select(x => new { Item = x.Item, Best = x.Best.Value })
            .FirstOrDefaultAsync();

        if (result is null)
            return SearchResult.NothingFound;

        return new SearchResult(result.Item.BuildLink(channelInfo), result.Best, result.Item.DT, totalCount);
    }

    private static string EscapeLike(string input)
    {
        return input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("[", @"\[")
            .Replace("_", @"\_");
    }
}
