using ESEvents.Infrastructure.Persistence.Cassandra;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ESEvents.Infrastructure.Persistence;

/// <summary>
/// Initializes Cassandra cluster and schema on application startup.
/// Ensures all tables and keyspaces exist before the application begins processing messages.
/// </summary>
public class CassandraStartupService : IHostedService
{
    private readonly CassandraSessionFactory _sessionFactory;
    private readonly CassandraSchemaInitializer _schemaInitializer;
    private readonly ILogger<CassandraStartupService> _logger;

    public CassandraStartupService(
        CassandraSessionFactory sessionFactory,
        CassandraSchemaInitializer schemaInitializer,
        ILogger<CassandraStartupService> logger)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _schemaInitializer = schemaInitializer ?? throw new ArgumentNullException(nameof(schemaInitializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting Cassandra initialization service");

            // Initialize the cluster connection
            await _sessionFactory.InitializeAsync();
            _logger.LogInformation("Cassandra cluster connection established");

            // Initialize the schema (keyspace and tables)
            await _schemaInitializer.InitializeAsync();
            _logger.LogInformation("Cassandra schema initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cassandra on startup");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Stopping Cassandra connection");
            await _sessionFactory.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Cassandra shutdown");
        }
    }
}
