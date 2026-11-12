# Cassandra Chat Implementation Guide

## Overview
Implementing Apache Cassandra for chat message logs with WebSocket real-time messaging in your ASP.NET Core application.

## Architecture Design

### Why Cassandra for Chat Logs?
- **Write-optimized**: Perfect for high-throughput message ingestion
- **Time-series data**: Natural partition key design for chat logs
- **Distributed & Scalable**: Horizontal scaling without resharding
- **Query patterns**: Retrieve message history by sender/receiver/date efficiently
- **TTL (Time-to-live)**: Automatic message expiration for data retention policies

### Data Model

```cql
-- Keyspace for chat logs
CREATE KEYSPACE chat_logs WITH replication = {'class': 'SimpleStrategy', 'replication_factor': 3};

USE chat_logs;

-- Primary table: messages by conversation
CREATE TABLE messages_by_conversation (
    company_id UUID,
    user_id_1 UUID,
    user_id_2 UUID,
    message_timestamp BIGINT,
    message_id UUID,
    sender_id UUID,
    content TEXT,
    created_at TIMESTAMP,
    PRIMARY KEY ((company_id, user_id_1, user_id_2), message_timestamp, message_id)
) WITH CLUSTERING ORDER BY (message_timestamp DESC)
  AND default_time_to_live = 7776000;  -- 90 days

-- Index table: messages by sender (for sent message history)
CREATE TABLE messages_by_sender (
    company_id UUID,
    sender_id UUID,
    message_timestamp BIGINT,
    message_id UUID,
    receiver_id UUID,
    content TEXT,
    created_at TIMESTAMP,
    PRIMARY KEY ((company_id, sender_id), message_timestamp, message_id)
) WITH CLUSTERING ORDER BY (message_timestamp DESC);

-- Index table: unread messages (transient)
CREATE TABLE unread_messages (
    company_id UUID,
    receiver_id UUID,
    sender_id UUID,
    message_timestamp BIGINT,
    message_id UUID,
    content TEXT,
    created_at TIMESTAMP,
    PRIMARY KEY ((company_id, receiver_id), message_timestamp, sender_id, message_id)
) WITH CLUSTERING ORDER BY (message_timestamp DESC)
  AND default_time_to_live = 2592000;  -- 30 days for unread
```

### Partition Strategy
- **Partition Key**: `(company_id, normalized_user_ids)` - Ensures conversations stay on same partition
- **Clustering Key**: `message_timestamp DESC` - Efficient time-range queries
- **Why this works**: Users in same company, conversations isolated by participant pairs

### TTL Strategy
- **Active messages**: 90 days default (configurable per company)
- **Unread index**: 30 days (cleaned up after read)
- **Sent/received history**: Adjustable retention policy

## Implementation Steps

### Phase 1: Infrastructure Setup
1. Add Cassandra to docker-compose.yml
2. Create Cassandra connection factory
3. Implement connection pooling and health checks
4. Create session management

### Phase 2: Domain & Application Layer
1. Create ChatMessage domain model
2. Create IChatRepository interface
3. Implement Cassandra-specific repository
4. Create ChatService with business logic
5. Add DTOs for API contracts

### Phase 3: WebSocket Integration
1. Create WebSocket hub for real-time messaging
2. Implement connection management (user tracking)
3. Create message broadcasting logic
4. Add reconnection handling

### Phase 4: API & Web Layer
1. Create chat controller/endpoints
2. Add WebSocket middleware
3. Create SignalR hub (alternative to raw WebSocket)
4. Build message UI components

### Phase 5: Testing & Optimization
1. Load test with message volume
2. Optimize query performance
3. Add caching layer (Redis) for active conversations
4. Monitor Cassandra metrics

## Directory Structure
```
src/ESEvents.Infrastructure/
├── Persistence/
│   ├── Cassandra/
│   │   ├── CassandraSessionFactory.cs
│   │   ├── CassandraHealthCheck.cs
│   │   └── CassandraClientOptions.cs
│   └── (existing PostgreSQL repos)

src/ESEvents.Application/
├── Chat/
│   ├── ChatService.cs
│   ├── IChatService.cs
│   ├── ChatDtos.cs
│   └── IChatRepository.cs

src/ESEvents.Domain/
├── Chat/
│   ├── ChatMessage.cs
│   └── Conversation.cs

src/ESEvents.Web/
├── Hubs/
│   └── ChatHub.cs
├── Controllers/
│   └── ChatController.cs
└── Components/
    └── Chat/ (Blazor components)
```

## NuGet Packages Required
```xml
<PackageReference Include="Cassandra.DataStax.Community" Version="3.20.2" />
<PackageReference Include="Cassandra.DataStax.Community.Linq" Version="3.20.2" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
<PackageReference Include="StackExchange.Redis" Version="2.8.0" /> <!-- Optional: caching -->
```

## Configuration (appsettings.json)
```json
{
  "Cassandra": {
    "ContactPoints": ["cassandra"],
    "Port": 9042,
    "Keyspace": "chat_logs",
    "ReplicationFactor": 3,
    "ConsistencyLevel": "LOCAL_QUORUM",
    "PageSize": 100,
    "SocketOptions": {
      "ConnectTimeoutMillis": 5000,
      "ReadTimeoutMillis": 12000
    }
  },
  "Chat": {
    "MessageRetentionDays": 90,
    "PageSize": 50,
    "MaxMessageLength": 5000
  }
}
```

## Performance Considerations

### Query Optimization
- Use partition keys correctly to avoid scatter-gather queries
- Batch inserts for bulk operations (batch size: 25-100)
- Use prepared statements for all queries
- Leverage token-aware routing

### Scaling Strategy
- Start with 3-node cluster (minimum for HA)
- Monitor disk usage (plan for 50% headroom)
- Add nodes when >70% capacity reached
- Adjust replication_factor for availability vs. consistency trade-off

### Caching Layer (Optional but Recommended)
- Cache last 50 messages per conversation in Redis
- TTL: 5 minutes or on-write invalidation
- Reduces Cassandra read load significantly

### Monitoring
- Track write throughput (msgs/sec)
- Monitor latency (p50, p95, p99)
- Watch disk space and compaction overhead
- Set up alerts for node health

## Migration from PostgreSQL (if moving existing messages)
1. Export messages from PostgreSQL
2. Transform to Cassandra format
3. Bulk load with sstableloader
4. Verify data integrity
5. Run dual-write phase before cutover

## Security Considerations
- Enable Cassandra authentication (username/password)
- Use TLS/SSL for client-server communication
- Network segmentation (Cassandra behind private network)
- Row-level filtering by company_id (multi-tenancy)
- Audit logging for compliance

## Next Steps
Proceed with Phase 1 implementation. Decide:
1. **SignalR vs Raw WebSocket?** (SignalR = easier, more features; Raw = lighter weight)
2. **Caching layer?** (Recommended for active chats)
3. **Read consistency?** (LOCAL_QUORUM = balance; ONE = faster but risky)
