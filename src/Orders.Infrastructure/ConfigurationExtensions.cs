using Microsoft.Extensions.Configuration;

namespace Orders.Infrastructure;

public static class ConfigurationExtensions
{
    /// <summary>Lê uma connection string obrigatória, com mensagem clara indicando a variável de ambiente.</summary>
    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"Connection string '{name}' não configurada. Defina a variável de ambiente ConnectionStrings__{name}.")
            : value;
    }
}
