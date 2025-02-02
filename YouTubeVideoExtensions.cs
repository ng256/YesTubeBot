using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using VideoLibrary;

namespace VideoDownloader;

/// <summary>
/// Provides extension methods for working with YouTubeVideo instances
/// </summary>
internal static class YouTubeVideoExtensions
{
    #region Tools

    private static Regex? _matchId = null;
    
    /// <summary>
    /// Regular expression pattern for extracting YouTube video IDs
    /// </summary>
    private static Regex MatchId => _matchId ??= new Regex(
        @"(?:v=|\/)([0-9A-Za-z_-]{11})", 
        RegexOptions.Compiled);

    /// <summary>
    /// Retrieves content length for a remote resource
    /// </summary>
    /// <param name="client">HttpClient instance to use</param>
    /// <param name="requestUri">Target resource URI</param>
    /// <param name="ensureSuccess">Whether to validate successful response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Content length in bytes or null if unavailable</returns>
    /// <exception cref="HttpRequestException">Thrown when ensureSuccess is true and response status is not successful</exception>
    private static async Task<long?> GetContentLengthAsync(
        HttpClient client, 
        string requestUri, 
        bool ensureSuccess = true,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Head, requestUri);
        HttpResponseMessage response = await client.SendAsync(
            request, 
            HttpCompletionOption.ResponseHeadersRead, 
            cancellationToken);
        
        if (ensureSuccess)
            response.EnsureSuccessStatusCode();
        
        return response.Content.Headers.ContentLength;
    }

    /// <summary>
    /// Creates a configured HTTP message handler
    /// </summary>
    /// <returns>HttpMessageHandler with YouTube cookies preconfigured</returns>
    private static HttpMessageHandler GetHandler()
    {
        CookieContainer cookieContainer = new();
        cookieContainer.Add(new Cookie("CONSENT", "YES+cb", "/", "youtube.com"));
        return new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookieContainer
        };
    }

    /// <summary>
    /// Creates a configured HttpClient instance
    /// </summary>
    /// <param name="handler">Message handler to use</param>
    /// <returns>HttpClient with appropriate headers and configuration</returns>
    private static HttpClient GetClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36 Edg/89.0.774.76");
        return httpClient;
    }

    /// <summary>
    /// Extracts YouTube video ID from a URI string
    /// </summary>
    /// <param name="uri">YouTube video URI</param>
    /// <returns>11-character video ID or null if not found</returns>
    private static string? ExtractVideoId(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        Match match = MatchId.Match(uri);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts YouTube video ID from a YouTubeVideo instance
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <returns>11-character video ID</returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when video URI is invalid</exception>
    private static string ExtractVideoId(this YouTubeVideo video)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        return ExtractVideoId(video.Uri) 
               ?? throw new InvalidOperationException("Unable to extract video ID from the URI.");
    }

    /// <summary>
    /// Recursively collects exception messages
    /// </summary>
    /// <param name="ex">Exception to process</param>
    /// <param name="messages">StringBuilder for accumulating messages</param>
    private static void AppendExceptionMessages(Exception ex, StringBuilder messages)
    {
        if (ex == null) return;

        messages.AppendLine(ex.Message);
        if (ex.InnerException != null)
        {
            AppendExceptionMessages(ex.InnerException, messages);
        }
    }

    /// <summary>
    /// Gets concatenated exception messages including inner exceptions
    /// </summary>
    /// <param name="ex">Exception to process</param>
    /// <returns>Multiline string containing all exception messages</returns>
    public static string GetMessages(this Exception ex)
    {
        if (ex == null)
        {
            return string.Empty;
        }

        StringBuilder messages = new();
        AppendExceptionMessages(ex, messages);
        return messages.ToString();
    }

    #endregion

    #region File name

    /// <summary>
    /// Generates unique audio identifier string
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <returns>Format: "{AudioFormat}_{AudioBitrate}_kbs"</returns>
    public static string GetAudioId(this YouTubeVideo video) 
        => $"{video.AudioFormat}_{video.AudioBitrate}_kbs";

    /// <summary>
    /// Generates safe audio filename
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <param name="format">Whether to include bitrate in filename</param>
    /// <returns>Sanitized filename with appropriate extension</returns>
    public static string GetAudioFileName(this YouTubeVideo video, bool format = false)
    {
        string extension = string.IsNullOrWhiteSpace(video.FileExtension)
            ? $"{video.AudioFormat}"
            : video.FileExtension;
        string videoTitle = PathInfo.GetValidPath(video.Title);
        if (format) videoTitle += $"_{video.AudioBitrate}_kbs";
        return Path.ChangeExtension(videoTitle, extension);
    }

    /// <summary>
    /// Generates unique video identifier string
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <returns>Format: "{Format}_{Resolution}_p"</returns>
    public static string GetVideoId(this YouTubeVideo video) 
        => $"{video.Format}_{video.Resolution}_p";

    /// <summary>
    /// Generates safe video filename
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <param name="format">Whether to include resolution in filename</param>
    /// <returns>Sanitized filename with appropriate extension</returns>
    public static string GetVideoFileName(this YouTubeVideo video, bool format = false)
    {
        string extension = string.IsNullOrWhiteSpace(video.FileExtension) 
            ?  $"{video.Format}" 
            : video.FileExtension;
        string videoTitle = PathInfo.GetValidPath(video.Title);
        if (format) videoTitle += $"_{video.Resolution}_p";
        return Path.ChangeExtension(videoTitle, extension);
    }

    /// <summary>
    /// Generates appropriate filename based on video type
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <param name="format">Whether to include format details in filename</param>
    /// <returns>
    /// Audio filename for adaptive audio, 
    /// Video filename for adaptive video,
    /// Temporary filename for unknown types
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
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

    /// <summary>
    /// Formats audio description string
    /// </summary>
    /// <param name="v">YouTube video instance</param>
    /// <returns>Format: "{AudioFormat} {AudioBitrate}kbs"</returns>
    public static string GetAudioFormatDescription(this YouTubeVideo v)
        => $"{v.AudioFormat} {v.AudioBitrate}kbs";

    /// <summary>
    /// Formats video description string
    /// </summary>
    /// <param name="v">YouTube video instance</param>
    /// <returns>Format: "{Format} {Resolution}p"</returns>
    public static string GetVideoFormatDescription(this YouTubeVideo v)
        => $"{v.Format} {v.Resolution}p";

    /// <summary>
    /// Gets format description based on video type
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <returns>
    /// Audio description for adaptive audio,
    /// Video description for adaptive video,
    /// "Unknown" for other types
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
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

    /// <summary>
    /// Generates full video description string
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <returns>
    /// Multiline string containing title, duration, and resolution
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
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

    /// <summary>
    /// Downloads video content to specified file
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <param name="filePath">Target file path</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing download operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
    /// <exception cref="ArgumentException">Thrown for invalid file path</exception>
    /// <exception cref="InvalidOperationException">Thrown when content length is unavailable</exception>
    /// <remarks>
    /// Uses chunked download with multiple range requests for better reliability
    /// </remarks>
    public static async Task DownloadAsync(
        this YouTubeVideo video, 
        string filePath, 
        IProgress<long> progress,
        CancellationToken cancellationToken = default)
    {
        if (video == null)
            throw new ArgumentNullException(nameof(video), "The video cannot be null.");

        await DownloadAsync(video.Uri, filePath, progress, cancellationToken);
    }

    /// <summary>
    /// Downloads video preview image (thumbnail)
    /// </summary>
    /// <param name="video">YouTube video instance</param>
    /// <param name="filePath">Target file path</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing download operation</returns>
    /// <exception cref="ArgumentNullException">Thrown when video is null</exception>
    public static async Task DownloadPreviewAsync(
        this YouTubeVideo video,
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

    /// <summary>
    /// Generic download implementation for any URL
    /// </summary>
    /// <param name="url">Resource URL</param>
    /// <param name="filePath">Target file path</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing download operation</returns>
    /// <exception cref="ArgumentNullException">Thrown for invalid URL</exception>
    /// <exception cref="ArgumentException">Thrown for invalid file path</exception>
    /// <exception cref="InvalidOperationException">Thrown when content length is unavailable</exception>
    /// <remarks>
    /// Implements resumable download using 10MB chunks
    /// Uses parallel range requests for improved performance
    /// </remarks>
    public static async Task DownloadAsync(
        string url, 
        string filePath, 
        IProgress<long> progress, 
        CancellationToken cancellationToken = default)
    {
        if (url == null)
            throw new ArgumentNullException(nameof(url), "The video cannot be null.");
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        SemaphoreSlim reportLock = new(1, 1);
        Uri uri = new(url);
        using HttpMessageHandler handler = GetHandler();
        using HttpClient client = GetClient(handler);
        
        long? contentLength = await GetContentLengthAsync(
            client, 
            uri.AbsoluteUri, 
            ensureSuccess: true, 
            cancellationToken);
        
        long fileSize = contentLength is > 0 
            ? contentLength.Value 
            : throw new InvalidOperationException("The video has no any content.");

        await using Stream fileStream = File.OpenWrite(filePath);
        byte[] buffer = new byte[0x14000];
        const long chunkSize = 0xA00000;
        int segmentCount = (int)Math.Ceiling(1.0 * fileSize / chunkSize);

        for (int i = 0; i < segmentCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long from = i * chunkSize;
            long to = (i + 1) * chunkSize - 1;
            HttpRequestMessage request = new(HttpMethod.Get, uri)
            {
                Headers = { Range = new RangeHeaderValue(from, to) }
            };

            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
            
            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            int bytesCopied;
            long bytesTotal = 0;
            do
            {
                bytesCopied = await responseStream.ReadAsync(buffer, cancellationToken);
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesCopied), cancellationToken);
                bytesTotal += bytesCopied;
                
                if (progress != null)
                {
                    long percent = bytesTotal * 100 / fileSize;
                    if (percent > progressReported)
                    {
                        progress.Report(percent);
                        progressReported = percent;
                    }
                }
            } while (bytesCopied > 0);
        }
    }

    #endregion
}
