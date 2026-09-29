using System.Net.Http.Json;
using System.Text.Json;
using Order.Application.Abstractions;
using Order.Application.Exceptions;

namespace Order.Infrastructure.Http;

public sealed class StockClient(HttpClient client) : IStockClient
{
    public async Task<StockResult> ApplyAsync(Guid operationId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/stock-operations/{operationId}")
            { Content = JsonContent.Create(new { items }) };
        var result = await SendAsync(request, operationId, cancellationToken);
        if (result.Status == "Applied" && (result.Allocations.Count != items.Count
            || result.Allocations.Select(x => x.ProductId).Distinct().Count() != items.Count
            || result.Allocations.Any(allocation => !items.Any(item => item.ProductId == allocation.ProductId
                && item.Quantity == allocation.RequestedQuantity && allocation.AcceptedQuantity >= 0
                && allocation.AcceptedQuantity <= item.Quantity)))) throw new UpstreamServiceException("Product");
        return result;
    }

    public async Task<StockResult> CancelAsync(Guid operationId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/stock-operations/{operationId}/cancel");
        var result = await SendAsync(request, operationId, cancellationToken);
        if (result.Status != "Cancelled") throw new UpstreamServiceException("Product");
        return result;
    }

    private async Task<StockResult> SendAsync(HttpRequestMessage request, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new UpstreamServiceException("Product");
            var result = await response.Content.ReadFromJsonAsync<StockResult>(cancellationToken);
            if (result is null || result.Id != id || result.Status is not ("Applied" or "Rejected" or "Cancelled")
                || result.Allocations is null || result.Allocations.Any(x => x is null)) throw new UpstreamServiceException("Product");
            return result;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        { throw new UpstreamServiceException("Product", true, exception); }
        catch (HttpRequestException exception) { throw new UpstreamServiceException("Product", innerException: exception); }
        catch (JsonException exception) { throw new UpstreamServiceException("Product", innerException: exception); }
        catch (NotSupportedException exception) { throw new UpstreamServiceException("Product", innerException: exception); }
    }
}
