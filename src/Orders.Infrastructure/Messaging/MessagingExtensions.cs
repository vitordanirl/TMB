using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Orders.Domain.Exceptions;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Prefixo de endpoints que apenas repassam eventos (ex.: para clientes SignalR): não gravam no banco,
    /// então dispensam Outbox/Inbox. Duplicatas são inofensivas (o cliente aplica o mesmo estado).
    /// </summary>
    public const string NotificationEndpointPrefix = "notifications-";

    /// <summary>
    /// Configura MassTransit sobre RabbitMQ com Transactional Outbox/Inbox no PostgreSQL.
    /// </summary>
    /// <param name="services">Coleção de serviços.</param>
    /// <param name="rabbitMqConnectionString">URI AMQP, ex.: <c>amqp://user:pass@host:5672/vhost</c>.</param>
    /// <param name="configure">Registro de consumidores específicos de cada aplicação.</param>
    public static IServiceCollection AddOrdersMessaging(
        this IServiceCollection services,
        string rabbitMqConnectionString,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        var rabbitMq = new Uri(rabbitMqConnectionString, UriKind.Absolute);

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Outbox: mensagens publicadas são gravadas na mesma transação do DbContext e
            // entregues ao broker por um serviço em background. Inbox: deduplica por MessageId.
            bus.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.DuplicateDetectionWindow = TimeSpan.FromHours(1);
            });

            bus.AddConfigureEndpointsCallback((context, name, endpoint) =>
            {
                // A retentativa fica fora do outbox: cada tentativa usa um escopo/transação novo.
                endpoint.UseMessageRetry(retry =>
                {
                    retry.Exponential(5, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500));
                    retry.Ignore<DomainException>();
                });

                if (!name.StartsWith(NotificationEndpointPrefix, StringComparison.Ordinal))
                {
                    endpoint.UseEntityFrameworkOutbox<OrdersDbContext>(context);
                }
            });

            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMq.Host, (ushort)(rabbitMq.IsDefaultPort ? 5672 : rabbitMq.Port), VirtualHost(rabbitMq), host =>
                {
                    var (username, password) = Credentials(rabbitMq);
                    host.Username(username);
                    host.Password(password);
                });

                cfg.UsePublishFilter(typeof(OrderMessagePublishFilter<>), context);
                cfg.UseSendFilter(typeof(OrderMessageSendFilter<>), context);

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    private static string VirtualHost(Uri uri)
    {
        var path = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
        return path.Length == 0 ? "/" : path;
    }

    private static (string Username, string Password) Credentials(Uri uri)
    {
        if (string.IsNullOrEmpty(uri.UserInfo))
        {
            return ("guest", "guest");
        }

        var parts = uri.UserInfo.Split(':', 2);
        return (Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
    }
}
