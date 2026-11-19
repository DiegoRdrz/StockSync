using System.Net;
using System.Text;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.Inventory;
using StockSync.Orders.Infrastructure.Inventory;

namespace StockSync.Orders.IntegrationTests;

// Contract with the Inventory API, checked against canned HTTP answers (no network or database).
public class InventoryHttpClientTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static (InventoryHttpClient Client, FakeHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://inventory.test/") };
        return (new InventoryHttpClient(httpClient, new Tenant(TenantId)), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetProduct_ReadsProductAndForwardsTenant()
    {
        var productId = Guid.NewGuid();
        var (client, handler) = Create(_ => Json(HttpStatusCode.OK,
            $$"""{"id":"{{productId}}","name":"Hammer","sku":"HAM-001","purchasePrice":8.5,"salePrice":12.75,"isActive":true}"""));

        var product = await client.GetProductAsync(productId, default);

        Assert.Equal(new InventoryProduct(productId, "Hammer", "HAM-001", 8.5m, 12.75m), product);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/api/products/{productId}", request.Path);
        Assert.Equal(TenantId.ToString(), request.Tenant);
    }

    [Fact]
    public async Task GetProduct_NotFound_ReturnsNull()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.NotFound, """{"title":"Resource not found."}"""));

        Assert.Null(await client.GetProductAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetProduct_ServerError_ThrowsInventoryUnavailable()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.InternalServerError, "{}"));

        await Assert.ThrowsAsync<InventoryUnavailableException>(() => client.GetProductAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task FindStockId_ScansPagesUntilBranchIsFound()
    {
        var productId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var stockId = Guid.NewGuid();
        var (client, handler) = Create(request => request.RequestUri!.Query.Contains("page=1&")
            ? Json(HttpStatusCode.OK,
                $$"""{"items":[{"id":"{{Guid.NewGuid()}}","productId":"{{productId}}","branchId":"{{Guid.NewGuid()}}","quantity":1}],"page":1,"pageSize":100,"total":2,"totalPages":2}""")
            : Json(HttpStatusCode.OK,
                $$"""{"items":[{"id":"{{stockId}}","productId":"{{productId}}","branchId":"{{branchId}}","quantity":4}],"page":2,"pageSize":100,"total":2,"totalPages":2}"""));

        var found = await client.FindStockIdAsync(productId, branchId, default);

        Assert.Equal(stockId, found);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task FindStockId_BranchNotAssigned_ReturnsNull()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.OK,
            """{"items":[],"page":1,"pageSize":100,"total":0,"totalPages":0}"""));

        Assert.Null(await client.FindStockIdAsync(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Issue_Created_ReturnsAppliedMovement()
    {
        var stockId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        var (client, handler) = Create(_ => Json(HttpStatusCode.Created,
            $$"""{"id":"{{movementId}}","stockId":"{{stockId}}","type":"Issue","quantity":2,"previousQuantity":5,"newQuantity":3,"date":"2026-11-17T10:00:00Z"}"""));

        var result = await client.IssueAsync(stockId, 2, default);

        Assert.Equal(StockMovementResult.Applied(movementId), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/api/stock/{stockId}/issues", request.Path);
        Assert.Equal("""{"quantity":2}""", request.Body);
    }

    [Fact]
    public async Task Receive_UsesReceiptsEndpoint()
    {
        var stockId = Guid.NewGuid();
        var (client, handler) = Create(_ => Json(HttpStatusCode.Created, $$"""{"id":"{{Guid.NewGuid()}}"}"""));

        await client.ReceiveAsync(stockId, 1, default);

        Assert.Equal($"/api/stock/{stockId}/receipts", Assert.Single(handler.Requests).Path);
    }

    [Fact]
    public async Task Issue_Conflict_ReturnsRejectedWithInventoryDetail()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.Conflict,
            """{"title":"Insufficient stock.","status":409,"detail":"Insufficient stock. Available: 1; requested: 3. Cannot record the issue."}"""));

        var result = await client.IssueAsync(Guid.NewGuid(), 3, default);

        Assert.Equal(StockMovementOutcome.Rejected, result.Outcome);
        Assert.StartsWith("Insufficient stock. Available: 1", result.Error);
    }

    [Fact]
    public async Task Issue_ServerError_ReturnsUnknown()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.InternalServerError, "{}"));

        Assert.Equal(StockMovementOutcome.Unknown, (await client.IssueAsync(Guid.NewGuid(), 1, default)).Outcome);
    }

    [Fact]
    public async Task Issue_SuccessWithoutMovementId_ReturnsUnknown()
    {
        var (client, _) = Create(_ => Json(HttpStatusCode.Created, "not json"));

        Assert.Equal(StockMovementOutcome.Unknown, (await client.IssueAsync(Guid.NewGuid(), 1, default)).Outcome);
    }

    [Fact]
    public async Task Issue_Timeout_ReturnsUnknown()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("Timeout", new TimeoutException()));

        Assert.Equal(StockMovementOutcome.Unknown, (await client.IssueAsync(Guid.NewGuid(), 1, default)).Outcome);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.NameResolutionError)]
    public async Task Issue_RequestNeverSent_ReturnsNotApplied(HttpRequestError error)
    {
        var (client, _) = Create(_ => throw new HttpRequestException(error, "Inventory unreachable"));

        Assert.Equal(StockMovementOutcome.NotApplied, (await client.IssueAsync(Guid.NewGuid(), 1, default)).Outcome);
    }

    [Fact]
    public async Task Issue_ConnectionLostAfterSending_ReturnsUnknown()
    {
        var (client, _) = Create(_ => throw new HttpRequestException(HttpRequestError.ResponseEnded, "Response ended"));

        Assert.Equal(StockMovementOutcome.Unknown, (await client.IssueAsync(Guid.NewGuid(), 1, default)).Outcome);
    }

    private sealed record Tenant(Guid TenantId) : ICurrentTenant;

    private sealed record RecordedRequest(string Path, string? Tenant, string? Body);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues(InventoryHttpClient.TenantHeader, out var values) ? values.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return _respond(request);
        }
    }
}
