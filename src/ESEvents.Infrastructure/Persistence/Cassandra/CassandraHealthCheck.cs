using Cassandra;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ESEvents.Infrastructure.Persistence.Cassandra;

/// <summary>
/// Health check for Cassandra cluster connectivity.
/// Integrates with ASP.NET Core health check system.
/// </summary>
public class CassandraHealthCheck : IHealthCheck
{
    private readonly CassandraSessionFactory _sessionFactory;
    private readonly ILogger<CassandraHealthCheck> _logger;

    public CassandraHealthCheck(CassandraSessionFactory sessionFactory, ILogger<CassandraHealthCheck> logger)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _sessionFactory.IsHealthyAsync();

            if (isHealthy)
            {
                _logger.LogDebug("Cassandra health check passed");
                return HealthCheckResult.Healthy("Cassandra cluster is reachable");
            }

            _logger.LogWarning("Cassandra health check failed - cluster unreachable");
            return HealthCheckResult.Unhealthy("Cassandra cluster is not reachable");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cassandra health check exception");
            return HealthCheckResult.Unhealthy("Cassandra health check failed", ex);
        }
    }
}
