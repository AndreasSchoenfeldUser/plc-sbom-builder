namespace PlcSbom.Core;

/// <summary>Format of user messages (CLAUDE.md section 7.2): stable English code, then German text.</summary>
public static class Messages
{
    public static string Format(string code, string message) => $"[{code}] {message}";
}
