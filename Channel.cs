using Serilog;
using TgChannelLib;

namespace TgChannelSearch;

public class Channel : IChannelInfo
{
    public long Id { get; }
    public string DbPath { get; }
    public string ChannelTag { get; set; }

    long IChannelInfo.ChannelId => Id;
    long? IChannelInfo.DiscussionGroupId => null; // TODO
    string IChannelInfo.ChannelTag => ChannelTag;

    private Channel(long id, string path)
    {
        Id = id;
        DbPath = path;
    }

    public static bool TryParse(string filepath, out Channel channel)
    {
        channel = null;

        var filename = Path.GetFileNameWithoutExtension(filepath);
        if (!long.TryParse(filename, out var id))
        {
            Log.Logger.Error("Can't parse {str} as id", filename);
            return false;
        }

        channel = new Channel(id, filepath);
        return true;
    }
}

