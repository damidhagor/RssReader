namespace RssReader.AppHost.Authentik.Resources;

internal sealed class AuthentikServerResource(string name) : ContainerResource(name)
{
    public EndpointReference HttpsEndpoint => field ??= new(this, "https");

    public EndpointReferenceExpression Host => HttpsEndpoint.Property(EndpointProperty.Host);

    public EndpointReferenceExpression Port => HttpsEndpoint.Property(EndpointProperty.Port);

    public ReferenceExpression AuthorityExpression => ReferenceExpression.Create(
        $"{new EndpointReference(this, "https", KnownNetworkIdentifiers.LocalhostNetwork).Property(EndpointProperty.Url)}");
}
