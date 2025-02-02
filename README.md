# 📥 YesTubeBot

YesTubeBot is a Telegram bot for downloading videos with audio from YouTube. Just send a video link, and the bot will download it and send you the ready file!  

__WARNING!__ This project is not complete yet!

## 🔧 Features

- ✅ **Integration with Telegram.Bot** – stable operation via Telegram API
- ✅ **Downloading via VideoLibrary** – extracts video and audio directly from YouTube
- ✅ **Merging video and audio using ffmpegtool** – preserves quality without loss
- ✅ **Flexible settings via ini-file** – manage download parameters and file quality
- ✅ **Support for files over 20MB** – sending via Telegram Bot API

## 🚀 How to Use

1. Start the bot in Telegram
2. Send a YouTube video link
3. Wait for processing and downloading
4. Receive the ready file directly in Telegram

## 🛠️ Installation and Setup

### Requirements:

- .NET 6 or newer
- FFMPEG installed on the system
- Telegram Bot API token
- Telegram Bot API server

### Running the bot:

1. Install dependencies
2. Configure `config.ini`
3. Start the application __YesTubeBot.exe__

## ⚙ Configuration (config.ini)

Example `config.ini` file:

```ini
[bot]
token=YOUR_TELEGRAM_BOT_TOKEN

[download]
tmp=downloads/
```

## 📜 License

This project is distributed under the MIT license.

