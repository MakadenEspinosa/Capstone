using ESEvents.Domain.Companies;

namespace ESEvents.Application.Companies;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid companyId, CancellationToken ct = default);
    Task<Company?> GetByCodeHashAsync(string hash, CancellationToken ct = default);
    Task<Guid?> FindActiveUserByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> IsPlatformAdministratorAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Company>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyMemberDto>> GetActiveMembersAsync(
        Guid companyId, Guid excludedUserId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string normalizedName, CancellationToken ct = default);
    Task AddAsync(Company company, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
