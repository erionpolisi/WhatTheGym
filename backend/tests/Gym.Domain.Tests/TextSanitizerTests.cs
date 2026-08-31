using FluentAssertions;
using Gym.Domain.Common;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class TextSanitizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Null_empty_or_whitespace_only_inputs_return_null(string? input)
    {
        TextSanitizer.Sanitize(input).Should().BeNull();
    }

    [Theory]
    [InlineData("  Hallo  ", "Hallo")]
    [InlineData("\tHallo Welt\t", "Hallo Welt")]
    [InlineData("\r\nHallo\r\n", "Hallo")]
    [InlineData("A\r\nB", "A\nB")]
    [InlineData("A\rB", "AB")]
    [InlineData("A\u0000B", "AB")]
    [InlineData("A\u001fB", "AB")]
    [InlineData("<b>Kein HTML Filter</b>", "<b>Kein HTML Filter</b>")]
    [InlineData("https://example.at bleibt", "https://example.at bleibt")]
    [InlineData("äöü ÄÖÜ ß 😀", "äöü ÄÖÜ ß 😀")]
    [InlineData("Mehrere   Leerzeichen", "Mehrere   Leerzeichen")]
    public void Sanitizer_trims_normalizes_crlf_and_removes_control_characters(string input, string expected)
    {
        TextSanitizer.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Very_long_input_is_returned_without_truncation()
    {
        var input = " " + new string('x', 5000) + " ";

        TextSanitizer.Sanitize(input).Should().Be(new string('x', 5000));
    }
}
