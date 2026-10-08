using System.Text.Json;

namespace Orders.Api.ErrorHandling;

/// <summary>Padroniza todas as respostas ProblemDetails da API.</summary>
internal static class ProblemDetailsConventions
{
    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        if (problem.Status == StatusCodes.Status404NotFound)
        {
            problem.Title = "Recurso não encontrado.";
        }

        if (problem is HttpValidationProblemDetails validation)
        {
            NormalizeValidation(validation);
        }
    }

    private static void NormalizeValidation(HttpValidationProblemDetails validation)
    {
        validation.Title = "Um ou mais campos são inválidos.";

        // A validação nativa usa o nome da propriedade C# (ex.: "Cliente");
        // o contrato HTTP é snake_case (ex.: "cliente"). Normaliza as chaves.
        var normalized = validation.Errors.ToDictionary(
            pair => JsonNamingPolicy.SnakeCaseLower.ConvertName(pair.Key),
            pair => pair.Value,
            StringComparer.Ordinal);

        validation.Errors.Clear();
        foreach (var (field, messages) in normalized)
        {
            validation.Errors[field] = messages;
        }
    }
}
