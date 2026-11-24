using System.Text.Json;
using FluentValidation;

namespace VideoProcessor.Api.Common;

/// <summary>Runs the FluentValidation validator for <typeparamref name="TRequest"/> before the endpoint handler.</summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { [""] = ["A request body is required."] });
        }

        var validation = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (validation.IsValid)
        {
            return await next(context);
        }

        // Error keys follow the JSON property names clients send (camelCase), not the C# names.
        var errors = validation.Errors
            .GroupBy(failure => ToJsonPath(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());

        return TypedResults.ValidationProblem(errors);
    }

    private static string ToJsonPath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
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
