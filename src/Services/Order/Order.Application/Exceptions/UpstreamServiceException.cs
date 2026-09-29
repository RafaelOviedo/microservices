namespace Order.Application.Exceptions;

public sealed class UpstreamServiceException(string service, bool timeout = false, Exception? innerException = null)
    : Exception(timeout ? $"The request to the {service} service timed out." : $"Could not obtain a valid response from the {service} service.", innerException)
{
    public bool IsTimeout { get; } = timeout;
}
