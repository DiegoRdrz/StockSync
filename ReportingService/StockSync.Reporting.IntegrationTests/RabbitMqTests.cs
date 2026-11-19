using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;

namespace StockSync.Reporting.IntegrationTests;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")))
            Skip = "Set STOCKSYNC_TEST_RABBITMQ to run against RabbitMQ.";
    }
}

public class RabbitMqTests
{
    [RabbitMqFact]
    public Task InvalidSaleReachesItsErrorQueue() => RejectDelivery<SaleConfirmed, SalesProjection>();

    [RabbitMqFact]
    public Task InvalidPurchaseReachesItsErrorQueue() => RejectDelivery<PurchaseOrderReceived, PurchaseProjection>();

    [RabbitMqFact]
    public Task InvalidMovementReachesItsErrorQueue() => RejectDelivery<StockMovementRecorded, StockMovementProjection>();

    private static async Task RejectDelivery<T, TProjection>()
        where T : class, IReportingEvent<T> where TProjection : class, IProjection<T>
    {
        var broker = new Uri(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")!);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = timeout.Token;
        await using var connection = await new ConnectionFactory { Uri = broker }.CreateConnectionAsync(token);
        await using var producer = await connection.CreateChannelAsync(cancellationToken: token);
        await using var subscription = await connection.CreateChannelAsync(cancellationToken: token);
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
            .AddScoped<TenantContext>().AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>())
            .AddDbContext<ReportingDbContext>(o => o.UseNpgsql())
            .AddScoped<IProjection<T>, TProjection>().AddScoped<EventDelivery<T>>().BuildServiceProvider();
        var worker = new ReportingConsumer<T>(broker, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ReportingConsumer<T>>.Instance);
        var queue = $"reporting.test.{Guid.NewGuid():N}";
        var exchange = queue + ".events";
        var error = queue + ".error";
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        await ReportingConsumer<T>.DeclareAsync(producer, queue, token, exchange);
        var run = worker.ConsumeAsync(subscription, queue, stop.Token, exchange);
        try
        {
            // The default exchange routes this invalid delivery only to this test's queue.
            var body = "invalid"u8.ToArray();
            await producer.BasicPublishAsync("", queue, body, token);
            BasicGetResult? rejected;
            while ((rejected = await producer.BasicGetAsync(error, true, token)) is null)
            {
                if (run.IsCompleted) await run;
                await Task.Delay(50, token);
            }
            Assert.Equal(body, rejected.Body.ToArray());
            Assert.True(rejected.BasicProperties.Headers!.ContainsKey("x-death"));
        }
        finally
        {
            stop.Cancel();
            try { await run; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            await producer.QueueDeleteAsync(queue, false, false);
            await producer.QueueDeleteAsync(error, false, false);
            await producer.ExchangeDeleteAsync(exchange, cancellationToken: token);
        }
    }
}
