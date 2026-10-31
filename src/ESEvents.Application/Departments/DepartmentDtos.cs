using ESEvents.Domain.Departments;

namespace ESEvents.Application.Departments;

public sealed record CreateDepartmentRequest(string Name, string? Description);

public sealed record UpdateDepartmentRequest(string Name, string? Description);

public sealed record DepartmentDto(Guid Id, string Name, string? Description, int MemberCount)
{
    public static DepartmentDto From(Department d) => new(d.Id, d.Name, d.Description, d.Members.Count);
}

public sealed record DepartmentMemberDto(Guid UserId, string Email, string FirstName, string LastName);

public sealed record DepartmentDetailDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<DepartmentMemberDto> Members);
