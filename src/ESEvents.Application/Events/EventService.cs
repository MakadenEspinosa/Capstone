using ESEvents.Application.Abstractions;
using ESEvents.Application.Departments;
using ESEvents.Application.Users;
using ESEvents.Domain.Common;
using ESEvents.Domain.Departments;
using ESEvents.Domain.Events;

namespace ESEvents.Application.Events;

public sealed class EventService(
    IEventRepository events,
    IUserRepository users,
    IDepartmentRepository departments,
    IUnitOfWork uow) : IEventService
{
    public async Task<IReadOnlyList<CalendarEventDto>> GetCalendarEventsAsync(
        Guid userId,
        bool isAdministrator,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default)
    {
        if (rangeEnd <= rangeStart)
            throw new DomainException("Calendar range end must be after its start.");

        var result = await events.GetVisibleInRangeAsync(
            userId, isAdministrator, rangeStart.ToUniversalTime(), rangeEnd.ToUniversalTime(), ct);
        return result.Select(e => CalendarEventDto.From(e, userId, isAdministrator)).ToArray();
    }

    public async Task<CalendarEventDto?> GetAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return null;
        await EnsureCanViewAsync(@event, userId, isAdministrator, ct);
        return CalendarEventDto.From(@event, userId, isAdministrator);
    }

    public async Task<CalendarEventDto> CreateAsync(
        Guid creatorUserId,
        bool isAdministrator,
        CreateEventRequest request,
        CancellationToken ct = default)
    {
        await EnsureActiveUserAsync(creatorUserId, ct);
        await EnsureDepartmentExistsAsync(request.DepartmentId, ct);
        if (request.DepartmentId is { } departmentId && !isAdministrator &&
            !await departments.IsMemberAsync(departmentId, creatorUserId, ct))
            throw new UnauthorizedAccessException("You must belong to the department to schedule an event for it.");
        var @event = Event.Create(request.Title, request.Start, request.End, request.AllDay, creatorUserId, request.DepartmentId);

        var participantIds = request.ParticipantUserIds ?? [];
        var activeUsers = await users.GetByIdsAsync(participantIds, ct);
        if (activeUsers.Count != participantIds.Distinct().Count() || activeUsers.Any(user => !user.IsActive))
            throw new DomainException("One or more selected users do not exist or are inactive.");

        foreach (var participantId in participantIds.Where(id => id != creatorUserId))
            @event.AddParticipant(participantId);

        await events.AddAsync(@event, ct);
        await uow.SaveChangesAsync(ct);
        return CalendarEventDto.From(@event, creatorUserId, isAdministrator);
    }

    public async Task<CalendarEventDto?> UpdateAsync(
        Guid id,
        Guid userId,
        bool isAdministrator,
        UpdateEventRequest request,
        CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return null;
        EnsureCanManage(@event, userId, isAdministrator);
        await EnsureDepartmentExistsAsync(request.DepartmentId, ct);
        if (request.DepartmentId is { } departmentId && !isAdministrator &&
            !await departments.IsMemberAsync(departmentId, userId, ct))
            throw new UnauthorizedAccessException("You must belong to the department to tag an event with it.");

        @event.UpdateDetails(request.Title, request.Start, request.End, request.AllDay, request.DepartmentId);
        await uow.SaveChangesAsync(ct);
        return CalendarEventDto.From(@event, userId, isAdministrator);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, bool isAdministrator, CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return false;
        EnsureCanManage(@event, userId, isAdministrator);
        await events.RemoveAsync(@event, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CalendarEventDto?> AddParticipantAsync(
        Guid id,
        Guid userId,
        bool isAdministrator,
        Guid participantUserId,
        EventRole role,
        CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return null;
        EnsureCanManage(@event, userId, isAdministrator);
        EnsureValidRole(role);
        await EnsureActiveUserAsync(participantUserId, ct);
        @event.AddParticipant(participantUserId, role);
        await uow.SaveChangesAsync(ct);
        return CalendarEventDto.From(@event, userId, isAdministrator);
    }

    public async Task<CalendarEventDto?> ChangeParticipantRoleAsync(
        Guid id,
        Guid userId,
        bool isAdministrator,
        Guid participantUserId,
        EventRole role,
        CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return null;
        EnsureCanManage(@event, userId, isAdministrator);
        EnsureValidRole(role);
        @event.ChangeParticipantRole(participantUserId, role);
        await uow.SaveChangesAsync(ct);
        return CalendarEventDto.From(@event, userId, isAdministrator);
    }

    public async Task<CalendarEventDto?> RemoveParticipantAsync(
        Guid id,
        Guid userId,
        bool isAdministrator,
        Guid participantUserId,
        CancellationToken ct = default)
    {
        var @event = await events.GetByIdAsync(id, ct);
        if (@event is null) return null;
        EnsureCanManage(@event, userId, isAdministrator);
        @event.RemoveParticipant(participantUserId);
        await uow.SaveChangesAsync(ct);
        return CalendarEventDto.From(@event, userId, isAdministrator);
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
            throw new DomainException("User does not exist or is inactive.");
    }

    private async Task EnsureDepartmentExistsAsync(Guid? departmentId, CancellationToken ct)
    {
        if (departmentId is not null && await departments.GetByIdAsync(departmentId.Value, ct) is null)
            throw new DomainException("Department not found.");
    }

    private async Task EnsureCanViewAsync(Event @event, Guid userId, bool isAdministrator, CancellationToken ct)
    {
        if (isAdministrator || @event.Participants.Any(p => p.UserId == userId)) return;
        if (@event.DepartmentId is { } departmentId && await departments.IsMemberAsync(departmentId, userId, ct)) return;
        throw new UnauthorizedAccessException("You do not have access to this event.");
    }

    private static void EnsureCanManage(Event @event, Guid userId, bool isAdministrator)
    {
        if (isAdministrator || @event.Participants.Any(p => p.UserId == userId && p.Role == EventRole.Organizer)) return;
        throw new UnauthorizedAccessException("Only event organizers or administrators can make this change.");
    }

    private static void EnsureValidRole(EventRole role)
    {
        if (!Enum.IsDefined(role))
            throw new DomainException("Event role is invalid.");
    }
}
