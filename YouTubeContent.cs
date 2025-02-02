using System.Text.RegularExpressions;
using VideoLibrary;

namespace VideoDownloader;

internal class YouTubeContent
{
    private static YouTube? _youTubeService;
    private static Regex? _matchUrl;
    private static YouTube YouTubeService => _youTubeService ??= YouTube.Default;
    private static Regex MatchUrl => _matchUrl ??= new Regex(@"(?:https?://)?(?:www\.)?(?:youtu\.be/|youtube\.com/(?:embed/|v/|playlist\?|watch\?v=|watch\?.+(?:&|&#38;);v=))([a-zA-Z0-9\-_]{11})?(?:(?:\?|&|&#38;)index=((?:\d){1,3}))?(?:(?:\?|&|&#38;)?list=([a-zA-Z\-_0-9]{34}))?(?:\S+)", RegexOptions.Compiled);

    private YouTubeVideoCollection _audios;
    private YouTubeVideoCollection _videos;

    public YouTubeVideoCollection Audios => _audios;
    public YouTubeVideoCollection Videos => _videos;

    public static bool ContainsUrl(string url) => MatchUrl.IsMatch(url);

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

    public static YouTubeContent Load(string? url)
    {
        return LoadAsync(url).GetAwaiter().GetResult();
    }

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