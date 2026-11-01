namespace PlcSbom.Core.Adapters;

/// <summary>
/// Something in the source the adapter could not fully evaluate (unknown writer version, role or type
/// identifier). Processing goes on; the warning is shown to the user and is not part of the SBOM.
/// <see cref="Code"/> is a stable English identifier, <see cref="Message"/> German user text.
/// </summary>
public sealed record AdapterWarning(string Code, string Message)
{
    public string UserMessage => Messages.Format(Code, Message);
}
