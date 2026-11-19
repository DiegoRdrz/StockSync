using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using StockSync.Reporting.Application;
using StockSync.Reporting.Application.Common;
using StockSync.Reporting.Infrastructure;

namespace StockSync.Reporting.IntegrationTests;

public sealed class BrokerPostgreSqlFactAttribute : FactAttribute
{
    public BrokerPostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")))
            Skip = "Set STOCKSYNC_TEST_POSTGRES and STOCKSYNC_TEST_RABBITMQ to run the sales delivery test.";
    }
}

public class SalesBrokerProjectionTests : PostgresTestBase
{
    [BrokerPostgreSqlFact]
    public async Task ConfirmedSaleDeliveredTwiceProducesOneQueryableSale()
    {
        var broker = new Uri(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")!);
        var body = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Contracts", "sale-confirmed.v1.json"));
        using var json = JsonDocument.Parse(body);
        var owner = json.RootElement.GetProperty("tenantId").GetGuid();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        await using var connection = await new ConnectionFactory { Uri = broker }.CreateConnectionAsync(token);
        await using var producer = await connection.CreateChannelAsync(cancellationToken: token);
        await using var subscription = await connection.CreateChannelAsync(cancellationToken: token);
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
            .AddScoped<TenantContext>().AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>())
            .AddDbContext<ReportingDbContext>(o => o.UseNpgsql(ConnectionString))
            .AddScoped<ISalesProjection, SalesProjection>().AddScoped<SalesDelivery>().BuildServiceProvider();
        var worker = new SalesConsumer(broker, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SalesConsumer>.Instance);
        var queue = $"reporting.test.{Guid.NewGuid():N}";
        var exchange = queue + ".events";
        await SalesConsumer.DeclareAsync(producer, queue, token);
        // Publish only to this test's queue while preserving the producer's routing key.
        await producer.ExchangeDeclareAsync(exchange, ExchangeType.Topic, false, true, cancellationToken: token);
        await producer.QueueBindAsync(queue, exchange, SaleConfirmed.RoutingKey, cancellationToken: token);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var run = worker.ConsumeAsync(subscription, queue, stop.Token);
        try
        {
            var props = new BasicProperties
            {
                Type = SaleConfirmed.RoutingKey, MessageId = json.RootElement.GetProperty("eventId").GetString(),
                ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { ["tenant-id"] = owner.ToString() }
            };
            await producer.BasicPublishAsync(exchange, SaleConfirmed.RoutingKey, true, props, body, token);
            await producer.BasicPublishAsync(exchange, SaleConfirmed.RoutingKey, true, props, body, token);
            // With prefetch one, the following invalid marker can reach the DLQ only after both sales settle.
            await producer.BasicPublishAsync(exchange, SaleConfirmed.RoutingKey, true, props, Encoding.UTF8.GetBytes("{}"), token);
            BasicGetResult? error;
            while ((error = await producer.BasicGetAsync(queue + ".error", true, token)) is null)
            {
                if (run.IsCompleted) await run;
                await Task.Delay(50, token);
            }
            Assert.Equal("{}", Encoding.UTF8.GetString(error.Body.Span));
            await using var db = CreateContext(owner);
            var date = json.RootElement.GetProperty("confirmedAt").GetDateTime();
            var summary = await new SalesReports(db).SummaryAsync(new SalesFilter(date, date, null), token);
            Assert.Equal(new SalesSummary(37.5m, 1, 37.5m, 3), summary);
            Assert.Single(await db.ProcessedEvents.ToListAsync(token));
        }
        finally
        {
            stop.Cancel();
            try { await run; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            await producer.QueueDeleteAsync(queue, false, false);
            await producer.QueueDeleteAsync(queue + ".error", false, false);
        }
    }
}
