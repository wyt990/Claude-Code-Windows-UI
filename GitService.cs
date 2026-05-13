using System.Diagnostics;

namespace ClaudeCodeGUI;

public static class GitService
{
    public static bool IsGitRepository(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --is-inside-work-tree")
            {
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd().Trim();
            process?.WaitForExit(500);
            return process?.ExitCode == 0 && output == "true";
        }
        catch
        {
            return false;
        }
    }

    public static string GetCurrentBranch(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --abbrev-ref HEAD")
            {
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd().Trim();
            process?.WaitForExit(500);
            return string.IsNullOrEmpty(output) ? "unknown" : output;
        }
        catch
        {
            return "unknown";
        }
    }

    public static (int staged, int unstaged) GetStatusCount(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "status --porcelain")
            {
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit(500);

            int staged = 0, unstaged = 0;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length < 2) continue;
                if (line[0] != ' ') staged++;
                if (line[1] != ' ') unstaged++;
            }
            return (staged, unstaged);
        }
        catch
        {
            return (0, 0);
        }
    }
}
