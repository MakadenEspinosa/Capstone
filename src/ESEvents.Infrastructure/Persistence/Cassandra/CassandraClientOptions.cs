using System;
using System.Collections.Generic;

namespace ESEvents.Infrastructure.Persistence.Cassandra;

/// <summary>
/// Configuration options for Cassandra connection and clustering.
/// Binds from appsettings.json "Cassandra" section.
/// </summary>
public class CassandraClientOptions
{
    public string[] ContactPoints { get; set; } = Array.Empty<string>();
    
    public int Port { get; set; } = 9042;
    
    public string Keyspace { get; set; } = "chat_logs";
    
    public int ReplicationFactor { get; set; } = 3;
    
    public string ConsistencyLevel { get; set; } = "LOCAL_QUORUM";
    
    public int PageSize { get; set; } = 100;
    
    public SocketOptionsConfig? SocketOptions { get; set; }
}

/// <summary>
/// Socket configuration for connection timeouts and performance tuning.
/// </summary>
public class SocketOptionsConfig
{
    public int ConnectTimeoutMillis { get; set; } = 5000;
    
    public int ReadTimeoutMillis { get; set; } = 12000;
}
