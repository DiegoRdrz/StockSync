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
            Skip = "Set STOCKSYNC_TEST_POSTGRES and STOCKSYNC_TEST_RABBITMQ to run the broker projection tests.";
    }
}

public class BrokerProjectionTests : PostgresTestBase
{
    [BrokerPostgreSqlFact]
    public Task ConfirmedSaleDeliveredTwiceProducesOneQueryableSale() =>
        VerifyDelivery<SaleConfirmed, SalesProjection>("sale-confirmed", async (db, json, token) =>
        {
            var date = json.GetProperty("confirmedAt").GetDateTime();
            var summary = await new SalesReports(db).SummaryAsync(new SalesFilter(date, date, null), token);
            Assert.Equal(new SalesSummary(37.5m, 1, 37.5m, 3), summary);
        });

    [BrokerPostgreSqlFact]
    public Task ReceivedPurchaseDeliveredTwiceProducesOneQueryablePurchase() =>
        VerifyDelivery<PurchaseOrderReceived, PurchaseProjection>("purchase-order-received", async (db, json, token) =>
        {
            var date = json.GetProperty("receivedAt").GetDateTime();
            var summary = await new PurchaseReports(db).SummaryAsync(new SalesFilter(date, date, null), token);
            Assert.Equal(new PurchaseSummary(80, 1, 10), summary);
        });

    [BrokerPostgreSqlFact]
    public Task StockMovementDeliveredTwiceProducesOneMovementAndLevel() =>
        VerifyDelivery<StockMovementRecorded, StockMovementProjection>("stock-movement-recorded", async (db, json, token) =>
        {
            var id = json.GetProperty("movementId").GetGuid();
            Assert.Equal(id, (await db.StockMovements.SingleAsync(token)).MovementId);
            Assert.Equal(id, (await db.StockLevels.SingleAsync(token)).LastMovementId);
        });

    private async Task VerifyDelivery<T, TProjection>(string fixture,
        Func<ReportingDbContext, JsonElement, CancellationToken, Task> verify)
        where T : class, IReportingEvent<T> where TProjection : class, IProjection<T>
    {
        var broker = new Uri(Environment.GetEnvironmentVariable("STOCKSYNC_TEST_RABBITMQ")!);
        var body = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Contracts", fixture + ".v1.json"));
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
            .AddScoped<IProjection<T>, TProjection>().AddScoped<EventDelivery<T>>().BuildServiceProvider();
        var worker = new ReportingConsumer<T>(broker, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ReportingConsumer<T>>.Instance);
        var queue = $"reporting.test.{Guid.NewGuid():N}";
        var exchange = queue + ".events";
        await ReportingConsumer<T>.DeclareAsync(producer, queue, token, exchange);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var run = worker.ConsumeAsync(subscription, queue, stop.Token, exchange);
        try
        {
            var props = new BasicProperties
            {
                Type = T.RoutingKey, MessageId = json.RootElement.GetProperty("eventId").GetString(),
                ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { ["tenant-id"] = owner.ToString() }
            };
            await producer.BasicPublishAsync(exchange, T.RoutingKey, true, props, body, token);
            await producer.BasicPublishAsync(exchange, T.RoutingKey, true, props, body, token);
            // With prefetch one, the following invalid marker can reach the DLQ only after both deliveries settle.
            await producer.BasicPublishAsync(exchange, T.RoutingKey, true, props, Encoding.UTF8.GetBytes("{}"), token);
            BasicGetResult? error;
            while ((error = await producer.BasicGetAsync(queue + ".error", true, token)) is null)
            {
                if (run.IsCompleted) await run;
                await Task.Delay(50, token);
            }
            Assert.Equal("{}", Encoding.UTF8.GetString(error.Body.Span));
            await using var db = CreateContext(owner);
            await verify(db, json.RootElement, token);
            Assert.Equal(T.ConsumerName, (await db.ProcessedEvents.SingleAsync(token)).ConsumerName);
        }
        finally
        {
            stop.Cancel();
            try { await run; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            await producer.QueueDeleteAsync(queue, false, false);
            await producer.QueueDeleteAsync(queue + ".error", false, false);
            await producer.ExchangeDeleteAsync(exchange, cancellationToken: token);
        }
    }
}
