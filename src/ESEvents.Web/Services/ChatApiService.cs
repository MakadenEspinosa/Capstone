using ESEvents.Application.Chat;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace ESEvents.Web.Services;

/// <summary>
/// Implementation of HTTP REST API communication with the chat service
/// </summary>
public class ChatApiService : IChatApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChatApiService> _logger;
    private const string ApiBase = "/api/chat";

    public ChatApiService(HttpClient httpClient, ILogger<ChatApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ConversationDto>> GetConversationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBase}/conversations");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<List<ConversationDto>>(json);
            _logger.LogDebug($"Loaded {result?.Count ?? 0} conversations");
            return result ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversations");
            throw;
        }
    }

    public async Task<ConversationDto> GetConversationAsync(
        Guid otherUserId, int pageSize = 50, long? pageToken = null)
    {
        try
        {
            var query = $"?pageSize={pageSize}";
            if (pageToken.HasValue)
                query += $"&pageToken={pageToken}";

            var response = await _httpClient.GetAsync(
                $"{ApiBase}/conversations/{otherUserId}{query}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ConversationDto>(json);
            _logger.LogDebug($"Loaded conversation with {otherUserId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading conversation with {otherUserId}");
            throw;
        }
    }

    public async Task<GetConversationResponse> GetConversationAsync(
        Guid companyId, Guid userId1, Guid userId2, int pageSize = 50, long? pageToken = null)
    {
        try
        {
            var query = $"?companyId={companyId}&pageSize={pageSize}";
            if (pageToken.HasValue)
                query += $"&pageToken={pageToken}";

            var response = await _httpClient.GetAsync(
                $"{ApiBase}/conversations/{userId1}/{userId2}{query}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(json);
            var nextPageToken = ExtractPageToken(response);

            return new GetConversationResponse
            {
                Messages = messages ?? new(),
                NextPageToken = nextPageToken,
                TotalCount = messages?.Count ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading conversation {userId1}/{userId2}");
            throw;
        }
    }

    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{ApiBase}/messages", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SendMessageResponse>(json);
            _logger.LogDebug($"Message sent to {request.ReceiverId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending message to {request.ReceiverId}");
            throw;
        }
    }

    public async Task<MarkAsReadResponse> MarkAsReadAsync(
        Guid companyId, Guid userId1, Guid userId2)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{ApiBase}/conversations/{userId1}/{userId2}/mark-as-read?companyId={companyId}",
                null);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<MarkAsReadResponse>(json);
            _logger.LogDebug($"Marked {result?.MessagesMarked ?? 0} messages as read");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking messages as read");
            throw;
        }
    }

    public async Task<DeleteMessageResponse> DeleteMessageAsync(
        Guid messageId, Guid companyId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{ApiBase}/messages/{messageId}?companyId={companyId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<DeleteMessageResponse>(json);
            _logger.LogDebug($"Deleted message {messageId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting message {messageId}");
            throw;
        }
    }

    public async Task<GetUnreadCountResponse> GetUnreadCountAsync(
        Guid companyId, Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{ApiBase}/unread-count?companyId={companyId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GetUnreadCountResponse>(json);
            _logger.LogDebug($"Unread count: {result?.TotalUnread ?? 0}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count");
            throw;
        }
    }

    public async Task<GetUnreadSummaryResponse> GetUnreadSummaryAsync(
        Guid companyId, Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{ApiBase}/unread-summary?companyId={companyId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GetUnreadSummaryResponse>(json);
            _logger.LogDebug($"Loaded unread summary with {result?.Conversations.Count ?? 0} conversations");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread summary");
            throw;
        }
    }

    private long? ExtractPageToken(HttpResponseMessage response)
    {
        // Could extract pagination token from header or response body
        return null;
    }
}
