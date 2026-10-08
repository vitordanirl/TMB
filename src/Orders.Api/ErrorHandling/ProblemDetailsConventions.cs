using System.Text.Json;

namespace Orders.Api.ErrorHandling;

/// <summary>Padroniza todas as respostas ProblemDetails da API.</summary>
internal static class ProblemDetailsConventions
{
    public static void Apply(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails validation)
        {
            return;
        }

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
