using BillingSaaS.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BillingSaaS.Web.Infrastructure;

/// <summary>
/// Converts well-known application exceptions into RFC 9110-compliant <see cref="ProblemDetails"/> responses,
/// mapping <see cref="ValidationException"/> → 400, <see cref="NotFoundException"/> → 404,
/// <see cref="UnauthorizedAccessException"/> → 401, and <see cref="ForbiddenAccessException"/> → 403.
/// Unrecognised exceptions are not handled and fall through to the default middleware.
/// </summary>
public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, (ProblemDetails)new ValidationProblemDetails(ve.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            }),
            NotFoundException ne => (StatusCodes.Status404NotFound, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Title = "The specified resource was not found.",
                Detail = ne.Message
            }),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
            }),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            }),
            HttpRequestException hre => (StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Error de comunicación con el SRI",
                Detail = hre.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.3"
            }),
            BillingSaaS.Domain.Exceptions.SubscriptionRequiredException sre => (StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Status = StatusCodes.Status402PaymentRequired,
                Title = "Suscripción requerida",
                Detail = sre.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.3"
            }),
            BillingSaaS.Domain.Exceptions.SubscriptionExpiredException see => (StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Status = StatusCodes.Status402PaymentRequired,
                Title = "Suscripción expirada",
                Detail = see.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.3"
            }),
            BillingSaaS.Domain.Exceptions.SubscriptionLimitExceededException sle => (StatusCodes.Status402PaymentRequired, new ProblemDetails
            {
                Status = StatusCodes.Status402PaymentRequired,
                Title = "Límite de documentos excedido",
                Detail = sle.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.3"
            }),
            BillingSaaS.Domain.Exceptions.DocumentTypeNotAllowedException dte => (StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Tipo de comprobante no permitido",
                Detail = dte.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            }),
            BillingSaaS.Domain.Exceptions.EstablishmentLimitExceededException ele => (StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Límite de establecimientos excedido",
                Detail = ele.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            }),
            InvalidOperationException ioe => (StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operación no permitida",
                Detail = ioe.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            })
        };

        if (problemDetails is null) return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
