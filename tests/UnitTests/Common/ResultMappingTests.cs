using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using VideoProcessor.Api.Common;
using VideoProcessor.Core.Domain;

namespace VideoProcessor.UnitTests.Common;

public sealed class ResultMappingTests
{
    [Fact]
    public void Success_uses_the_success_mapping()
    {
        Result<int> result = 42;

        var http = result.ToHttpResult(value => TypedResults.Ok(value));

        http.Should().BeOfType<Ok<int>>().Which.Value.Should().Be(42);
    }

    [Fact]
    public void Validation_error_becomes_a_400_problem()
    {
        Result<int> result = Error.Validation("Video.TooLarge", "The file is larger than 5 GB.");

        var problem = result.ToHttpResult(value => TypedResults.Ok(value)).Should().BeOfType<ProblemHttpResult>().Subject;

        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Detail.Should().Be("The file is larger than 5 GB.");
        problem.ProblemDetails.Extensions.Should().Contain("code", "Video.TooLarge");
    }

    [Fact]
    public void Not_found_error_becomes_a_404_problem()
    {
        Result<int> result = Error.NotFound("Video.NotFound", "The Video was not found.");

        var problem = result.ToHttpResult(value => TypedResults.Ok(value)).Should().BeOfType<ProblemHttpResult>().Subject;

        problem.StatusCode.Should().Be(404);
        problem.ProblemDetails.Extensions.Should().Contain("code", "Video.NotFound");
    }

    [Fact]
    public void Conflict_error_becomes_a_409_problem()
    {
        Result<int> result = Error.Conflict("Video.AlreadyConfirmed", "The Video has already been confirmed.");

        var problem = result.ToHttpResult(value => TypedResults.Ok(value)).Should().BeOfType<ProblemHttpResult>().Subject;

        problem.StatusCode.Should().Be(409);
        problem.ProblemDetails.Detail.Should().Be("The Video has already been confirmed.");
    }
}
