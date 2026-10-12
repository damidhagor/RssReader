namespace RssReader.AppHost.Authentik.Resources;

internal sealed class AuthentikResource(
    string name,
    AuthentikServerResource server,
    AuthentikWorkerResource worker,
    ParameterResource secretKeyParameter,
    ParameterResource adminPasswordParameter,
    ParameterResource adminTokenParameter)
    : Resource(name)
{
    public AuthentikServerResource Server { get; } = server;

    public AuthentikWorkerResource Worker { get; } = worker;

    public ParameterResource SecretKeyParameter { get; } = secretKeyParameter;

    public ParameterResource AdminPasswordParameter { get; } = adminPasswordParameter;

    public ParameterResource AdminTokenParameter { get; } = adminTokenParameter;

    public EndpointReference HttpsEndpoint => Server.HttpsEndpoint;

    public EndpointReferenceExpression Host => Server.Host;

    public EndpointReferenceExpression Port => Server.Port;

    public ReferenceExpression AuthorityExpression => Server.AuthorityExpression;
}
