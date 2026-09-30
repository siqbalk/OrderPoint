using BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace BuildingBlocks.Web;

/// <summary>
/// Maps <see cref="Result"/>/<see cref="Error"/> to RFC 9457 problem details so
/// every module's endpoints return failures in the same shape.
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bad Request"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error"),
        };

        return HttpResults.Problem(
            statusCode: status,
            title: title,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
        => result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess ? HttpResults.NoContent() : result.Error.ToProblem();
}
