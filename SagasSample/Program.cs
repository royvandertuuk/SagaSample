using Hangfire;
using MassTransit;
using MassTransit.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using SagasSample.Events;
using SagasSample.Sagas;
using System.Reflection;

namespace SagasSample;

internal class Program
{
    private const string _connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=SagaSample";
    private const bool _useMediator = false;

    public static async Task Main(string[] args)
    {
        var cancelSource = new CancellationTokenSource();
        var host = _useMediator ? CreateHostBuilderForMediator(args).Build() : CreateHostBuilderForRabbitMq(args).Build();
        var hostTask = host.RunAsync(cancelSource.Token);

        string? input;

        do
        {
            input = Console.ReadLine();
            string? command = GetCommand(input);
            if (command is null)
            {
                continue;
            }

            Guid orderId = GetOrderId(input);

            var message = GetMessage(command, orderId);
            if (message is null)
            {
                continue;
            }

            if (_useMediator)
            {
                var mediator = host.Services.GetRequiredService<IMediator>();
                await mediator.Send(message);
            } else
            {
                var bus = host.Services.GetRequiredService<IBus>();
                await bus.Publish(message);
            }
        }
        while (input != "q");

        cancelSource.Cancel();
        await hostTask;
    }

    private static Guid GetOrderId(string input)
    {
        var components = input.Split(" ");
        return components.Length > 1 ? Guid.Parse(components[1]) : Guid.NewGuid();
    }

    private static string? GetCommand(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var components = input.Split(" ");
        return components[0];
    }

    private static object? GetMessage(string? command, Guid orderId)
    {
        return command switch
        {
            "create" => new OrderCreatedEvent
            {
                OrderId = orderId
            },
            "pay" => new OrderPaymentReceivedEvent
            {
                OrderId = orderId
            },
            "ship" => new OrderShippedEvent
            {
                OrderId = orderId
            },
            _ => null
        };
    }
    public static IHostBuilder CreateHostBuilderForRabbitMq(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddOptions<RabbitMqTransportOptions>()
                .Configure(options =>
                {
                    options.Host = "192.168.2.157";
                    options.ManagementPort = 49157;
                    options.Port = 49159;
                    options.User = "guest";
                    options.Pass = "guest";
                });

                services.AddMassTransit(o =>
                {
                    o.SetKebabCaseEndpointNameFormatter();

                    var entryAssembly = Assembly.GetEntryAssembly();
                    o.AddConsumers(entryAssembly);
                    o.AddSagaStateMachine<OrderStateMachine, OrderState>()
                       .EntityFrameworkRepository(r =>
                       {
                           r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                           r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
                           {
                               builder.UseSqlServer(_connectionString, m =>
                               {
                                   m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                                   m.MigrationsHistoryTable($"_{nameof(OrderStateDbContext)}");
                               });
                           });
                       });

                    o.AddSagas(entryAssembly);
                    o.AddActivities(entryAssembly);

                    o.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.UseDelayedMessageScheduler();
                        cfg.ConfigureEndpoints(context);

                        cfg.ExchangeType = ExchangeType.Fanout;
                        cfg.Durable = true;
                    });
                });
            });
    }

    public static IHostBuilder CreateHostBuilderForMediator(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHangfire(h =>
                {
                    h.UseRecommendedSerializerSettings();
                    h.UseSqlServerStorage(_connectionString);
                });

                services.AddMassTransit(o =>
                {
                    var entryAssembly = Assembly.GetEntryAssembly();
                    o.AddConsumers(entryAssembly);

                    o.AddSagaStateMachine<OrderStateMachine, OrderState>()
                       .EntityFrameworkRepository(r =>
                       {
                           r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                           r.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
                           {
                               builder.UseSqlServer(_connectionString, m =>
                               {
                                   m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                                   m.MigrationsHistoryTable($"_{nameof(OrderStateDbContext)}");
                               });
                           });
                       });

                    o.AddSagas(entryAssembly);
                    o.AddActivities(entryAssembly);

                    o.AddMediator(cfg =>
                    {
                        cfg.AddSagaStateMachines(entryAssembly);
                        cfg.AddSagas(entryAssembly);
                    });
                });
            });
    }
}