namespace ESEvents.Application.Companies;

public interface ICompanyService
{
    Task<CompanyDto> CreateAsync(string name, Guid creatorId, CancellationToken ct = default);
    Task<CompanyDto?> GetAsync(Guid companyId, Guid requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyDto>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyMemberDto>> GetMembersAsync(
        Guid companyId, Guid requesterId, CancellationToken ct = default);
    Task<CompanyDto?> AddMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<CompanyDto?> RemoveMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default);
}
