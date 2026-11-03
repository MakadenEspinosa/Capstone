namespace ESEvents.Application.Departments;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> SetForUserAsync(Guid userId, IReadOnlyCollection<Guid> departmentIds, CancellationToken ct = default);
    Task<DepartmentDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<DepartmentDetailDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default);
    Task<DepartmentDetailDto?> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<DepartmentDetailDto?> AddMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default);
    Task<DepartmentDetailDto?> RemoveMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default);
}
