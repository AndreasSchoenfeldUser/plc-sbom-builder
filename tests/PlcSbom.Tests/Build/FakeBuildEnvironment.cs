using PlcSbom.Core.Build;

namespace PlcSbom.Tests.Build;

/// <summary>Environment of a build under test: variables, clock and git answers are set by the test.</summary>
public sealed class FakeBuildEnvironment : IBuildEnvironment
{
    /// <summary>17.10.2026 11:00:00 UTC, the SOURCE_DATE_EPOCH of the sample.</summary>
    public const string Epoch = "1792234800";

    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 9, 30, 15, 500, TimeSpan.Zero);

    /// <summary>Answer per git call ("rev-parse HEAD", "log -1 --format=%ct &lt;sha&gt;"); missing: git fails.</summary>
    public Dictionary<string, string> GitAnswers { get; } = new(StringComparer.Ordinal);

    public List<string> GitCalls { get; } = [];

    public string? Variable(string name) => Variables.GetValueOrDefault(name);

    public string? Git(string workingDirectory, params string[] arguments)
    {
        var call = string.Join(' ', arguments);
        GitCalls.Add(call);
        return GitAnswers.GetValueOrDefault(call);
    }

    /// <summary>Reproducible environment: SOURCE_DATE_EPOCH set, git unavailable.</summary>
    public static FakeBuildEnvironment Reproducible()
    {
        var environment = new FakeBuildEnvironment();
        environment.Variables["SOURCE_DATE_EPOCH"] = Epoch;
        return environment;
    }
}
