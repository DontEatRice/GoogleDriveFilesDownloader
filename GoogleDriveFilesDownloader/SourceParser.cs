using System.Text.RegularExpressions;

namespace GoogleDriveFilesDownloader;

internal static partial class SourceParser
{
    [GeneratedRegex(@"^https:\/\/\w+\.google.com\/(\w*\/)?d\/")]
    private static partial Regex IsLinkRegex();
    [GeneratedRegex(@"\/d\/\S+\/")]
    private static partial Regex IdFromRegex();
    
    public static List<string> GetIdsFromSource(string source)
    {
        if (IsLinkRegex().IsMatch(source))
        {
            return [ExtractIdFromLink(source)];
        }

        if (Path.Exists(source))
        {
            return GetLinksFromFile(source);
        }

        return [source];
    }

    private static string ExtractIdFromLink(string link)
    {
        var match = IdFromRegex().Match(link);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Could not get file id from {link}. Ensure it is in correct format or extract the id manually");
        }

        return match.Value.Replace("/d/", "").TrimEnd('/');
    }

    private static List<string> GetLinksFromFile(string path)
    {
        var lines = File.ReadAllLines(path);
        var ids = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            if (IsLinkRegex().IsMatch(line))
            {
                ids.Add(ExtractIdFromLink(line));
            }
            else
            {
                ids.Add(line.Trim());
            }
        }

        return ids;
    }
}