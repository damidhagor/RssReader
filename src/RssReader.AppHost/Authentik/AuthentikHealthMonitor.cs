using Microsoft.Extensions.Diagnostics.HealthChecks;
using RssReader.AppHost.Authentik.Resources;

namespace RssReader.AppHost.Authentik;

internal static class AuthentikHealthMonitor
{
    public static async Task Run(AuthentikResource authentik, ResourceNotificationService notifications, CancellationToken cancellationToken)
    {
        var serverName = authentik.Server.Name;
        var workerName = authentik.Worker.Name;

        notifications.TryGetCurrentState(serverName, out var serverEvent);
        notifications.TryGetCurrentState(workerName, out var workerEvent);

        ChildStatus? published = null;
        await Publish().ConfigureAwait(false);

        try
        {
            await foreach (var resourceEvent in notifications.WatchAsync(cancellationToken).ConfigureAwait(false))
            {
                if (resourceEvent.Resource.Name == serverName)
                {
                    serverEvent = resourceEvent;
                }
                else if (resourceEvent.Resource.Name == workerName)
                {
                    workerEvent = resourceEvent;
                }
                else
                {
                    continue;
                }

                await Publish().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { }

        Task Publish()
        {
            var status = Combine((serverName, Classify(serverEvent)), (workerName, Classify(workerEvent)));
            if (status == published)
            {
                return Task.CompletedTask;
            }

            published = status;
            return notifications.PublishUpdateAsync(
                authentik,
                snapshot => snapshot with { State = new(status.Text, status.Style) });
        }
    }

    private static ChildStatus Combine((string Name, ChildStatus Status) server, (string Name, ChildStatus Status) worker)
    {
        if (server.Status.Severity == worker.Status.Severity && server.Status.Text == worker.Status.Text)
        {
            return server.Status;
        }

        var (winnerName, winnerStatus) = server.Status.Severity >= worker.Status.Severity ? server : worker;
        return winnerStatus with { Text = $"{winnerName}: {winnerStatus.Text}" };
    }

    private static ChildStatus Classify(ResourceEvent? resourceEvent)
    {
        var state = resourceEvent?.Snapshot.State?.Text;
        var health = resourceEvent?.Snapshot.HealthStatus;

        return (state, health) switch
        {
            (not null, _) when KnownResourceStates.TerminalStates.Contains(state) || state == KnownResourceStates.RuntimeUnhealthy
                => new(4, state, KnownResourceStateStyles.Error),
            (_, HealthStatus.Unhealthy or HealthStatus.Degraded)
                => new(3, state is null ? health.ToString()! : $"{state} ({health})", KnownResourceStateStyles.Warn),
            _ when state == KnownResourceStates.Stopping
                => new(2, state, KnownResourceStateStyles.Info),
            _ when state == KnownResourceStates.Running
                => new(0, state, KnownResourceStateStyles.Success),
            _ => new(1, state ?? KnownResourceStates.Starting, KnownResourceStateStyles.Info)
        };
    }

    private readonly record struct ChildStatus(int Severity, string Text, string Style);
}
