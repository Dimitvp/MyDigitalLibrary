using FluentAssertions;
using MyDigitalLibrary.Domain.ValueObjects;

namespace MyDigitalLibrary.Domain.Tests.ValueObjects;

public class IsbnTests
{
    [Fact]
    public void TryCreate_accepts_a_valid_isbn13()
    {
        var result = Isbn.TryCreate("9780132350884");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("9780132350884");
    }

    [Fact]
    public void TryCreate_accepts_a_valid_isbn13_with_dashes_and_spaces()
    {
        var result = Isbn.TryCreate("978-0 13-235088-4");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("9780132350884");
    }

    [Fact]
    public void TryCreate_normalizes_a_valid_isbn10_to_isbn13()
    {
        var result = Isbn.TryCreate("0132350882");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("9780132350884");
    }

    [Fact]
    public void TryCreate_accepts_an_isbn10_with_an_x_check_digit()
    {
        var result = Isbn.TryCreate("097522980X");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("0132350880")] // bad ISBN-10 checksum
    [InlineData("9780132350880")] // bad ISBN-13 checksum
    [InlineData("not-an-isbn")]
    [InlineData("")]
    [InlineData(null)]
    public void TryCreate_rejects_invalid_input(string? raw)
    {
        var result = Isbn.TryCreate(raw);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("isbn.invalid");
    }
}
