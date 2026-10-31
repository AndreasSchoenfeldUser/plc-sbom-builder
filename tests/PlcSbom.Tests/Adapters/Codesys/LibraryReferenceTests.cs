using PlcSbom.Core.Adapters.Codesys;

namespace PlcSbom.Tests.Adapters.Codesys;

public sealed class LibraryReferenceTests
{
    [Theory]
    // Values as written in the fixtures.
    [InlineData("SM3_Basic, 4.5.1.0 (3S - Smart Software Solutions GmbH)", "SM3_Basic", "4.5.1.0", "3S - Smart Software Solutions GmbH")]
    [InlineData("Breakpoint Logging Functions, 3.5.5.0 (3S - Smart Software Solutions GmbH)", "Breakpoint Logging Functions", "3.5.5.0", "3S - Smart Software Solutions GmbH")]
    [InlineData("Robin, 0.1.2 (INESC TEC)", "Robin", "0.1.2", "INESC TEC")]
    [InlineData("CmpErrors Interfaces, * (System)", "CmpErrors Interfaces", "*", "System")]
    [InlineData("CAA Types Extern, 3.5.3.0 (CAA Technical Workgroup)", "CAA Types Extern", "3.5.3.0", "CAA Technical Workgroup")]
    [InlineData("3SLicense, 0.0.0.0 (3S - Smart Software Solutions GmbH)", "3SLicense", "0.0.0.0", "3S - Smart Software Solutions GmbH")]
    // Title with a comma: the version is the last ", <token>" before the company.
    [InlineData("Lib, mit Komma, 1.0.0.0 (Firma)", "Lib, mit Komma", "1.0.0.0", "Firma")]
    public void Parses_title_version_and_company(string text, string title, string version, string company)
    {
        var parsed = LibraryReference.Parse(text);

        Assert.NotNull(parsed);
        Assert.Equal(title, parsed.Title);
        Assert.Equal(version, parsed.Version);
        Assert.Equal(company, parsed.Company);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Kaputt")]
    [InlineData("#SM3_Basic")]
    [InlineData("Lib, 1.0.0.0")]
    [InlineData("Lib (Firma)")]
    public void Rejects_text_that_does_not_follow_the_pattern(string? text)
    {
        Assert.Null(LibraryReference.Parse(text));
    }

    [Fact]
    public void Star_means_newest_installed_version()
    {
        Assert.True(LibraryReference.Parse("Standard, * (System)")!.IsNewest);
        Assert.False(LibraryReference.Parse("Standard, 3.5.18.0 (System)")!.IsNewest);
    }
}
