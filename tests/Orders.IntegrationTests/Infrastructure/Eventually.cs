using System.Runtime.CompilerServices;

namespace Orders.IntegrationTests.Infrastructure;

/// <summary>Espera ativa para efeitos assíncronos (mensageria), com timeout e mensagem clara na falha.</summary>
public static class Eventually
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    public static async Task<T> Until<T>(
        Func<Task<T>> probe,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        [CallerArgumentExpression(nameof(condition))] string? description = null)
    {
        using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);
        Exception? lastError = null;
        T last = default!;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                last = await probe();
                if (condition(last))
                {
                    return last;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                lastError = ex;
            }

            try
            {
                await Task.Delay(PollInterval, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Condição não satisfeita em {(timeout ?? DefaultTimeout).TotalSeconds:0}s: {description}. Último valor: {last}",
            lastError);
    }
}
