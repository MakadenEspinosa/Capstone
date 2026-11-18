using ESEvents.Application.Companies;
using ESEvents.Domain.Companies;
using ESEvents.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ESEvents.Infrastructure.Persistence.Companies;

internal sealed class CompanyRepository(AppDbContext db) : ICompanyRepository
{
    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken ct = default) =>
        db.Companies.Include(company => company.Members)
            .FirstOrDefaultAsync(company => company.Id == companyId, ct);

    public async Task<IReadOnlyList<Company>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Companies
            .Include(company => company.Members)
            .Where(company => company.Members.Any(member =>
                member.UserId == userId &&
                db.Users.Any(user => user.Id == member.UserId && user.Status == UserStatus.Active)))
            .OrderBy(company => company.Name)
            .ToArrayAsync(ct);

    public async Task<IReadOnlyList<CompanyMemberDto>> GetActiveMembersAsync(
        Guid companyId, Guid excludedUserId, CancellationToken ct = default) =>
        await db.Set<CompanyMember>()
            .Where(member => member.CompanyId == companyId && member.UserId != excludedUserId)
            .Join(db.Users.Where(user => user.Status == UserStatus.Active),
                member => member.UserId,
                user => user.Id,
                (_, user) => user)
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Select(user => new CompanyMemberDto(user.Id, user.FirstName, user.LastName))
            .ToArrayAsync(ct);

    public Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default) =>
        db.Set<CompanyMember>().AnyAsync(member =>
            member.CompanyId == companyId &&
            member.UserId == userId &&
            db.Users.Any(user => user.Id == userId && user.Status == UserStatus.Active), ct);

    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.AnyAsync(user => user.Id == userId && user.Status == UserStatus.Active, ct);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken ct = default) =>
        db.Companies.AnyAsync(company => company.NormalizedName == normalizedName, ct);

    public async Task AddAsync(Company company, CancellationToken ct = default) =>
        await db.Companies.AddAsync(company, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
