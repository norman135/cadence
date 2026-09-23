using Cadence.Domain.Common;

namespace Cadence.Domain.UnitTests.Common;

public sealed class ResultTests
{
    private static readonly Error s_notFound = Error.NotFound("issues.not_found", "The issue does not exist.");

    [Fact]
    public void A_successful_result_has_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void A_failed_result_carries_its_error()
    {
        var result = Result.Failure(s_notFound);

        Assert.True(result.IsFailure);
        Assert.Equal(s_notFound, result.Error);
    }

    [Fact]
    public void A_failed_result_requires_an_error()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Fact]
    public void A_successful_result_exposes_its_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Reading_the_value_of_a_failed_result_throws()
    {
        var result = Result.Failure<int>(s_notFound);

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Values_and_errors_convert_implicitly()
    {
        Result<string> success = "done";
        Result<string> failure = s_notFound;
        Result plainFailure = s_notFound;

        Assert.Equal("done", success.Value);
        Assert.Equal(s_notFound, failure.Error);
        Assert.True(plainFailure.IsFailure);
    }

    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public void Error_factories_set_the_matching_type(ErrorType type)
    {
        var error = type switch
        {
            ErrorType.Failure => Error.Failure("code", "description"),
            ErrorType.Validation => Error.Validation("code", "description"),
            ErrorType.NotFound => Error.NotFound("code", "description"),
            ErrorType.Conflict => Error.Conflict("code", "description"),
            ErrorType.Unauthorized => Error.Unauthorized("code", "description"),
            ErrorType.Forbidden => Error.Forbidden("code", "description"),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        Assert.Equal(type, error.Type);
        Assert.Equal("code", error.Code);
    }
}
