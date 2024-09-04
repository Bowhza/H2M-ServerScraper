﻿using MatchmakingServer.SignalR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchmakingServer
{
    [ApiController]
    [Route("[controller]")]
    public class QueuesController : ControllerBase
    {
        private readonly QueueingService _queueingService;

        public QueuesController(QueueingService queueingService)
        {
            _queueingService = queueingService;
        }


        [HttpGet]
        public IActionResult GetAllQueues([FromQuery] QueueProcessingState? state = null)
        {
            IEnumerable<GameServer> filteredQueues = state is null
                ? _queueingService.QueuedServers
                : _queueingService.QueuedServers.Where(s => s.ProcessingState == state);

            return Ok(filteredQueues.Select(s =>
            {
                return new
                {
                    s.InstanceId,
                    s.ServerIp,
                    s.ServerPort,
                    Players = s.PlayerQueue.Select(p =>
                    {
                        return new
                        {
                            p.ConnectionId,
                            p.Name,
                            p.State,
                            JoinAttempts = p.JoinAttempts.Count,
                            QueueTime = DateTimeOffset.Now - p.QueuedAt,
                        };
                    }),
                    s.LastServerInfo?.HostName,
                    s.LastServerInfo?.Ping,
                    s.LastServerInfo?.RealPlayerCount,
                    s.LastServerInfo?.MaxClients,
                    s.SpawnDate,
                    ProcessingState = s.ProcessingState.ToString(),
                };
            }));
        }
    }
}
