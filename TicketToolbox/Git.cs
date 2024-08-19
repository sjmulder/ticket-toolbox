using System.Diagnostics;

namespace TicketToolbox;

static class Git
{
    public static string GetOriginOrFail()
    {
        using var git = Run("remote", "get-url", "origin");

        string? output = git.StandardOutput.ReadLine();

        git.WaitForExit();

        if (string.IsNullOrWhiteSpace(output) || git.ExitCode != 0)
            throw new Exception($"can't get 'origin' remote");

        return output;
    }

    public static string GetRequiredConfig(string name)
    {
        using var git = Run("config", name);

        string? output = git.StandardOutput.ReadLine();

        git.WaitForExit();

        if (string.IsNullOrWhiteSpace(output))
            throw new Exception($"empty git config for {name}");
        if (git.ExitCode != 0)
            throw new SubprocessException(git);

        return output;
    }

    public static Process Run(params string[] args)
    {
        var git = new Process();
        git.StartInfo.FileName = "git";
        git.StartInfo.UseShellExecute = false;
        git.StartInfo.CreateNoWindow = true;
        git.StartInfo.RedirectStandardOutput = true;

        foreach (var arg in args)
            git.StartInfo.ArgumentList.Add(arg);

        if (Program.Verbose)
            Console.WriteLine($"+ git {string.Join(" ", args)}");

        git.Start();

        return git;
    }
}

class Commit(string hash, string? title = null) : IEquatable<Commit>
{
    public string Hash { get; } = hash;
    public string ShortHash => Hash[..8];
    public string? Title { get; set; } = title;

    public override string ToString()
        => Title == null ? ShortHash : $"{ShortHash} {Title}";

    public bool Equals(Commit? other)
        => Hash == other?.Hash;

    public override bool Equals(object? obj)
        => obj is Commit commit && Equals(commit);

    public override int GetHashCode()
        => Hash.GetHashCode();
}
