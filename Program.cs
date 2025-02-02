/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *\
*                                                                                 *
*     _|      _|  _|_|_|_|    _|_|_|  _|_|_|_|_|  _|    _|  _|_|_|    _|_|_|_|    *
*       _|  _|    _|        _|            _|      _|    _|  _|    _|  _|          *
*         _|      _|_|_|      _|_|        _|      _|    _|  _|_|_|    _|_|_|      *
*         _|      _|              _|      _|      _|    _|  _|    _|  _|          *
*         _|      _|_|_|_|  _|_|_|        _|        _|_|    _|_|_|    _|_|_|_|    *
*                                                                                 *
\* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Ini;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VideoDownloader;
using VideoLibrary;
using File = System.IO.File;
#pragma warning disable CS0618 // Type or member is obsolete

[IniSection("Settings")]
internal class Program
{

    private static readonly TelegramBotClient BotClient = CreateBotClient("7408864209:AAEepcFxjK-8nx1IfL2bO5b_3sqlJoV6lNo", "http://127.0.0.1:8080/");
    private static readonly ConcurrentDictionary<long, UserSession> UserSelections = new();
    private static readonly string AppFolderPath = PathInfo.GetFolderPath(WorkFolder.Application);
    private static readonly string LogoPath = Path.Combine(AppFolderPath, "logo.txt");
    private static readonly string IniPath = Path.Combine(AppFolderPath, "settings.ini");

    [IniEntry("temp")]
    private static string TempPath { get; set; } = PathInfo.GetFolderPath(WorkFolder.Temp);

    [IniEntry("start")]
    private static string StartMessage { get; set; } = @"Welcome to YesTubeBot that lets you download YouTube videos easily! 🎥

🔗 Send a YouTube link.
📋 Choose the video quality or format.
📥 Get the video sent directly to your chat!

Try it now—just send a link!";

    [IniEntry("commands")]
    private static string CommandsMessage { get; set; } = @"Commands:
/start
Begin using YesTubeBot and receive a welcome message.

/help
View a list of available commands and instructions.

/reset
Reset your current session and start fresh.

YouTube Link
Simply send a YouTube link to download the video or audio.";

    public static async Task Main(string[] args)
    {

        if(File.Exists(LogoPath))
        {
            string logo = await File.ReadAllTextAsync(LogoPath);
            Console.WriteLine(logo);
        }
        

        if (File.Exists(IniPath))
        {
            IniFile inifile = IniFile.Load(IniPath, allowEscChars: true);
            inifile.ReadSettings(typeof(Program));
        }

        var me = await BotClient.GetMeAsync();
        Console.WriteLine($"Start listening for @{me.Username}");

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
            DropPendingUpdates = true,
        };

        void CtrlC(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            cts.Cancel();
            eventArgs.Cancel = true;
        }

        Console.CancelKeyPress += CtrlC;

        BotClient.StartReceiving
        (
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Cancellation requested. Stopping the bot...");
        }
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { } message:
                await HandleMessage(message, cancellationToken);
                break;
            case UpdateType.CallbackQuery when update.CallbackQuery is { } callbackQuery:
                await HandleCallbackQuery(callbackQuery, cancellationToken);
                break;
        }
    }

    private static async Task HandleMessage(Message message, CancellationToken cancellationToken)
    {
        var user = message.From!;

        switch (message.Type)
        {
            case MessageType.Text when message.Text is { } messageText:

                Console.WriteLine($"{user.Username}: {messageText}");
                await HandleTextMessage(message, cancellationToken);
                break;
        }
    }

    private static async Task HandleTextMessage(Message message, CancellationToken cancellationToken)
    {
        string messageText = message.Text!;

        switch (messageText)
        {
            case "/start":
                await HandleStartCommand(message, cancellationToken);
                break;
            case "/help":
                await HandleHelpCommand(message, cancellationToken);
                break;
            default:
                if (YouTubeContent.ContainsUrl(messageText))
                    await HandleYouTubeLink(message, cancellationToken);
                break;
        }

        await BotClient.DeleteMessageAsync(message.Chat.Id, message.Id, cancellationToken).ConfigureAwait(false);
    }

    private static async Task HandleYouTubeLink(Message message, CancellationToken cancellationToken)
    {
        var url = YouTubeContent.GetUrl(message.Text!);
        var chatId = message.Chat.Id;
        ResetUserState(chatId);

        UserSession userState = await UserSession.Create(url, BotClient, message.Chat, cancellationToken);
        UserSelections[chatId] = userState;

        await userState.ProcessNextAction().ConfigureAwait(false);
    }

    private static async Task HandleHelpCommand(Message message, CancellationToken cancellationToken)
    {
        await BotClient.SendTextMessageAsync
        (
            chatId: message.Chat.Id,
            text: CommandsMessage,
            cancellationToken: cancellationToken
        );
    }

    private static async Task HandleStartCommand(Message message, CancellationToken cancellationToken)
    {
        ResetUserState(message.Chat.Id);

        await BotClient.SendTextMessageAsync
        (
            chatId: message.Chat.Id,
            text: StartMessage,
            cancellationToken: cancellationToken
        );
    }

    private static async Task HandleCallbackQuery(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data!.Split(':');
        var type = data[0];
        var format = data[1];
        var message = callbackQuery.Message!;
        var chatId = message.Chat.Id;
        var user = message.From!;
        Console.WriteLine($"{user.Username}: {callbackQuery.Data}");

        var userState = GetUserState(chatId)!;

        switch (type)
        {
            case "audio":
                userState.SelectedAudioFormat = format;
                break;
            case "video":
                userState.SelectedVideoFormat = format;
                break;
        }

        await userState.ProcessNextAction();
    }

    private static UserSession? GetUserState(long chatId)
    {
        return UserSelections.TryGetValue(chatId, out UserSession? userState) ? userState : default;
    }

    private static void ResetUserState(long chatId)
    {
        UserSelections.Remove(chatId, out UserSession? session);
        session?.Dispose();
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.Message
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }

    public static TelegramBotClient CreateBotClient(string botToken, string? apiServerUrl = default)
    {
        var options = new TelegramBotClientOptions(botToken, apiServerUrl);
        return new TelegramBotClient(options);
    }
}