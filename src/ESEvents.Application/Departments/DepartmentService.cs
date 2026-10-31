using ESEvents.Application.Abstractions;
using ESEvents.Application.Users;
using ESEvents.Domain.Common;
using ESEvents.Domain.Departments;

namespace ESEvents.Application.Departments;

public sealed class DepartmentService(
    IDepartmentRepository departments,
    IUserRepository users,
    IUnitOfWork uow) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default) =>
        (await departments.GetAllAsync(ct)).Select(DepartmentDto.From).ToArray();

    public async Task<IReadOnlyList<DepartmentDto>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        (await departments.GetForUserAsync(userId, ct)).Select(DepartmentDto.From).ToArray();

    public async Task<DepartmentDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(id, ct);
        return department is null ? null : await ToDetailAsync(department, ct);
    }

    public async Task<DepartmentDetailDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default)
    {
        var department = Department.Create(request.Name, request.Description);
        await EnsureNameAvailableAsync(department, ct);

        await departments.AddAsync(department, ct);
        await uow.SaveChangesAsync(ct);
        return await ToDetailAsync(department, ct);
    }

    public async Task<DepartmentDetailDto?> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(id, ct);
        if (department is null) return null;

        department.Update(request.Name, request.Description);
        await EnsureNameAvailableAsync(department, ct);

        await uow.SaveChangesAsync(ct);
        return await ToDetailAsync(department, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(id, ct);
        if (department is null) return false;

        await departments.RemoveAsync(department, ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<DepartmentDetailDto?> AddMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(departmentId, ct);
        if (department is null) return null;

        var user = await users.GetByIdAsync(userId, ct) ?? throw new DomainException("User not found.");
        if (!user.IsActive) throw new DomainException("Only active users can be added to a department.");

        department.AddMember(userId);
        await uow.SaveChangesAsync(ct);
        return await ToDetailAsync(department, ct);
    }

    public async Task<DepartmentDetailDto?> RemoveMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(departmentId, ct);
        if (department is null) return null;

        department.RemoveMember(userId);
        await uow.SaveChangesAsync(ct);
        return await ToDetailAsync(department, ct);
    }

    private async Task EnsureNameAvailableAsync(Department department, CancellationToken ct)
    {
        if (await departments.NameExistsAsync(department.NormalizedName, department.Id, ct))
            throw new DomainException("A department with this name already exists.");
    }

    private async Task<DepartmentDetailDto> ToDetailAsync(Department department, CancellationToken ct)
    {
        var memberIds = department.Members.Select(m => m.UserId).ToArray();
        var memberUsers = memberIds.Length == 0
            ? []
            : await users.GetByIdsAsync(memberIds, ct);

        var members = memberUsers
            .OrderBy(u => u.Profile.LastName)
            .ThenBy(u => u.Profile.FirstName)
            .Select(u => new DepartmentMemberDto(u.Id, u.Email.Value, u.Profile.FirstName, u.Profile.LastName))
            .ToArray();

        return new DepartmentDetailDto(department.Id, department.Name, department.Description, members);
    }
}
