using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Order.Application.Exceptions;

namespace Order.Infrastructure.Http;

internal static class ServiceHttpReader
{
    public static async Task<T?> GetAsync<T>(HttpClient client, string path, string service,
        Func<T, bool> isValid, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await client.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw new UpstreamServiceException(service);
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            if (value is null || !isValid(value)) throw new UpstreamServiceException(service);
            return value;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamServiceException(service, timeout: true, innerException: exception);
        }
        catch (HttpRequestException exception) { throw new UpstreamServiceException(service, innerException: exception); }
        catch (JsonException exception) { throw new UpstreamServiceException(service, innerException: exception); }
        catch (NotSupportedException exception) { throw new UpstreamServiceException(service, innerException: exception); }
    }
}
