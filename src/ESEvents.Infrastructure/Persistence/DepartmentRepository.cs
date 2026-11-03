using ESEvents.Application.Departments;
using ESEvents.Domain.Departments;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence;

internal sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    public async Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct = default) =>
        await db.Departments.Include(d => d.Members).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Department>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Departments
            .Include(d => d.Members)
            .Where(d => d.Members.Any(m => m.UserId == userId))
            .OrderBy(d => d.Name)
            .ToListAsync(ct);

    public Task<bool> IsMemberAsync(Guid departmentId, Guid userId, CancellationToken ct = default) =>
        db.Departments.AnyAsync(d => d.Id == departmentId && d.Members.Any(m => m.UserId == userId), ct);

    public Task<Department?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Departments.Include(d => d.Members).FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<bool> NameExistsAsync(string normalizedName, Guid? excludeId, CancellationToken ct = default) =>
        db.Departments.AnyAsync(d => d.NormalizedName == normalizedName && d.Id != excludeId, ct);

    public async Task AddAsync(Department department, CancellationToken ct = default) =>
        await db.Departments.AddAsync(department, ct);

    public Task RemoveAsync(Department department, CancellationToken ct = default)
    {
        db.Departments.Remove(department);
        return Task.CompletedTask;
    }
}
