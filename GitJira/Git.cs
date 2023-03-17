using System.Diagnostics;

namespace GitJira;

public static class Git
{
    public static string GetOriginOrFail()
    {
        using var git = Run("remote", "get-url", "origin");

        string? output = git.StandardOutput.ReadLine();

        git.WaitForExit();

        if (string.IsNullOrWhiteSpace(output) || git.ExitCode != 0)
            Program.Fail($"can't get 'origin' remote");

        return output;
    }

    public static string GetConfigOrFail(string name)
    {
        using var git = Run("config", name);

        string? output = git.StandardOutput.ReadLine();

        git.WaitForExit();

        if (string.IsNullOrWhiteSpace(output) || git.ExitCode != 0)
            Program.Fail($"'{name}' must be set in git config");

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

class Commit : IEquatable<Commit>
{
    public Commit(string hash, string? title = null)
    {
        Hash = hash;
        Title = title;
    }

    public string Hash { get; }
    public string ShortHash => Hash[..8];
    public string? Title { get; set; }

    public override string ToString()
        => Title == null ? ShortHash : $"{ShortHash} {Title}";

    public bool Equals(Commit? other)
        => Hash == other?.Hash;

    public override bool Equals(object? obj)
        => obj is Commit commit && Equals(commit);

    public override int GetHashCode()
        => Hash.GetHashCode();
}
