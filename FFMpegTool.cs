using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static VideoDownloader.PathInfo;

namespace VideoDownloader;

internal class FFMpegTool
{

    private static string? _ffmpegPath = null;
    private static string? _ffprobePath = null;

    public static string FfmpegPath => 
        _ffmpegPath ??= Path.Combine(path1: GetFolderPath(WorkFolder.Application), // Application folder
            "ffmpeg.exe");
    
    public static string FfprobePath =>
        _ffprobePath ??= Path.Combine(path1: GetFolderPath(WorkFolder.Application),
            "ffprobe.exe");

    public static async Task CreateThumbnailAsync(string videoPath,
        string outputPath,
        IProgress<long> progress,
        CancellationToken cancellationToken = default)
    {
        string ffmpegPath = FfmpegPath;
        string arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 \"{outputPath}\"";

        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        if (!process.Start())
            throw new InvalidOperationException("Cannot run ffmpeg process.");

        Task outputTask = ReadProgressAsync(process.StandardOutput, progress, 1, cancellationToken);
        Task errorTask = ReadProgressAsync(process.StandardError, cancellationToken);
        Task exitTask = process.WaitForExitAsync(cancellationToken);

        await Task.WhenAll(outputTask, errorTask, exitTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}");
        }
    }

    public static async Task CombineVideoAndAudioAsync(string videoPath, 
        string audioPath, 
        string outputPath,
        IProgress<long> progress,
        CancellationToken cancellationToken = default)
    {
        string ffmpegPath = FfmpegPath;
        string arguments = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac \"{outputPath}\" -progress pipe:1 -nostats";
        double totalDuration = await GetDuration(videoPath);

        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        if (!process.Start()) 
            throw new InvalidOperationException("Cannot run ffmpeg process.");

        Task outputTask = ReadProgressAsync(process.StandardOutput, progress, totalDuration, cancellationToken);
        Task errorTask = ReadProgressAsync(process.StandardError, cancellationToken);
        Task exitTask = process.WaitForExitAsync(cancellationToken);

        await Task.WhenAll(outputTask, errorTask, exitTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}");
        }
    }

    private static async Task<double> GetDuration(string inputFile)
    {
        Process ffprobeProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfprobePath,
                Arguments = $"-i \"{inputFile}\" -show_entries format=duration -v quiet -of csv=p=0",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        ffprobeProcess.Start();
        string output = (await ffprobeProcess.StandardOutput.ReadToEndAsync()).Trim();
        await ffprobeProcess.WaitForExitAsync();

        return double.TryParse(output, NumberStyles.Float, CultureInfo.InvariantCulture, out double duration) ? duration : 0;
    }

    private static double TimeToSeconds(string timeString)
    {
        string[] parts = timeString.Split(':');
        if (parts.Length != 3)
            return 0;

        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double hours) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
        {
            return hours * 3600 + minutes * 60 + seconds;
        }

        return 0;
    }

    private static async Task ReadProgressAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            //await Console.Error.WriteLineAsync(line);
        }
    }

    private static async Task ReadProgressAsync(
        StreamReader reader,
        IProgress<long> progress, double totalDuration,
        CancellationToken cancellationToken)
    {
        long totalPercent = 0;
        Regex progressRegex = new Regex(@"out_time=(\d{2}:\d{2}:\d{2}\.\d+)", RegexOptions.Compiled);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            Match match = progressRegex.Match(line);
            if (match.Success)
            {
                string timeString = match.Groups[1].Value;
                double currentTime = TimeToSeconds(timeString);
                long percent = (long) (currentTime / totalDuration * 100);
                //Console.WriteLine($"Progress: {p:0.00}%");
                // Only report progress if percent is greater than the last reported value
                if (totalPercent < percent) progress.Report(totalPercent = percent);
            }
        }
    }
}