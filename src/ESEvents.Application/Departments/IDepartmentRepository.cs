using ESEvents.Domain.Departments;

namespace ESEvents.Application.Departments;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Department>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default);
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken ct = default);
    Task AddAsync(Department department, CancellationToken ct = default);
    Task RemoveAsync(Department department, CancellationToken ct = default);
}
