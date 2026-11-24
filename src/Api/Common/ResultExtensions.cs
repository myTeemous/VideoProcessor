using VideoProcessor.Core.Domain;

namespace VideoProcessor.Api.Common;

/// <summary>Turns handler <see cref="Result{T}"/>s into HTTP responses; failures become ProblemDetails.</summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, ToProblem);

    public static IResult ToProblem(this Error error) =>
        TypedResults.Problem(
            detail: error.Message,
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unknown error type."),
            },
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
