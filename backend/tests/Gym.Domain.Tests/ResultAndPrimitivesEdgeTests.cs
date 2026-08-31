using FluentAssertions;
using Gym.Domain.Common;
using Xunit;

namespace Gym.Domain.Tests;

public sealed class ResultAndPrimitivesEdgeTests
{
    public static TheoryData<Error, ErrorType> ErrorFactories => new()
    {
        { Error.Validation("validation", "Validation"), ErrorType.Validation },
        { Error.NotFound("notFound", "Not found"), ErrorType.NotFound },
        { Error.Conflict("conflict", "Conflict"), ErrorType.Conflict },
        { Error.Unauthorized("unauthorized", "Unauthorized"), ErrorType.Unauthorized },
        { Error.Forbidden("forbidden", "Forbidden"), ErrorType.Forbidden },
        { Error.Failure("failure", "Failure"), ErrorType.Failure },
    };

    [Fact]
    public void Success_result_has_no_error_and_failure_is_false()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Theory]
    [MemberData(nameof(ErrorFactories))]
    public void Failure_result_preserves_error(Error error, ErrorType expectedType)
    {
        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(expectedType);
        result.Error.Code.Should().Be(error.Code);
        result.Error.Message.Should().Be(error.Message);
    }

    [Fact]
    public void Generic_success_exposes_value()
    {
        var result = Result.Success("value");

        result.Value.Should().Be("value");
    }

    [Fact]
    public void Generic_failure_value_access_throws()
    {
        var result = Result.Failure<string>(Error.Validation("code", "message"));

        Action act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_on_success_transforms_value_and_keeps_success_state()
    {
        var mapped = Result.Success(21).Map(value => value * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(42);
    }

    [Fact]
    public void Map_on_failure_propagates_original_error_without_invoking_mapper()
    {
        var invoked = false;
        var error = Error.Conflict("conflict", "Nope");
        var mapped = Result.Failure<int>(error).Map(value =>
        {
            invoked = true;
            return value * 2;
        });

        invoked.Should().BeFalse();
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Implicit_conversion_creates_success_result()
    {
        Result<int> result = 5;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void Entity_default_id_is_empty_until_factory_sets_it()
    {
        var entity = new TestEntity();

        entity.Id.Should().BeEmpty();
    }

    private sealed class TestEntity : Entity
    {
    }
}
