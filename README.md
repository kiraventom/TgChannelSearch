# Search through [TgChannelRecognize](https://github.com/kiraventom/TgChannelRecognize) database with Telegram Bot

### Features
- Runs effecient search on the database
- Supports one channel per bot instance
- Relies on link media preview, does not download media

### Requirements
- .NET 10 or higher
- [TgChannelLib](https://github.com/kiraventom/TgChannelLib)

### Run
1. Run the application, providing the path to [TgChannelRecognize](https://github.com/kiraventom/TgChannelRecognize) database and @tag of the channel (optional). Example: `dotnet run -- ~/.local/share/TgChannelRecognize/1006503122.db @durov`
2. Application will create example `config.json` file if not present. Telegram Bot API token should be specified in this file.

### Troubleshooting
1. App will not start if Telegram Bot API token specified in `config.json` is not valid. You can get one from [BotFather](https://t.me/BotFather).
2. Link media preview will not work if @tag of the channel is not specified (private channels don't have one). This limitation is on the Telegram's side.

### Generated files
TgChannelSearch stores logs at `~/.local/share/TgChannelSearch` and config at `~/.config/TgChannelSearch`.

### Bugs
There are some, for sure.
