using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Domain.Exceptions;
using Orders.Domain.Orders;

namespace Orders.Api.ErrorHandling;

/// <summary>
/// Traduz exceções conhecidas (domínio, concorrência, requisição malformada) em respostas
/// ProblemDetails (RFC 9457). Exceções não mapeadas seguem para o handler padrão (500).
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    // Nomes dos campos do domínio → nomes expostos no contrato HTTP.
    private static readonly Dictionary<string, string> FieldNames = new(StringComparer.Ordinal)
    {
        [nameof(Order.Customer)] = "cliente",
        [nameof(Order.Product)] = "produto",
        [nameof(Order.Amount)] = "valor",
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            DomainValidationException validation => new HttpValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [FieldNames.GetValueOrDefault(validation.Field, validation.Field)] = [validation.Message],
                })
            {
                Status = StatusCodes.Status400BadRequest,
            },
            InvalidStatusTransitionException transition => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Transição de status inválida.",
                Detail = transition.Message,
            },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflito de concorrência.",
                Detail = "O recurso foi alterado por outra operação. Tente novamente.",
            },
            // JSON malformado, tipos incompatíveis, corpo ausente etc.
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "Requisição inválida.",
                Detail = "O corpo da requisição não pôde ser interpretado. Verifique o JSON enviado.",
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
