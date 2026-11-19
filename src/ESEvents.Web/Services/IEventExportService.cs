namespace ESEvents.Web.Services;

public interface IEventExportService
{
    Task<byte[]> ExportAsync(
        Guid userId, bool isAdministrator, DateOnly start, DateOnly end,
        string timeZoneId, bool includeParticipants, string format, CancellationToken ct = default);
}
