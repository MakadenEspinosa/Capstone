namespace ESEvents.Application.Companies;

public interface ICompanyService
{
    Task<CompanyDto> CreateAsync(string name, Guid creatorId, CancellationToken ct = default);
    Task<CompanyDto?> GetAsync(Guid companyId, Guid requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyDto>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyMemberDto>> GetMembersAsync(
        Guid companyId, Guid requesterId, CancellationToken ct = default);
    Task<CompanyDto?> AddMemberAsync(Guid companyId, Guid userId, Guid requesterId, CancellationToken ct = default);
    Task<CompanyDto?> RemoveMemberAsync(Guid companyId, Guid userId, Guid requesterId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default);
    Task<CompanyDto> JoinAsync(string code, Guid userId, CancellationToken ct = default);
    Task<string> RotateCodeAsync(Guid companyId, Guid requesterId, bool administratorCode, CancellationToken ct = default);
    Task AddByEmailAsync(Guid companyId, Guid requesterId, string email, CancellationToken ct = default);
    Task ChangeRoleAsync(Guid companyId, Guid requesterId, Guid userId, bool administrator, CancellationToken ct = default);
    Task TransferOwnershipAsync(Guid companyId, Guid requesterId, Guid successorId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanyManagementMemberDto>> GetManagementMembersAsync(Guid companyId, Guid requesterId, CancellationToken ct = default);
    Task RecoverOwnershipAsync(Guid companyId, Guid requesterId, Guid newOwnerId, CancellationToken ct = default);
}
