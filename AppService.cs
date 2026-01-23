using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgChannelLib.Model;

namespace TgChannelSearch;

public class AppService(ILogger logger, TelegramBotClient client, Channel channel, IServiceScopeFactory spf) : BackgroundService
{
    private const char CALLBACK_QUERY_SEPARATOR = '@';

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
        };

        client.StartReceiving(OnUpdate, OnError, receiverOptions, ct);
        logger.Information("Initialized, ready to search");
    }

    private async Task OnUpdate(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        if (update.Message is { Text: not null } message)
            await HandleMessage(message, ct);
        else if (update.CallbackQuery is { Message: not null } callbackQuery)
            await HandleCallbackQuery(callbackQuery, ct);
    }

    private async Task HandleMessage(Message message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
            return;

        if (message.Text == "/start")
        {
            await StartCommand(message, ct);
            return;
        }

        var prompt = message.Text.Trim().ToLowerInvariant();
        await HandleMessagePrompt(message.Chat.Id, prompt, 0, ct);
    }

    private async Task HandleCallbackQuery(CallbackQuery query, CancellationToken ct)
    {
        var data = query.Data;

        int pageIndex;
        string prompt;

        if (data is null || string.IsNullOrWhiteSpace(data))
        {
            await client.AnswerCallbackQuery(query.Id, "Ошибка, отправьте новый запрос");
            return;
        }

        var dataSplit = data.Split(CALLBACK_QUERY_SEPARATOR);
        if (dataSplit.Length != 2 || !int.TryParse(dataSplit[0], out pageIndex) || string.IsNullOrWhiteSpace(dataSplit[1]))
        {
            await client.AnswerCallbackQuery(query.Id, "Ошибка, отправьте новый запрос");
            return;
        }

        prompt = dataSplit[1];

        await HandleMessagePrompt(query.Message.Chat.Id, prompt, pageIndex, ct, query.Message?.MessageId);
        await client.AnswerCallbackQuery(query.Id);
    }

    private async Task HandleMessagePrompt(ChatId chatId, string prompt, int postIndex, CancellationToken ct, int? messageId = null)
    {
        var query = new SearchQuery(prompt, postIndex, 1);
        var result = await HandleSearchQuery(query);

        if (result.TotalCount == 0)
        {
            await client.SendMessage(chatId, $"Поиск по запросу \"{prompt}\" не дал результатов");
            return;
        }

        var postNumber = postIndex + 1;
        var postsCount = result.TotalCount;

        var stringBuilder = new StringBuilder()
            .Append(postsCount == 1 ? "Результат" : "Результаты").Append(" по запросу \"").Append(prompt).AppendLine("\":");

        if (postsCount > 1)
            stringBuilder.Append("Пост ").Append(postNumber).Append(" из ").Append(postsCount).AppendLine();
        
        var confStr = (result.Confidence * 100).ToString("##");
        stringBuilder.Append("Уверенность: ").Append(confStr).Append('%').AppendLine();
        stringBuilder.Append(result.Link).AppendLine();

        var buttons = new List<InlineKeyboardButton>();

        if (postIndex > 0)
            buttons.Add(new InlineKeyboardButton($"\u2b05\ufe0f {postNumber - 1}", $"{postIndex - 1}{CALLBACK_QUERY_SEPARATOR}{prompt}"));

        if (postIndex < postsCount - 1)
            buttons.Add(new InlineKeyboardButton($"{postNumber + 1} \u27a1\ufe0f", $"{postIndex + 1}{CALLBACK_QUERY_SEPARATOR}{prompt}"));

        var keyboard = new InlineKeyboardMarkup(buttons);

        if (messageId == null)
            await client.SendMessage(chatId, stringBuilder.ToString(), replyMarkup: keyboard, linkPreviewOptions: new LinkPreviewOptions() { PreferLargeMedia = true } );
        else
            await client.EditMessageText(chatId, messageId.Value, stringBuilder.ToString(), replyMarkup: keyboard, linkPreviewOptions: new LinkPreviewOptions() { PreferLargeMedia = true } );
    }

    private async Task StartCommand(Message message, CancellationToken ct)
    {
        var fileInfo = new FileInfo(channel.DbPath);
        var lastWriteDT = fileInfo.LastWriteTimeUtc;
        var utcNow = DateTime.UtcNow;
        var timeSpan = utcNow - lastWriteDT;
        var timeSpanStr = timeSpan switch
        {
            TimeSpan ts when ts.TotalMinutes < 1 => "Только что",
            TimeSpan ts when ts.TotalHours < 1 => $"{(int)ts.TotalMinutes} мин. назад",
            TimeSpan ts => $"{(int)ts.TotalHours} ч. назад",
        };

        int photoCount, videoCount;

        using (var scope = spf.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ChannelContext>();
            
            var recognizedMedia = context.Recognitions
                .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                .Select(r => r.Media);

            photoCount = recognizedMedia
                .Where(m => m.Type == MediaType.Photo)
                .Select(m => m.MediaId)
                .Distinct()
                .Count();

            videoCount = recognizedMedia
                .Where(m => m.Type == MediaType.Document)
                .Select(m => m.MediaId)
                .Distinct()
                .Count();
        }

        var stringBuilder = new StringBuilder()
            .Append("Канал ").Append(channel.ChannelTag is null ? $"https://t.me/c/{channel.Id}" : channel.ChannelTag).AppendLine()
            .Append("Изображений распознано: ").Append(photoCount).AppendLine()
            .Append("Видео распознано: ").Append(videoCount).AppendLine()
            .Append("Последнее обновление базы: ").Append(timeSpanStr).AppendLine();

        await client.SendMessage(message.Chat.Id, stringBuilder.ToString());
    }

    private async Task<SearchResult> HandleSearchQuery(SearchQuery query)
    {
        using var scope = spf.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<SearchService>();
        var result = await searchService.GetResult(query);
        return result;
    }

    private async Task OnError(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
    {
        logger.Error(exception.Message);
    }
}

