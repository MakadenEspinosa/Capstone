# Quick Start: Cassandra Chat Implementation

## 🚀 Get Started in 5 Minutes

### 1. Start Docker Services
```powershell
cd C:\Classes\Capstone\Capstone
docker-compose up -d
```

### 2. Wait for Cassandra (40-60 seconds)
```powershell
docker-compose logs cassandra | Select-String "ready to accept"
```

### 3. Run Your Application
```powershell
dotnet run
# Watch for startup logs:
# ✓ "Cassandra cluster connection established"
# ✓ "Cassandra schema initialization completed"
```

### 4. Verify Health
```powershell
curl http://localhost:8080/health
```

## 📊 Your Database Model

```
┌─────────────────────────────────┐
│   messages_by_conversation      │
├─────────────────────────────────┤
│ PK: (company_id, user_pair)     │
│ CK: timestamp DESC              │
│ Use: Fetch 1-1 chat history     │
└─────────────────────────────────┘
         ↑
         │ Denormalized copies ↓
         │
┌─────────────────────────────────┐
│   messages_by_sender            │
├─────────────────────────────────┤
│ PK: (company_id, sender_id)     │
│ CK: timestamp DESC              │
│ Use: User's sent messages       │
└─────────────────────────────────┘
```

## 🛠️ Key Configuration

| Setting | Value | Purpose |
|---------|-------|---------|
| **Consistency** | LOCAL_QUORUM | Balanced: fast + safe |
| **Replication** | Factor 3 | HA across 3 nodes |
| **TTL** | 30 days | Auto-cleanup |
| **Page Size** | 50 msgs | Efficient pagination |
| **Retention** | 30 days | Configurable policy |

## 📝 Example: Send a Message (Future Implementation)

```csharp
// Inject IChatService
public class ChatController : ControllerBase {
    private readonly IChatService _chatService;
    
    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(SendMessageDto dto)
    {
        var message = await _chatService.SendMessageAsync(
            senderId: User.GetUserId(),
            receiverId: dto.ReceiverId,
            companyId: User.GetCompanyId(),
            content: dto.Content
        );
        
        // WebSocket broadcast via SignalR
        await _chatHub.Clients.User(dto.ReceiverId.ToString())
            .SendAsync("ReceiveMessage", message);
        
        return Ok(message);
    }
}
```

## 🔍 Query Cassandra Directly

```bash
# Connect to CLI
docker-compose exec cassandra cqlsh

# View keyspace
cqlsh> DESCRIBE KEYSPACE chat_logs;

# View table
cqlsh> SELECT * FROM chat_logs.messages_by_conversation 
       WHERE company_id = ? AND user_id_1 = ? AND user_id_2 = ?;

# Check table size
cqlsh> SELECT COUNT(*) FROM messages_by_conversation;
```

## 🚨 Troubleshooting

| Problem | Solution |
|---------|----------|
| **Cassandra won't start** | Check disk space: `docker system df` |
| **App connects to DB but not Cassandra** | Wait longer for health check (60s) |
| **"Connection refused"** | Port 9042 occupied? Run `netstat -ano \| findstr 9042` |
| **Schema already exists** | It's idempotent - safe to restart |

## 📚 Next: Phase 2 (Domain Layer)

1. Create `ChatMessage` entity
2. Create `IChatRepository` interface
3. Implement repository with Cassandra queries
4. Create `ChatService` with business logic
5. Build DTOs and API endpoints

**Estimated Time**: 8-10 hours total for Phases 2-4

## 🌐 AWS Production Deployment

Your app will run on:
- **WebSocket**: SignalR (recommended for sticky sessions)
- **Compute**: AWS App Runner
- **Database**: Amazon Managed Cassandra Service
- **Caching**: ElastiCache Redis (optional)

No architecture changes needed - just deploy containers!

## 📊 Performance Targets

- **Write**: 15,000+ msgs/sec (3-node cluster)
- **Read**: p99 latency <50ms
- **Storage**: ~25GB per 1M msgs/day

## 💾 Configuration Files

All config is in: `appsettings.json`
- `Cassandra.*` - Connection settings
- `Chat.*` - Application settings

Change `MessageRetentionDays` to adjust TTL.

---

**Build Status**: ✅ Successful
**Next Command**: `docker-compose up -d`
