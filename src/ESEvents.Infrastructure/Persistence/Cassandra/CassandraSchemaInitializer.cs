using Cassandra;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ESEvents.Infrastructure.Persistence.Cassandra;

/// <summary>
/// Initializes Cassandra keyspace and tables for chat functionality.
/// Should be run once during application setup.
/// </summary>
public class CassandraSchemaInitializer
{
    private readonly CassandraSessionFactory _sessionFactory;
    private readonly ILogger<CassandraSchemaInitializer> _logger;

    public CassandraSchemaInitializer(CassandraSessionFactory sessionFactory, ILogger<CassandraSchemaInitializer> logger)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the chat_logs keyspace and all required tables.
    /// Idempotent - safe to call multiple times.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var session = await _sessionFactory.GetSessionAsync();

            _logger.LogInformation("Initializing Cassandra schema for chat logs");

            // Create keyspace if it doesn't exist
            var createKeyspaceStatement = @"
                CREATE KEYSPACE IF NOT EXISTS chat_logs
                WITH replication = {
                    'class': 'SimpleStrategy',
                    'replication_factor': 3
                };";

            await session.ExecuteAsync(new SimpleStatement(createKeyspaceStatement));
            _logger.LogInformation("Keyspace 'chat_logs' created/verified");

            // Create messages_by_conversation table
            var messagesConversationStatement = @"
                CREATE TABLE IF NOT EXISTS chat_logs.messages_by_conversation (
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
                  AND default_time_to_live = 2592000;";

            await session.ExecuteAsync(new SimpleStatement(messagesConversationStatement));
            _logger.LogInformation("Table 'messages_by_conversation' created/verified");

            // Create messages_by_sender table for sent message history
            var messagesSenderStatement = @"
                CREATE TABLE IF NOT EXISTS chat_logs.messages_by_sender (
                    company_id UUID,
                    sender_id UUID,
                    message_timestamp BIGINT,
                    message_id UUID,
                    receiver_id UUID,
                    content TEXT,
                    created_at TIMESTAMP,
                    PRIMARY KEY ((company_id, sender_id), message_timestamp, message_id)
                ) WITH CLUSTERING ORDER BY (message_timestamp DESC)
                  AND default_time_to_live = 2592000;";

            await session.ExecuteAsync(new SimpleStatement(messagesSenderStatement));
            _logger.LogInformation("Table 'messages_by_sender' created/verified");

            // Create unread_messages table (index for active conversations)
            var unreadMessagesStatement = @"
                CREATE TABLE IF NOT EXISTS chat_logs.unread_messages (
                    company_id UUID,
                    receiver_id UUID,
                    sender_id UUID,
                    message_timestamp BIGINT,
                    message_id UUID,
                    content TEXT,
                    created_at TIMESTAMP,
                    PRIMARY KEY ((company_id, receiver_id), message_timestamp, sender_id, message_id)
                ) WITH CLUSTERING ORDER BY (message_timestamp DESC)
                  AND default_time_to_live = 2592000;";

            await session.ExecuteAsync(new SimpleStatement(unreadMessagesStatement));
            _logger.LogInformation("Table 'unread_messages' created/verified");

            _logger.LogInformation("Cassandra schema initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cassandra schema");
            throw;
        }
    }
}
