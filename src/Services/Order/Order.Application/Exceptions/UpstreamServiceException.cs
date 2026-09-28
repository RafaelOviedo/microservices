namespace Order.Application.Exceptions;

public sealed class UpstreamServiceException(string service, bool timeout = false, Exception? innerException = null)
    : Exception(timeout ? $"Se agotó el tiempo de espera del servicio {service}." : $"No se pudo obtener una respuesta válida del servicio {service}.", innerException)
{
    public bool IsTimeout { get; } = timeout;
}
