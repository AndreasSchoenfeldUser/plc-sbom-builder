namespace PlcSbom.Core;

/// <summary>
/// Error that ends a command with a defined exit code. <see cref="Code"/> is a stable English
/// identifier (e.g. GSDML_NOT_FOUND), the message is German user text in full sentences.
/// </summary>
public abstract class PlcSbomException : Exception
{
    protected PlcSbomException(string code, string message, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
    }

    public string Code { get; }

    public abstract int ExitCode { get; }

    /// <summary>User-facing line: <c>[CODE] Message</c>.</summary>
    public string UserMessage => Messages.Format(Code, Message);
}

/// <summary>Call or configuration wrong (exit 2).</summary>
public sealed class UsageException : PlcSbomException
{
    public UsageException(string code, string message, Exception? inner = null)
        : base(code, message, inner)
    {
    }

    public override int ExitCode => ExitCodes.Usage;
}

/// <summary>The environment prevents the command (exit 3).</summary>
public sealed class EnvironmentException : PlcSbomException
{
    public EnvironmentException(string code, string message, Exception? inner = null)
        : base(code, message, inner)
    {
    }

    public override int ExitCode => ExitCodes.Environment;
}

/// <summary>The run was aborted: timeout, crashed worker (exit 4).</summary>
public sealed class AbortedException : PlcSbomException
{
    public AbortedException(string code, string message, Exception? inner = null)
        : base(code, message, inner)
    {
    }

    public override int ExitCode => ExitCodes.Aborted;
}
