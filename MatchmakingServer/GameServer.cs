﻿using H2MLauncher.Core.Models;
using H2MLauncher.Core.Services;

namespace MatchmakingServer
{
    public class GameServer : IServerConnectionDetails
    {
        public required string ServerIp { get; init; }

        public required int ServerPort { get; init; }

        public ConcurrentLinkedQueue<Player> PlayerQueue { get; } = [];

        public IEnumerable<Player> JoiningPlayers => PlayerQueue.Where(p => p.State is PlayerState.Joining);

        public IEnumerable<Player> QueuedPlayers => PlayerQueue.Where(p => p.State is PlayerState.Queued);

        public int JoiningPlayerCount { get; set; }

        string IServerConnectionDetails.Ip => ServerIp;

        int IServerConnectionDetails.Port => ServerPort;

        public DateTimeOffset LastSuccessfulPingTimestamp { get; set; }
        public GameServerInfo? LastServerInfo { get; set; }

        public List<string> ActualPlayers { get; } = [];

        public string InstanceId { get; }

        public Task? ProcessingTask { get; set; }

        public CancellationTokenSource ProcessingCancellation { get; set; } = new();

        public GameServer(string instanceId)
        {
            InstanceId = instanceId;
        }

        public override string ToString()
        {
            return $"[{ServerIp}:{ServerPort}]";
        }
    }
}
