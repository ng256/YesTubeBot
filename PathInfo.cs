using System.Reflection;
using System.Text.RegularExpressions;

namespace VideoDownloader;

internal enum WorkFolder
{
    Default,
    Downloads,
    Application,
    Current,
    Temp
}

internal static class PathInfo
{
    public static string GetValidPath(string sourcePath, string? replacement = null)
    {
        if (string.IsNullOrWhiteSpace(replacement))
        {
            replacement = "_";
        }

        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        invalidChars = new string(invalidChars.Distinct().ToArray());
        Regex containsABadChar = new Regex($"[\\s{Regex.Escape(invalidChars)}]");
        return containsABadChar.Replace(sourcePath, replacement);
    }

    public static string GetFolderPath(string root, string? name = null)
    {
        string folder = string.IsNullOrWhiteSpace(name) ? root : Path.Combine(path1: root, GetValidPath(name));
        return folder;
    }

    public static string GetFolderPath(WorkFolder systemFolder, string? name = null)
    {
        string root = string.Empty;

        switch (systemFolder)
        {
            case WorkFolder.Downloads:
                root = GetFolderPath(Environment.SpecialFolder.UserProfile, "Downloads");
                break;
            case WorkFolder.Application:
                root = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
                break;
            case WorkFolder.Current:
                root = Directory.GetCurrentDirectory();
                break;
            case WorkFolder.Temp:
                root = Path.GetTempPath();
                break;
        }

        return GetFolderPath(root, name);
    }

    public static string GetFolderPath(Environment.SpecialFolder specialFolder, string? name)
    {
        string root = Environment.GetFolderPath(specialFolder);
        return GetFolderPath(root, name);
    }
}