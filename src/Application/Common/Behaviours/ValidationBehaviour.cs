using Microsoft.Extensions.Logging;
using ValidationException = BillingSaaS.Application.Common.Exceptions.ValidationException;

namespace BillingSaaS.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehaviour<TRequest, TResponse>> _logger;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators, ILogger<ValidationBehaviour<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var validationResults = await Task.WhenAll(
                _validators.Select(v =>
                    v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));

            var failures = validationResults
                .Where(r => r.Errors.Any())
                .SelectMany(r => r.Errors)
                .ToList();

            if (failures.Count != 0)
            {
                var requestName = typeof(TRequest).Name;
                var formattedErrors = string.Join(Environment.NewLine,
                    failures.Select(f => $"   --> [{f.PropertyName}]: {f.ErrorMessage}"));

                _logger.LogWarning("Validación fallida para {RequestName} ({FailureCount} error(es)):{NewLine}{Errors}",
                    requestName, failures.Count, Environment.NewLine, formattedErrors);

                throw new ValidationException(failures);
            }
        }

        return await next();
    }
}
