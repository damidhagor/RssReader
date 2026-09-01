namespace RssReader.AppHost.Authentik.Resources;

internal sealed class AuthentikWorkerResource(string name) : ContainerResource(name)
{
    public EndpointReference HttpEndpoint => field ??= new(this, "http");

    public EndpointReferenceExpression Host => HttpEndpoint.Property(EndpointProperty.Host);

    public EndpointReferenceExpression Port => HttpEndpoint.Property(EndpointProperty.Port);
}
