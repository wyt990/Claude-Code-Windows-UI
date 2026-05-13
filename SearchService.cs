using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ClaudeCodeGUI;

public class SearchResultItem
{
    public string FilePath { get; set; } = "";
    public List<MatchLine> Matches { get; set; } = new();
}

public class MatchLine
{
    public int LineNumber { get; set; }
    public string Context { get; set; } = "";
}

public static class SearchService
{
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".bin", ".obj", ".lib", ".pak", ".dat",
        ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".bmp",
        ".ttf", ".otf", ".woff", ".woff2",
        ".zip", ".tar", ".gz", ".rar", ".7z",
        ".mp3", ".mp4", ".avi", ".mov", ".wmv"
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", ".vs", ".svn",
        ".idea", "packages", "vendor", ".cache",
        "Debug", "Release", "x64", "x86"
    };

    public static async Task<List<SearchResultItem>> SearchInDirectory(
        string directory, string keyword, bool matchCase = false, bool wholeWord = false)
    {
        var results = new List<SearchResultItem>();
        if (!Directory.Exists(directory) || string.IsNullOrWhiteSpace(keyword))
            return results;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        await Task.Run(() =>
        {
            try
            {
                var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(f => !ExcludedExtensions.Contains(Path.GetExtension(f)))
                    .Where(f =>
                    {
                        var dir = Path.GetDirectoryName(f);
                        if (dir == null) return true;
                        var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        return !parts.Any(p => ExcludedDirectories.Contains(p));
                    })
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        var matchLines = new List<MatchLine>();
                        var lines = File.ReadAllLines(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            var matchFound = false;

                            if (wholeWord)
                            {
                                var searchIn = matchCase ? line : line.ToLowerInvariant();
                                var searchFor = matchCase ? keyword : keyword.ToLowerInvariant();
                                int idx = 0;
                                while ((idx = searchIn.IndexOf(searchFor, idx, comparison)) >= 0)
                                {
                                    bool wordBoundaryStart = idx == 0 || !char.IsLetterOrDigit(searchIn[idx - 1]);
                                    bool wordBoundaryEnd = idx + searchFor.Length >= searchIn.Length ||
                                                          !char.IsLetterOrDigit(searchIn[idx + searchFor.Length]);
                                    if (wordBoundaryStart && wordBoundaryEnd)
                                    {
                                        matchFound = true;
                                        break;
                                    }
                                    idx++;
                                }
                            }
                            else
                            {
                                matchFound = line.Contains(keyword, comparison);
                            }

                            if (matchFound)
                            {
                                matchLines.Add(new MatchLine
                                {
                                    LineNumber = i + 1,
                                    Context = line.Trim()
                                });
                            }
                        }

                        if (matchLines.Count > 0)
                        {
                            results.Add(new SearchResultItem
                            {
                                FilePath = file,
                                Matches = matchLines
                            });
                        }
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }
            catch
            {
                // Skip directories that can't be enumerated
            }
        });

        return results;
    }
}
