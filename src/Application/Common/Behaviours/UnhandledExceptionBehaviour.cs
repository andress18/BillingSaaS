using Microsoft.Extensions.Logging;
using ValidationException = BillingSaaS.Application.Common.Exceptions.ValidationException;

namespace BillingSaaS.Application.Common.Behaviours;

public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (ValidationException)
        {
            // Las fallas de validación son esperadas y son registradas con detalle en ValidationBehaviour
            throw;
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            _logger.LogError(ex, "BillingSaaS Request: Unhandled Exception for Request {Name} {@Request}", requestName, request);

            throw;
        }
    }
}
