using Cassandra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ESEvents.Infrastructure.Persistence.Cassandra;

/// <summary>
/// Factory for creating and managing Cassandra cluster and session connections.
/// Implements connection pooling, health checks, and graceful shutdown.
/// </summary>
public class CassandraSessionFactory : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CassandraSessionFactory> _logger;
    private ICluster? _cluster;
    private ISession? _session;
    private readonly object _lockObject = new();

    public CassandraSessionFactory(IConfiguration configuration, ILogger<CassandraSessionFactory> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or creates the Cassandra session with lazy initialization.
    /// </summary>
    public async Task<ISession> GetSessionAsync()
    {
        if (_session?.IsDisposed != false)
        {
            lock (_lockObject)
            {
                if (_session?.IsDisposed != false)
                {
                    _session = _cluster?.Connect() 
                        ?? throw new InvalidOperationException("Cluster not initialized");
                }
            }
        }

        return await Task.FromResult(_session);
    }

    /// <summary>
    /// Initializes the Cassandra cluster connection.
    /// Must be called during application startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_cluster is not null)
        {
            _logger.LogInformation("Cassandra cluster already initialized");
            return;
        }

        lock (_lockObject)
        {
            if (_cluster is not null)
                return;

            try
            {
                var options = _configuration.GetSection("Cassandra").Get<CassandraClientOptions>()
                    ?? throw new InvalidOperationException("Cassandra configuration not found");

                _logger.LogInformation(
                    "Initializing Cassandra cluster: {ContactPoints}, Keyspace: {Keyspace}",
                    string.Join(", ", options.ContactPoints),
                    options.Keyspace);

                var poolingOptions = new PoolingOptions()
                    .SetCoreConnectionsPerHost(HostDistance.Local, 4)
                    .SetMaxConnectionsPerHost(HostDistance.Local, 10)
                    .SetCoreConnectionsPerHost(HostDistance.Remote, 2)
                    .SetMaxConnectionsPerHost(HostDistance.Remote, 4)
                    .SetMaxRequestsPerConnection(32768);

                var builder = Cluster.Builder()
                    .AddContactPoints(options.ContactPoints)
                    .WithPort(options.Port)
                    .WithDefaultKeyspace(options.Keyspace)
                    .WithPoolingOptions(poolingOptions);

                // Configure consistency level using query options
                var queryOptions = new QueryOptions()
                    .SetConsistencyLevel(ConsistencyLevel.LocalQuorum);
                
                builder.WithQueryOptions(queryOptions);

                _cluster = builder.Build();

                _logger.LogInformation("Cassandra cluster initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Cassandra cluster");
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks if the Cassandra cluster is healthy and reachable.
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            if (_cluster is null || _session?.IsDisposed != false)
                return false;

            var statement = new SimpleStatement("SELECT now() FROM system.local LIMIT 1");
            var result = await _session.ExecuteAsync(statement);
            return result.Count() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cassandra health check failed");
            return false;
        }
    }

    /// <summary>
    /// Gracefully shuts down the Cassandra connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_session is not null && !_session.IsDisposed)
            {
                _session.Dispose();
                _logger.LogInformation("Cassandra session closed");
            }

            if (_cluster is not null)
            {
                _cluster.Dispose();
                _logger.LogInformation("Cassandra cluster shutdown complete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Cassandra shutdown");
        }

        await Task.CompletedTask;
        GC.SuppressFinalize(this);
    }
}
