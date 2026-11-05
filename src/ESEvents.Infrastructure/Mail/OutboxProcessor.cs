using ESEvents.Application.Abstractions;
using ESEvents.Domain.Notifications;
using ESEvents.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ESEvents.Infrastructure.Mail;

internal sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
            logger.LogWarning("Email:Host is not set; queued notifications will wait until email is configured.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                if (options.Value.IsConfigured)
                    processed = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Processing queued notifications failed; will retry.");
            }

            if (processed < BatchSize)
                await Task.Delay(PollInterval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // SKIP LOCKED lets several API instances share the queue without sending a message twice.
        var now = DateTime.UtcNow;
        var batch = await db.Set<OutboxMessage>()
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE "SentAtUtc" IS NULL AND "FailedAtUtc" IS NULL AND "NextAttemptAtUtc" <= {now}
                ORDER BY "NextAttemptAtUtc"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            try
            {
                await sender.SendAsync(message.RecipientEmail, message.Subject, message.Body, ct);
                message.MarkSent(DateTime.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception.Message, DateTime.UtcNow);
                logger.LogWarning(exception, "Sending notification {MessageId} failed (attempt {Attempt}).", message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return batch.Count;
    }
}
