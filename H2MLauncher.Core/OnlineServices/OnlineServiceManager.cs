using H2MLauncher.Core.Matchmaking.Models;
using H2MLauncher.Core.OnlineServices.Authentication;
using H2MLauncher.Core.Services;
using H2MLauncher.Core.Settings;
using H2MLauncher.Core.Utilities.SignalR;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace H2MLauncher.Core.OnlineServices;

/// <summary>
/// Manages connection to online services (such as party or matchmaking) with SignalR.
/// </summary>
public sealed class OnlineServiceManager : IOnlineServices, IAsyncDisposable
{
    private readonly ILogger<OnlineServiceManager> _logger;
    private readonly IOptions<MatchmakingSettings> _options;
    private readonly AuthenticationService _authenticationService;

    private readonly List<CustomHubConnection> _hubConnections = [];

    public ClientContext ClientContext { get; }

    public HubConnection QueueingHubConnection { get; }
    public HubConnection PartyHubConnection { get; }

    public bool IsPartyServiceConnected => PartyHubConnection.State is HubConnectionState.Connected;
    public bool IsQueueingServiceConnected => QueueingHubConnection.State is HubConnectionState.Connected;


    private PlayerState _state = PlayerState.Disconnected;
    public PlayerState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _logger.LogTrace("Set client state to {state}", value);

            PlayerState oldState = _state;
            _state = value;

            OnStateChanged(oldState, value);
        }
    }

    public event Action<PlayerState, PlayerState>? StateChanged;

    public OnlineServiceManager(
        ILogger<OnlineServiceManager> logger,
        IOptions<MatchmakingSettings> matchmakingSettings,
        AuthenticationService authenticationService,
        ClientContext clientContext)
    {
        _logger = logger;
        _options = matchmakingSettings;
        _authenticationService = authenticationService;
        ClientContext = clientContext;

        object queryParams = new
        {
            uid = ClientContext.ClientId,
            playerName = ClientContext.PlayerName
        };

        QueueingHubConnection = new CustomHubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.QueueingHubUrl, (opts) =>
            {
                opts.AccessTokenProvider = GetAccessTokenAsync;

                // add headers to identify app version
                AddAppHeaders(opts.Headers);
            })
            .Build();

        PartyHubConnection = new CustomHubConnectionBuilder()
            .WithUrl(matchmakingSettings.Value.PartyHubUrl, (opts) =>
            {
                opts.AccessTokenProvider = GetAccessTokenAsync;

                AddAppHeaders(opts.Headers);
            })
            .Build();

        _hubConnections = [(CustomHubConnection)QueueingHubConnection, (CustomHubConnection)PartyHubConnection];
        _hubConnections.ForEach(conn => conn.Closed += HubConnection_Closed);
        _hubConnections.ForEach(conn => conn.Connected += HubConnection_Connected);
    }

    private static void AddAppHeaders(IDictionary<string, string> headers)
    {
        // add headers to identify app version
        headers.Add("X-App-Name", "H2MLauncher");
        headers.Add("X-App-Version", LauncherService.CurrentVersion);
    }

    private Task<string?> GetAccessTokenAsync()
    {
        if (ClientContext.IsAuthenticated)
        {
            // stored token still valid
            return Task.FromResult<string?>(ClientContext.AccessToken);
        }

        return _authenticationService.LoginAsync();
    }

    private void OnStateChanged(PlayerState oldState, PlayerState newState)
    {
        StateChanged?.Invoke(oldState, newState);
    }

    private Task HubConnection_Connected(string? connectionId)
    {
        _logger.LogDebug("Hub connection connect with connection id {connectionId}", connectionId);

        if (State is PlayerState.Disconnected)
        {
            State = PlayerState.Connected;
        }

        return Task.CompletedTask;
    }

    private Task HubConnection_Closed(Exception? exception)
    {
        _logger.LogDebug(exception, "Hub connection was closed");

        if (_hubConnections.TrueForAll(conn => conn.State is HubConnectionState.Disconnected))
        {
            State = PlayerState.Disconnected;
        }

        return Task.CompletedTask;
    }

    public async Task StartAllConnections(CancellationToken cancellationToken = default)
    {
        foreach (HubConnection connection in _hubConnections)
        {
            if (connection.State is not HubConnectionState.Disconnected)
            {
                return;
            }

            await connection.StartAsync(cancellationToken);
        }
    }

    public async Task CloseAllConnections(CancellationToken cancellationToken = default)
    {
        foreach (HubConnection connection in _hubConnections)
        {
            if (connection.State is HubConnectionState.Disconnected)
            {
                return;
            }

            await connection.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (HubConnection connection in _hubConnections)
        {
            await connection.DisposeAsync();
        }
    }

    class PartyHubConnectionRetryPolicy : IRetryPolicy
    {
        internal static TimeSpan?[] DEFAULT_RETRY_DELAYS =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
        ];

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return DEFAULT_RETRY_DELAYS[Math.Min(retryContext.PreviousRetryCount, DEFAULT_RETRY_DELAYS.Length)];
        }
    }
}
