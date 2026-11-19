using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StockSync.Orders.Application.Common;
using StockSync.Orders.Application.Common.Exceptions;
using StockSync.Orders.Application.Inventory;

namespace StockSync.Orders.Infrastructure.Inventory;

// HTTP adapter for the Inventory Service API. It calls Inventory directly on the internal
// network (not through the gateway) and forwards the current tenant in X-Tenant-Id.
public sealed class InventoryHttpClient : IInventoryClient
{
    public const string TenantHeader = "X-Tenant-Id";
    private const int StockPageSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ICurrentTenant _currentTenant;

    public InventoryHttpClient(HttpClient httpClient, ICurrentTenant currentTenant)
    {
        _httpClient = httpClient;
        _currentTenant = currentTenant;
    }

    public async Task<InventoryProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        using var response = await SendQueryAsync($"api/products/{productId}", cancellationToken);
        // Inventory answers 404 for missing and inactive products.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        var product = await ReadQueryAsync<ProductDto>(response, cancellationToken);
        return new InventoryProduct(product.Id, product.Name, product.Sku, product.PurchasePrice, product.SalePrice);
    }

    // Inventory has no lookup by product and branch, so scan the product's assignments.
    public async Task<Guid?> FindStockIdAsync(Guid productId, Guid branchId, CancellationToken cancellationToken)
    {
        for (var page = 1; ; page++)
        {
            using var response = await SendQueryAsync(
                $"api/stock/product/{productId}?page={page}&pageSize={StockPageSize}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            var result = await ReadQueryAsync<StockPageDto>(response, cancellationToken);
            var stock = result.Items.FirstOrDefault(s => s.BranchId == branchId);
            if (stock is not null)
                return stock.Id;
            if (page >= result.TotalPages)
                return null;
        }
    }

    public Task<StockMovementResult> IssueAsync(Guid stockId, int quantity, CancellationToken cancellationToken) =>
        RecordMovementAsync($"api/stock/{stockId}/issues", quantity, cancellationToken);

    public Task<StockMovementResult> ReceiveAsync(Guid stockId, int quantity, CancellationToken cancellationToken) =>
        RecordMovementAsync($"api/stock/{stockId}/receipts", quantity, cancellationToken);

    // Classify the answer so the saga knows whether stock changed. Requests are never retried here:
    // Inventory movements are not idempotent and a retry could move stock twice.
    private async Task<StockMovementResult> RecordMovementAsync(string path, int quantity, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(new MovementRequestDto(quantity), options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError is HttpRequestError.NameResolutionError
                                                  or HttpRequestError.ConnectionError)
        {
            // The host was not found or the connection was never established, so the request was not sent.
            return StockMovementResult.NotApplied($"Inventory is unavailable ({ex.Message}).");
        }
        catch (HttpRequestException ex)
        {
            return StockMovementResult.Unknown($"Communication with Inventory failed ({ex.Message}).");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StockMovementResult.Unknown("Inventory did not respond in time.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var movement = await TryReadAsync<MovementDto>(response);
                return movement is not null && movement.Id != Guid.Empty
                    ? StockMovementResult.Applied(movement.Id)
                    : StockMovementResult.Unknown("Inventory accepted the movement without returning its identifier.");
            }

            var problem = await ReadProblemAsync(response);
            // A server error may happen after the movement was committed.
            return (int)response.StatusCode >= 500
                ? StockMovementResult.Unknown($"Inventory returned {(int)response.StatusCode}: {problem}")
                : StockMovementResult.Rejected(problem);
        }
    }

    private async Task<HttpResponseMessage> SendQueryAsync(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InventoryUnavailableException("The Inventory service is unavailable.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InventoryUnavailableException("The Inventory service did not respond in time.", ex);
        }

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            return response;

        var problem = await ReadProblemAsync(response);
        response.Dispose();
        throw new InventoryUnavailableException($"The Inventory service returned {(int)response.StatusCode}: {problem}");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TenantHeader, _currentTenant.TenantId.ToString());
        return request;
    }

    private static async Task<T> ReadQueryAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw new JsonException("Empty response.");
        }
        catch (JsonException ex)
        {
            throw new InventoryUnavailableException("The Inventory service returned an unreadable response.", ex);
        }
    }

    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response) where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Inventory errors are ProblemDetails; prefer the detail, which explains the business rule.
    private static async Task<string> ReadProblemAsync(HttpResponseMessage response)
    {
        var problem = await TryReadAsync<ProblemDto>(response);
        return problem?.Detail ?? problem?.Title ?? $"HTTP {(int)response.StatusCode}";
    }

    private sealed record MovementRequestDto(int Quantity);

    private sealed record MovementDto(Guid Id);

    private sealed record ProductDto(Guid Id, string Name, string Sku, decimal PurchasePrice, decimal SalePrice);

    private sealed record StockDto(Guid Id, Guid ProductId, Guid BranchId, int Quantity);

    private sealed record StockPageDto(IReadOnlyList<StockDto> Items, int TotalPages);

    private sealed record ProblemDto(string? Title, string? Detail);
}
