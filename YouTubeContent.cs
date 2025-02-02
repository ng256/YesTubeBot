using System.Text.RegularExpressions;
using VideoLibrary;

namespace VideoDownloader;

/// <summary>
/// Provides functionality to load and organize YouTube video and audio content
/// </summary>
internal class YouTubeContent
{
    private static YouTube? _youTubeService;
    private static Regex? _matchUrl;
    
    /// <summary>
    /// Gets the singleton instance of the YouTube service
    /// </summary>
    private static YouTube YouTubeService => _youTubeService ??= YouTube.Default;
    
    /// <summary>
    /// Gets the compiled regular expression for matching YouTube URLs
    /// </summary>
    /// <remarks>
    /// Matches various YouTube URL formats including:
    /// - Standard watch URLs
    /// - Short URLs (youtu.be)
    /// - Embed URLs
    /// - Playlist URLs
    /// </remarks>
    private static Regex MatchUrl => _matchUrl ??= new Regex(
        @"(?:https?://)?(?:www\.)?(?:youtu\.be/|youtube\.com/(?:embed/|v/|playlist\?|watch\?v=|watch\?.+(?:&|&#38;);v=))([a-zA-Z0-9\-_]{11})?(?:(?:\?|&|&#38;)index=((?:\d){1,3}))?(?:(?:\?|&|&#38;)?list=([a-zA-Z\-_0-9]{34}))?(?:\S+)", 
        RegexOptions.Compiled);

    private YouTubeVideoCollection _audios;
    private YouTubeVideoCollection _videos;

    /// <summary>
    /// Gets the collection of available audio streams
    /// </summary>
    /// <remarks>
    /// Audio streams are ordered by ascending audio bitrate
    /// </remarks>
    public YouTubeVideoCollection Audios => _audios;

    /// <summary>
    /// Gets the collection of available video streams
    /// </summary>
    /// <remarks>
    /// Video streams are ordered by ascending resolution
    /// </remarks>
    public YouTubeVideoCollection Videos => _videos;

    /// <summary>
    /// Determines if a string contains a valid YouTube URL
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>true if the input contains a valid YouTube URL; otherwise, false</returns>
    public static bool ContainsUrl(string url) => MatchUrl.IsMatch(url);

    /// <summary>
    /// Extracts the first valid YouTube URL from a string
    /// </summary>
    /// <param name="url">The input string containing a potential YouTube URL</param>
    /// <returns>The matched YouTube URL or null if no valid URL found</returns>
    public static string? GetUrl(string url)
    {
        var match = MatchUrl.Match(url);
        return match.Success ? match.Value : null;
    }

    private YouTubeContent(params YouTubeVideo[] videos)
    {
        _audios = GetAudios(videos);
        _videos = GetVideos(videos);
    }

    /// <summary>
    /// Synchronously loads YouTube content from a URL
    /// </summary>
    /// <param name="url">Valid YouTube URL</param>
    /// <returns>YouTubeContent containing available media streams</returns>
    /// <exception cref="ArgumentNullException">Thrown when url is null</exception>
    /// <exception cref="ArgumentException">Thrown when url is empty or invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when no videos could be loaded</exception>
    public static YouTubeContent Load(string? url)
    {
        return LoadAsync(url).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously loads YouTube content from a URL
    /// </summary>
    /// <param name="url">Valid YouTube URL</param>
    /// <returns>Task that resolves to YouTubeContent containing available media streams</returns>
    /// <exception cref="ArgumentNullException">Thrown when url is null</exception>
    /// <exception cref="ArgumentException">Thrown when url is empty or invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when no videos could be loaded</exception>
    public static async Task<YouTubeContent> LoadAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url), "URL cannot be null.");
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty.", nameof(url));
        if (!MatchUrl.IsMatch(url))
            throw new ArgumentException("URL is invalid.", nameof(url));

        var videos = (await YouTubeService.GetAllVideosAsync(url)).ToArray();

        int count = videos.Length;
        if (count == 0)
        {
            throw new InvalidOperationException($"Could not load any videos from {url}");
        }

        return new YouTubeContent(videos);
    }

    /// <summary>
    /// Filters and orders video streams
    /// </summary>
    /// <param name="videos">Raw video collection</param>
    /// <returns>Ordered collection of video streams</returns>
    /// <remarks>
    /// Includes both adaptive video streams and non-adaptive streams with resolution information,
    /// ordered by ascending resolution
    /// </remarks>
    private static YouTubeVideoCollection GetVideos(params YouTubeVideo[] videos)
    {
        return new YouTubeVideoCollection
        (
            from video in videos
            where video 
                is { IsAdaptive: true, AdaptiveKind: AdaptiveKind.Video } 
                or { Resolution: > 0 }
            orderby video.Resolution
            select video
        );
    }

    /// <summary>
    /// Filters and orders audio streams
    /// </summary>
    /// <param name="videos">Raw video collection</param>
    /// <returns>Ordered collection of audio streams</returns>
    /// <remarks>
    /// Includes both adaptive audio streams and non-adaptive streams with audio bitrate information,
    /// ordered by ascending audio bitrate
    /// </remarks>
    private static YouTubeVideoCollection GetAudios(params YouTubeVideo[] videos)
    {
        return new YouTubeVideoCollection
        (
            from video in videos
            where video 
                is { IsAdaptive: true, AdaptiveKind: AdaptiveKind.Audio } 
                or { AudioBitrate: > 0 }
            orderby video.AudioBitrate
            select video
        );
    }
}
