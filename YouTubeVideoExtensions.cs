using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using VideoLibrary;

namespace VideoDownloader;

internal static class YouTubeVideoExtensions
{
    #region Tools

    private static Regex? _matchId = null;
    private static Regex MatchId => _matchId ??= new Regex(@"(?:v=|\/)([0-9A-Za-z_-]{11})", RegexOptions.Compiled);

    private static async Task<long?> GetContentLengthAsync(HttpClient client, 
        string requestUri, 
        bool ensureSuccess = true,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, requestUri);
        HttpResponseMessage response = await client.SendAsync(request, 
            HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (ensureSuccess)
            response.EnsureSuccessStatusCode();
        return response.Content.Headers.ContentLength;
    }

    private static HttpMessageHandler GetHandler()
    {
        CookieContainer cookieContainer = new CookieContainer();
        cookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", "youtube.com"));
        return new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookieContainer
        };

    }

    private static HttpClient GetClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36 Edg/89.0.774.76");
        return httpClient;
    }

    private static string? ExtractVideoId(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        Match match = MatchId.Match(uri);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string ExtractVideoId(this YouTubeVideo video)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        return ExtractVideoId(video.Uri) 
               ?? throw new InvalidOperationException("Unable to extract video ID from the URI."); ;
    }

    private static void AppendExceptionMessages(Exception ex, StringBuilder messages)
    {
        if (ex == null)
        {
            return;
        }

        messages.AppendLine(ex.Message);

        if (ex.InnerException != null)
        {
            AppendExceptionMessages(ex.InnerException, messages);
        }
    }

    public static string GetMessages(this Exception ex)
    {
        if (ex == null)
        {
            return string.Empty;
        }

        StringBuilder messages = new StringBuilder();
        AppendExceptionMessages(ex, messages);
        return messages.ToString();
    }

    #endregion

    #region File name

    public static string GetAudioId(this YouTubeVideo video) 
        => $"{video.AudioFormat}_{video.AudioBitrate}_kbs";

    public static string GetAudioFileName(this YouTubeVideo video, bool format = false)
    {
        string extension = string.IsNullOrWhiteSpace(video.FileExtension)
            ? $"{video.AudioFormat}"
            : video.FileExtension;
        string videoTitle = PathInfo.GetValidPath(video.Title);
        if (format) videoTitle += $"_{video.AudioBitrate}_kbs";
        string fileName = Path.ChangeExtension(videoTitle, extension);

        return fileName;
    }

    public static string GetVideoId(this YouTubeVideo video) 
        => $"{video.Format}_{video.Resolution}_p";

    public static string GetVideoFileName(this YouTubeVideo video, bool format = false)
    {
        string extension = string.IsNullOrWhiteSpace(video.FileExtension) 
            ?  $"{video.Format}" 
            : video.FileExtension;
        string videoTitle = PathInfo.GetValidPath(video.Title);
        if (format) videoTitle += $"_{video.Resolution}_p";
        string fileName = Path.ChangeExtension(videoTitle, extension);

        return fileName;
    }

    public static string GetFileName(this YouTubeVideo video, bool format = false)
    {
        if (video == null) 
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        if (video.IsAdaptive) switch (video.AdaptiveKind)
        {
            case AdaptiveKind.Audio:
                return GetAudioFileName(video, format);
            case AdaptiveKind.Video:
                return GetVideoFileName(video, format);
        }
        if (video.Resolution > 0) return GetAudioFileName(video, format);
        if (video.AudioBitrate > 0) return GetVideoFileName(video, format);

        return Path.GetTempFileName();
    }

    #endregion

    #region Description

    public static string GetAudioFormatDescription(this YouTubeVideo v)
        => $"{v.AudioFormat} {v.AudioBitrate}kbs";
    public static string GetVideoFormatDescription(this YouTubeVideo v)
        => $"{v.Format} {v.Resolution}p";

    public static string GetFormatDescription(YouTubeVideo video)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        if (video.IsAdaptive) switch (video.AdaptiveKind)
        {
            case AdaptiveKind.Audio:
                return GetAudioFormatDescription(video);
            case AdaptiveKind.Video:
                return GetVideoFormatDescription(video);
        }
        if (video.Resolution > 0) return GetAudioFormatDescription(video);
        if (video.AudioBitrate > 0) return GetVideoFormatDescription(video);

        return "Unknown";
    }

    public static string GetVideoDescription(this YouTubeVideo video)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        string title = video.Title ?? "Unknown";
        int lengthSeconds = video.Info.LengthSeconds ?? 0;
        string duration = lengthSeconds > 0
            ? $"{TimeSpan.FromSeconds(lengthSeconds):hh\\:mm\\:ss}"
            : "Unknown";
        string resolution = $"{video.Resolution}p";

        return $"Title: {title}\nDuration: {duration}\nResolution: {resolution}";
    }

    #endregion

    #region Download

    public static async Task DownloadAsync(this YouTubeVideo video, 
        string filePath, 
        IProgress<long> progress,
        CancellationToken cancellationToken = default)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        await DownloadAsync(video.Uri, filePath, progress, cancellationToken);
    }

    public static async Task DownloadPreviewAsync(this YouTubeVideo video,
        string filePath,
        IProgress<long> progress,
        CancellationToken cancellationToken = default)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        string videoId = video.ExtractVideoId();
        string previewUrl = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";

        await DownloadAsync(previewUrl, filePath, progress, cancellationToken);
    }

    public static async Task DownloadAsync(string url, 
        string filePath, 
        IProgress<long> progress, 
        CancellationToken cancellationToken = default)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url), "The video cannot be null.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        SemaphoreSlim reportLock = new SemaphoreSlim(1, 1);
        Uri uri = new Uri(url);
        HttpMessageHandler handler = GetHandler();
        HttpClient client = GetClient(handler);
        long percentTotal = 0L;
        long bytesTotal = 0L;
        long? contentLength = await GetContentLengthAsync(client, uri.AbsoluteUri, 
            ensureSuccess: true, cancellationToken);
        long fileSize = contentLength is > 0 
            ? contentLength.Value 
            : throw new InvalidOperationException("The video has no any content.");
        await using Stream fileStream = File.OpenWrite(filePath);
        byte[] buffer = new byte[0x14000];
        const long chunkSize = 0xA00000;
        int segmentCount = (int)Math.Ceiling(1.0 * fileSize / chunkSize);
        for (int i = 0; i < segmentCount; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            long from = i * chunkSize;
            long to = (i + 1) * chunkSize - 1;
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Range = new RangeHeaderValue(from, to);
            using (request)
            {
                HttpResponseMessage response = await client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.IsSuccessStatusCode)
                    response.EnsureSuccessStatusCode();
                await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                int bytesCopied;
                do
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    bytesCopied = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    await fileStream.WriteAsync(buffer, 0, bytesCopied, cancellationToken);
                    bytesTotal += bytesCopied;
                    if (progress == null) continue;
                    long percent = bytesTotal * 100 / fileSize;
                    if (percentTotal < percent) progress.Report(percentTotal = percent);
                } while (bytesCopied > 0);
            }
        }
    }

    #endregion
}     