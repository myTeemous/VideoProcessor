using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using VideoProcessor.Api.Common;

namespace VideoProcessor.UnitTests.Common;

public sealed class ValidationFilterTests
{
    public sealed record CreateThingRequest(string Name, int Size);

    public sealed class CreateThingRequestValidator : AbstractValidator<CreateThingRequest>
    {
        public CreateThingRequestValidator()
        {
            RuleFor(r => r.Name).NotEmpty();
            RuleFor(r => r.Size).GreaterThan(0);
        }
    }

    [Fact]
    public async Task Invalid_request_returns_a_validation_problem_with_field_errors_and_skips_the_handler()
    {
        var handlerCalled = false;

        var result = await Invoke(new CreateThingRequest("", 0), _ =>
        {
            handlerCalled = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok());
        });

        handlerCalled.Should().BeFalse();
        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.StatusCode.Should().Be(400);
        problem.ProblemDetails.Errors.Keys.Should().BeEquivalentTo("Name", "Size");
    }

    [Fact]
    public async Task Valid_request_runs_the_handler()
    {
        var result = await Invoke(new CreateThingRequest("clip", 10), _ =>
            ValueTask.FromResult<object?>(TypedResults.Ok("handled")));

        result.Should().BeOfType<Ok<string>>().Which.Value.Should().Be("handled");
    }

    private static ValueTask<object?> Invoke(CreateThingRequest request, EndpointFilterDelegate next)
    {
        var services = new ServiceCollection()
            .AddSingleton<IValidator<CreateThingRequest>, CreateThingRequestValidator>()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var context = EndpointFilterInvocationContext.Create(httpContext, request);

        return new ValidationFilter<CreateThingRequest>().InvokeAsync(context, next);
    }
}
