using System.Diagnostics;
using BuildingBlocks.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// Logs every command/query with its duration, and the error code when a
/// handler returns a failed <see cref="Result"/>.
/// </summary>
public sealed class RequestLoggingBehavior<TRequest, TResponse>(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        if (response is Result { IsFailure: true } failure)
        {
            logger.LogInformation("{RequestName} failed with {ErrorCode} in {ElapsedMs} ms",
                requestName, failure.Error.Code, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            logger.LogDebug("{RequestName} handled in {ElapsedMs} ms", requestName, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
