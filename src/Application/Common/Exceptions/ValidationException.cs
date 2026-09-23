using FluentValidation.Results;

namespace BillingSaaS.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(FormatMessage(failures))
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());
    }

    private static string FormatMessage(IEnumerable<ValidationFailure> failures)
    {
        var failuresList = failures.ToList();
        if (failuresList.Count == 0)
            return "One or more validation failures have occurred.";

        var formattedErrors = string.Join(Environment.NewLine,
            failuresList.Select(f => $"  • [{f.PropertyName}]: {f.ErrorMessage}"));

        return $"One or more validation failures have occurred:{Environment.NewLine}{formattedErrors}";
    }

    public IDictionary<string, string[]> Errors { get; }
}
