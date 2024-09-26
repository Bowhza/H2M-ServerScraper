﻿using System.Collections.Concurrent;

using H2MLauncher.Core;
using H2MLauncher.Core.Matchmaking.Models;

using MatchmakingServer.Queueing;

using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MatchmakingServer.SignalR
{
    [Authorize(AuthenticationSchemes = BearerTokenDefaults.AuthenticationScheme)]
    public class QueueingHub : Hub<IClient>, IMatchmakingHub
    {
        private readonly ILogger<QueueingHub> _logger;
        private readonly QueueingService _queueingService;
        private readonly MatchmakingService _matchmakingService;

        private readonly PlayerStore _playerStore;

        private readonly static ConcurrentDictionary<string, Player> ConnectedPlayers = new();

        public QueueingHub(
            ILogger<QueueingHub> logger,
            QueueingService queueingService,
            MatchmakingService matchmakingService,
            PlayerStore playerStore)
        {
            _logger = logger;
            _queueingService = queueingService;
            _matchmakingService = matchmakingService;
            _playerStore = playerStore;
        }

        /// <summary>
        /// Unregister a player.
        /// </summary>
        /// <param name="connectionId">Client connection id.</param>
        /// <returns>The previously connected player.</returns>
        private Player? RemovePlayer(string connectionId)
        {
            if (!ConnectedPlayers.TryRemove(connectionId, out var player))
            {
                return null;
            }

            // clean up
            _queueingService.LeaveQueue(player, disconnected: true);

            if (player.State is PlayerState.Matchmaking)
            {
                _matchmakingService.LeaveMatchmaking(player);
            }

            return player;
        }

        public Task JoinAck(bool successful)
        {
            if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                // not found
                return Task.CompletedTask;
            }

            if (successful)
            {
                _queueingService.OnPlayerJoinConfirmed(player);
            }
            else
            {
                _queueingService.OnPlayerJoinFailed(player);
            }

            return Task.CompletedTask;
        }

        public Task<bool> JoinQueue(string serverIp, int serverPort, string instanceId)
        {
            if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
            {
                return Task.FromResult(false);
            }

            _logger.LogTrace("JoinQueue({serverIp}:{serverPort}, {playerName}) triggered", serverIp, serverPort, player.Name);

            return _queueingService.JoinQueue(serverIp, serverPort, player, instanceId);
        }

        public Task LeaveQueue()
        {
            if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                // unknown player
                return Task.CompletedTask;
            }

            if (player.State is PlayerState.Queued or PlayerState.Joining)
            {
                _queueingService.LeaveQueue(player);
            }
            else if (player.State is PlayerState.Matchmaking)
            {
                _matchmakingService.LeaveMatchmaking(player);
            }

            return Task.CompletedTask;
        }

        public Task<bool> SearchMatch(MatchSearchCriteria searchPreferences, List<string> preferredServers)
        {
            if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out Player? player))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(_matchmakingService.EnterMatchmaking(player, searchPreferences, preferredServers));
        }

        public Task<bool> UpdateSearchSession(MatchSearchCriteria searchPreferences, List<ServerPing> serverPings)
        {
            if (!ConnectedPlayers.TryGetValue(Context.ConnectionId, out var player))
            {
                // unknown player
                return Task.FromResult(false);
            }

            return Task.FromResult(_matchmakingService.UpdateSearchPreferences(player, searchPreferences, serverPings));
        }

        public override async Task OnConnectedAsync()
        {
            string uniqueId = Context.UserIdentifier!;
            string playerName = Context.User!.Identity!.Name!;

            Player player = await _playerStore.GetOrAdd(uniqueId, Context.ConnectionId, playerName);

            if (player.QueueingHubId is not null)
            {
                // Reject the connection because the user is already connected
                Context.Abort();
                return;
            }

            player.QueueingHubId = Context.ConnectionId;
            ConnectedPlayers[Context.ConnectionId] = player;

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(exception, "Client disconnected: {connectionId}", Context.ConnectionId);

            Player? player = await _playerStore.TryRemove(Context.UserIdentifier!, Context.ConnectionId);
            if (player is not null)
            {
                player.QueueingHubId = null;
            }

            if (RemovePlayer(Context.ConnectionId) != null)
            {
                _logger.LogInformation("Removed player {player}", player);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
