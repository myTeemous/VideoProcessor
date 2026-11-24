using FluentValidation;

namespace VideoProcessor.Api.Common;

/// <summary>Runs the FluentValidation validator for <typeparamref name="TRequest"/> before the endpoint handler.</summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();

        var request = context.Arguments.OfType<TRequest>().SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Endpoint '{context.HttpContext.GetEndpoint()?.DisplayName}' has no {typeof(TRequest).Name} argument to validate.");

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return validation.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(validation.ToDictionary());
    }
}

public static class ValidationExtensions
{
    /// <summary>Registers every validator in the Api assembly.</summary>
    public static IServiceCollection AddRequestValidation(this IServiceCollection services) =>
        services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

    /// <summary>Validates the endpoint's <typeparamref name="TRequest"/> argument, returning 400 ValidationProblem when invalid.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
