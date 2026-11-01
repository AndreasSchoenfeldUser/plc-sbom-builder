using PlcSbom.Core;

namespace PlcSbom.Tests;

public sealed class WildcardTests
{
    [Theory]
    [InlineData("PLC_1", "PLC_1", true)]
    [InlineData("PLC_?", "PLC_1", true)]
    [InlineData("PLC_?", "PLC_12", false)]
    [InlineData("Testaufbau_*", "Testaufbau_", true)]
    [InlineData("Testaufbau_*", "Testaufbau_Halle 2", true)]
    [InlineData("*_1", "ET 200SP station_1", true)]
    [InlineData("*station*", "ET 200SP station_1", true)]
    [InlineData("plc_1", "PLC_1", false)]
    [InlineData("HMI", "HMI_1", false)]
    [InlineData("*", "", true)]
    [InlineData("a*b*c", "aXbYbZc", true)]
    [InlineData("a*b*c", "aXbYbZ", false)]
    public void Patterns_match_the_whole_text(string pattern, string text, bool expected)
    {
        Assert.Equal(expected, Wildcard.IsMatch(pattern, text));
    }

    [Theory]
    [InlineData("6EP1*", "6EP1332-4BA00", true)]
    [InlineData("6ES7511*", "6ES7 511-1AK02-0AB0", true)]
    [InlineData("6ES7 511-1AK0?-0AB0", "6ES7 511-1AK02-0AB0", true)]
    [InlineData("6ES7 512*", "6ES7 511-1AK02-0AB0", false)]
    [InlineData("6ES7 590-1*", "6ES7 590-1***0-0AA0", true)]
    public void Order_numbers_match_with_or_without_spaces(string pattern, string orderNumber, bool expected)
    {
        Assert.Equal(expected, Wildcard.IsOrderNumberMatch(pattern, orderNumber));
    }
}
