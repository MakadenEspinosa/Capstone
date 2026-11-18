using System.Collections.Concurrent;
using System.Security.Claims;
using ESEvents.Application.Chat;
using ESEvents.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ESEvents.Web.Hubs;

[Authorize]
public sealed class ChatHub(
    IChatService chat,
    ICompanyService companies,
    ILogger<ChatHub> logger) : Hub
{
    private const string CompanyIdsKey = "chat-company-ids";
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>> CompanyConnections = new();

    public static string UserGroup(Guid userId) => $"chat-user:{userId:D}";
    public static string CompanyGroup(Guid companyId) => $"chat-company:{companyId:D}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

        var companyIds = await companies.GetCompanyIdsForUserAsync(userId, Context.ConnectionAborted);
        Context.Items[CompanyIdsKey] = companyIds;
        foreach (var companyId in companyIds)
        {
            var users = CompanyConnections.GetOrAdd(
                companyId, _ => new ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>>());
            var connections = users.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            var isFirstConnection = connections.IsEmpty;
            connections.TryAdd(Context.ConnectionId, 0);
            await Groups.AddToGroupAsync(Context.ConnectionId, CompanyGroup(companyId));
            if (isFirstConnection)
                await Clients.Group(CompanyGroup(companyId)).SendAsync("UserOnline", userId, Context.ConnectionAborted);
        }

        logger.LogInformation("Chat connection {ConnectionId} opened for user {UserId}", Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetUserId(out var userId) && Context.Items[CompanyIdsKey] is IReadOnlyList<Guid> companyIds)
        {
            foreach (var companyId in companyIds)
            {
                if (!CompanyConnections.TryGetValue(companyId, out var users) ||
                    !users.TryGetValue(userId, out var connections))
                    continue;

                connections.TryRemove(Context.ConnectionId, out _);
                if (connections.IsEmpty)
                {
                    users.TryRemove(userId, out _);
                    await Clients.Group(CompanyGroup(companyId)).SendAsync("UserOffline", userId);
                }
                if (users.IsEmpty) CompanyConnections.TryRemove(companyId, out _);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid companyId, Guid receiverId, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            return await chat.SendMessageAsync(companyId, GetUserId(), receiverId, content, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("Chat is limited to active members of the same company.");
        }
        catch (ArgumentException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    public async Task MarkAsReadAsync(
        Guid companyId, Guid senderId, CancellationToken cancellationToken = default)
    {
        try
        {
            await chat.MarkAsReadAsync(companyId, GetUserId(), senderId, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("You are not authorized to read this conversation.");
        }
    }

    public async Task TypingAsync(
        Guid companyId, Guid recipientId, CancellationToken cancellationToken = default)
    {
        var senderId = GetUserId();
        if (!await companies.IsMemberAsync(companyId, senderId, cancellationToken) ||
            !await companies.IsMemberAsync(companyId, recipientId, cancellationToken))
            throw new HubException("Chat is limited to active members of the same company.");

        await Clients.Group(UserGroup(recipientId))
            .SendAsync("UserTyping", senderId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetOnlineUsersAsync(
        Guid companyId, CancellationToken cancellationToken = default)
    {
        if (!await companies.IsMemberAsync(companyId, GetUserId(), cancellationToken))
            throw new HubException("You are not a member of this company.");

        return CompanyConnections.TryGetValue(companyId, out var users)
            ? users.Keys.ToArray()
            : Array.Empty<Guid>();
    }

    public async Task<bool> IsUserOnlineAsync(
        Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await companies.IsMemberAsync(companyId, GetUserId(), cancellationToken) ||
            !await companies.IsMemberAsync(companyId, userId, cancellationToken))
            throw new HubException("You are not authorized to view this user's presence.");

        return CompanyConnections.TryGetValue(companyId, out var users) && users.ContainsKey(userId);
    }

    public async Task<Dictionary<Guid, bool>> GetUsersOnlineStatusAsync(
        Guid companyId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (!await companies.IsMemberAsync(companyId, GetUserId(), cancellationToken))
            throw new HubException("You are not a member of this company.");

        var members = await companies.GetMembersAsync(companyId, GetUserId(), cancellationToken);
        var validIds = members.Select(member => member.UserId).Append(GetUserId()).ToHashSet();
        if (userIds.Any(userId => !validIds.Contains(userId)))
            throw new HubException("Presence can only be checked for members of your company.");

        return userIds.Distinct().ToDictionary(
            userId => userId,
            userId => CompanyConnections.TryGetValue(companyId, out var users) && users.ContainsKey(userId));
    }

    private Guid GetUserId() =>
        TryGetUserId(out var userId) ? userId : throw new HubException("Authenticated user ID is missing.");

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
