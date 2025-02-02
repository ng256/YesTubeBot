using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VideoDownloader;
using VideoLibrary;
using File = System.IO.File;

#pragma warning disable CS0618
internal class UserSession : Disposable
{
    #region Embeded Types

    public enum Action
    {
        SelectAudioFormat,
        SelectVideoFormat,
        DownloadAndSendVideo
    }

    private class FileProgressArgs : Disposable
    {
        public bool Temp = false;
        public long Complete = 0;
        public readonly string Path;
        public Stream? Data;

        public Stream Open()
        {
            if (Path is { } path && Data is null)
                Data = File.OpenRead(path);

            return Data;
        }

        public FileProgressArgs(string path, bool temp = false)
        {
            Path = path;
            Complete = 0;
        }

        protected override Task ClearManagedResourcesAsync()
        {
            Complete = 0;

            return Task.CompletedTask;
        }

        protected override void ClearManagedResources()
        {
            ClearManagedResourcesAsync().RunSync();
        }

        protected override async Task ClearUnmanagedResourcesAsync()
        {
            if (Path is { } path && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    Console.WriteLine($"File: {path} has been deleted");
                }
                catch
                {
                    await Console.Error.WriteLineAsync($"Cannot delete {path}");
                }
            }
        }

        protected override void ClearUnmanagedResources()
        {
            ClearUnmanagedResourcesAsync().RunSync();
        }
    }

    #endregion

    #region Fields

    private static readonly SemaphoreSlim Semaphore = new(1);
    private FileProgressArgs _thumbDownloadArgs;
    private FileProgressArgs _audioDownloadArgs;
    private FileProgressArgs _videoDownloadaArgs;
    private FileProgressArgs _videoEncodeArgs;
    private Message? _lastMessage;
    private Chat _chat;
    private ITelegramBotClient _botClient;
    private CancellationToken _cancellationToken;

    #endregion

    #region Properties

    public static string TempPath { get; set; } = PathInfo.GetFolderPath(WorkFolder.Temp);

    public YouTubeContent? Content { get; set; }
    public string? Url { get; set; }
    public string? SelectedAudioFormat { get; set; }
    public string? SelectedVideoFormat { get; set; }
    public Stack<Action> Actions { get; set; }

    #endregion

    #region Constructor

    public static async Task<UserSession> Create(string url, ITelegramBotClient botClient, Chat chat, CancellationToken ct = default)
    {
        UserSession userState = new UserSession
        {
            _botClient = botClient,
            _chat = chat,
            Url = url,
            SelectedAudioFormat = null,
            SelectedVideoFormat = null,
            Content = await YouTubeContent.LoadAsync(url),
            _cancellationToken = ct,
            Actions = new Stack<Action>
            (
                new[]
                {
                    Action.DownloadAndSendVideo,
                    Action.SelectVideoFormat,
                    Action.SelectAudioFormat
                }
            ),
        };

        return userState;
    }

    #endregion

    #region Messaging

    private async Task DeleteLastMessage()
    {
        if (_lastMessage != null)
        {
            try
            {
                await _botClient.DeleteMessageAsync
                (
                    chatId: _lastMessage.Chat.Id,
                    messageId: _lastMessage.MessageId,
                    cancellationToken: _cancellationToken
                );

            }
            finally
            {
                _lastMessage = null;
            }
        }
    }

    private async Task UpdateMessageAsync(string text, bool push = true)
    {
        try
        {
            //await DeleteLastMessage();

            var message = await _botClient.EditMessageText
                (
                    chatId: _chat.Id,
                    messageId: _lastMessage.Id, 
                    text: text, 
                    cancellationToken: _cancellationToken);
                /*chatId: _chat,
                text: text,
                disableNotification: true,
                cancellationToken: _cancellationToken
            );*/

            if (push) _lastMessage = message;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send message: {e.Message}");
        }
    }

    private async Task SendMessageAsync(string text, bool push = true)
    {
        try
        {
            await DeleteLastMessage();

            var message = await _botClient.SendTextMessageAsync(
                chatId: _chat,
                text: text,
                disableNotification: true,
                cancellationToken: _cancellationToken
            );

            if (push) _lastMessage = message;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send message: {e.Message}");
        }
    }

    private async Task SendMessageAsync(string text, InlineKeyboardMarkup keyboard, bool push = true)
    {
        try
        {
            await DeleteLastMessage();

            var message = await _botClient.SendTextMessageAsync(
                chatId: _chat,
                text: text,
                replyMarkup: keyboard,
                disableNotification: true,
                cancellationToken: _cancellationToken
            );

            if (push) _lastMessage = message;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send message: {e.Message}");
        }
    }

    #endregion

    #region Collecting Options

    public async Task ProcessNextAction()
    {
        await Semaphore.WaitAsync(_cancellationToken);

        if (SelectedAudioFormat == null)
        {
            await SendAudioFormatSelection();
        }
        else if (SelectedVideoFormat == null)
        {
            await SendVideoFormatSelection();
        }
        else if (Content != null)
        {
            await DownloadAndSendVideo();
        }
    }

    public async Task SendVideoFormatSelection()
    {
        try
        {
            var youTubeContent = Content!;
            var videos = youTubeContent.Videos;
            var videoButtons =
                from video in videos
                where video.Format == VideoFormat.Mp4
                let text = video.GetVideoFormatDescription()
                let data = $"video:{video.GetVideoId()}"
                select InlineKeyboardButton.WithCallbackData(text, data);

            var inlineKeyboard = new InlineKeyboardMarkup(videoButtons
                .DistinctBy(button => button.CallbackData)
                .Select(button => new[] { button })
                .ToArray());


            await SendMessageAsync("Please select an video format:", inlineKeyboard);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Failed to send message: {e.Message}");
        }
    }

    public async Task SendAudioFormatSelection()
    {
        try
        {
            var youTubeContent = Content!;
            var audios = youTubeContent.Audios;
            var audioButtons =
                from audio in audios
                where audio.AudioFormat == AudioFormat.Aac
                let text = audio.GetAudioFormatDescription()
                let data = $"audio:{audio.GetAudioId()}"
                select InlineKeyboardButton.WithCallbackData(text, data);

            var inlineKeyboard = new InlineKeyboardMarkup(audioButtons
                .DistinctBy(button => button.CallbackData)
                .Select(button => new[] { button })
                .ToArray());

            await SendMessageAsync("Please select an audio format:", inlineKeyboard);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"Failed to send message: {e.Message}");
        }

    }

    #endregion

    #region Downloading & Sending

    public async Task DownloadAndSendVideo()
    {
        await Semaphore.WaitAsync(_cancellationToken);
        try
        {
            var selectedAudio = Content!.Audios.FirstOrDefault(a => a.GetAudioId() == SelectedAudioFormat);
            var selectedVideo = Content.Videos.FirstOrDefault(v => v.GetVideoId() == SelectedVideoFormat);

            if (selectedVideo == null || selectedAudio == null)
                throw new InvalidOperationException("Invalid data streams selected.");

            var tempPath = TempPath;
            var timeStamp = $"{DateTime.Now:yyyyMMddhhmmss}";
            var audioFile = selectedAudio.GetAudioFileName(true);
            var videoFile = selectedVideo.GetVideoFileName(true);
            var cancellationToken = _cancellationToken;
            var thumbPath = Path.Combine(path1: tempPath, "thumb_" + timeStamp + ".jpg");
            var audioDownloadedPath = Path.Combine(path1: tempPath, "audio_" + timeStamp + ".aac");
            var videoDownloadedPath = Path.Combine(path1: tempPath, "video_" + timeStamp + ".mp4");
            var videoEncodedPath = Path.Combine(path1: tempPath, videoFile);
            bool exists = File.Exists(videoEncodedPath) && File.Exists(thumbPath);
            if (!exists)
            {
                _thumbDownloadArgs =
                    new FileProgressArgs(thumbPath);
                _audioDownloadArgs =
                    new FileProgressArgs(audioDownloadedPath);
                _videoDownloadaArgs =
                    new FileProgressArgs(videoDownloadedPath);
                _videoEncodeArgs =
                    new FileProgressArgs(videoEncodedPath);

                IProgress<long> thumbDownloadingProgress = new Progress<long>(UpdateThumbnailDownloadingInfo);
                IProgress<long> audioDownloadingProgress = new Progress<long>(UpdateAudioDownloadingInfo);
                IProgress<long> videoDownloadingProgress = new Progress<long>(UpdateVideoDownloadingInfo);
                IProgress<long> videoEncodingProgress = new Progress<long>(UpdateVideoEncodingInfo);

                Console.WriteLine($"Downloading from {Url}:");
                Console.WriteLine(_audioDownloadArgs.Path);
                Console.WriteLine(_videoDownloadaArgs.Path);
                await SendMessageAsync("○○○○○ Downloading data streams...");
                var audioThread = selectedAudio.DownloadAsync(_audioDownloadArgs.Path,
                    audioDownloadingProgress,
                    cancellationToken);
                var videoThread = selectedVideo.DownloadAsync(_videoDownloadaArgs.Path,
                    videoDownloadingProgress,
                    cancellationToken);
                await Task.WhenAll(audioThread, videoThread).ConfigureAwait(false);
                await FFMpegTool.CreateThumbnailAsync(_videoDownloadaArgs.Path, 
                    _thumbDownloadArgs.Path,
                    thumbDownloadingProgress, 
                    cancellationToken);

                Console.WriteLine("Encoding:");
                Console.WriteLine(_videoEncodeArgs.Path);
                await SendMessageAsync("◙◙○○○ Encoding video...");
                await FFMpegTool.CombineVideoAndAudioAsync(_videoDownloadaArgs.Path,
                    _audioDownloadArgs.Path,
                    _videoEncodeArgs.Path,
                    videoEncodingProgress,
                    cancellationToken).ConfigureAwait(false);

                Console.WriteLine($"Sending to {_chat.Username}");
                await SendMessageAsync("◙◙◙◙○ Sending video...");
            }

            var thumbStream = _thumbDownloadArgs.Open();
            var videoStream = _videoEncodeArgs.Open();
            await _botClient.SendVideoAsync
            (
                chatId: _chat.Id,
                thumbnail: thumbStream,
                video: videoStream,
                width: selectedVideo.Resolution,
                duration: selectedVideo.Info.LengthSeconds,
                caption: selectedVideo.Title,
                parseMode: ParseMode.None,
                cancellationToken: cancellationToken
            );
            await DeleteLastMessage();
            Console.WriteLine("Session completed");
            Console.WriteLine();
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync(e.Message);
            await SendMessageAsync("Sorry! Try again later...", false);
            Console.WriteLine("Session failed");
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
            Semaphore.Release();
        }
    }

    private void UpdateThumbnailDownloadingInfo(long p)
    {
        _thumbDownloadArgs.Complete = p;
    }

    private void UpdateAudioDownloadingInfo(long p)
    {
        _audioDownloadArgs.Complete = p;
        ReportDownloadingInfo();
    }

    private void UpdateVideoDownloadingInfo(long p)
    {
        _videoDownloadaArgs.Complete = p;
        ReportDownloadingInfo();
    }
    private void UpdateVideoEncodingInfo(long p)
    {
        _videoEncodeArgs.Complete = p;
        ReportEncodingInfo();
    }

    private void ReportDownloadingInfo()
    {
        var audioPercentage = _audioDownloadArgs.Complete;
        var videoPercentage = _videoDownloadaArgs.Complete;
        var isDownloadComplete = _audioDownloadArgs.Complete == 100 && videoPercentage == 100;


        var text = isDownloadComplete
            ? "Downloading completed!"
            : $"Downloading: audio {audioPercentage}% video {videoPercentage}%";


        var consoleText = EnsureConsoleText(text);
        Console.Write($"\r{consoleText.PadRight(Console.WindowWidth - 1)}");
        if (isDownloadComplete) // Move to a new line after completion.
            Console.WriteLine();

        if (videoPercentage == 50)
            UpdateMessageAsync("◙○○○○ Downloading data streams...").RunSync();
    }

    private void ReportEncodingInfo()
    {
        var videoEncodingPercentage = _videoEncodeArgs.Complete;
        var isEncodingComplete = _videoEncodeArgs.Complete == 100;

        var text = isEncodingComplete
            ? "Encoding completed!"
            : $"Encoding: output video {videoEncodingPercentage}%";

        var consoleText = EnsureConsoleText(text);
        Console.Write($"\r{consoleText.PadRight(Console.WindowWidth - 1)}");

        if (isEncodingComplete)
            Console.WriteLine();

        if (videoEncodingPercentage == 50)
            UpdateMessageAsync("◙◙◙○○ Encoding video...").RunSync();
    }

    private void ReportSendingInfo()
    {
        throw new NotImplementedException("TelegramBot did not yet provide a way to get information about the download progress.");

        /*
        var videoSendingPercentage = 0;
        var isEncodingComplete = videoSendingPercentage == 100;

        var text = isEncodingComplete
            ? "Video was sent successful!"
            : $"Sending: video {isEncodingComplete}%";

        SendMessageAsync(text).RunSync();

        var consoleText = EnsureConsoleText(text);
        Console.Write($"\r{consoleText.PadRight(Console.WindowWidth)}");
        if (isEncodingComplete)
            Console.WriteLine();*/
    }

    #endregion

    #region Tools

    // Ensure text fits in the console window.
    private static string EnsureConsoleText(string text)
    {
        var consoleText = text.Length > Console.WindowWidth
            ? text.Substring(0, Console.WindowWidth - 3) + "..."
            : text;
        return consoleText;
    }

    protected override void ClearManagedResources()
    {
        DeleteLastMessage().RunSync();
    }

    #endregion
}