using ESEvents.Domain.Companies;
using ESEvents.Domain.Common;

namespace ESEvents.Application.Companies;

public sealed class CompanyService(ICompanyRepository companies) : ICompanyService
{
    public async Task<CompanyDto> CreateAsync(string name, Guid creatorId, CancellationToken ct = default)
    {
        if (creatorId == Guid.Empty) throw new DomainException("A company creator is required.");
        if (!await companies.IsActiveUserAsync(creatorId, ct))
            throw new UnauthorizedAccessException("Only an active user can create a company.");
        if ((await companies.GetForUserAsync(creatorId, ct)).Count != 0)
            throw new DomainConflictException("You are already assigned to a company.");

        var company = Company.Create(name);
        if (await companies.NameExistsAsync(company.NormalizedName, ct))
            throw new DomainConflictException("A company with this name already exists.");
        company.AddMember(creatorId);
        await companies.AddAsync(company, ct);
        await companies.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<IReadOnlyList<CompanyDto>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        (await companies.GetForUserAsync(userId, ct)).Select(ToDto).ToArray();

    public async Task<CompanyDto?> GetAsync(Guid companyId, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, requesterId, ct);
        var company = await companies.GetByIdAsync(companyId, ct);
        return company is null ? null : ToDto(company);
    }

    public async Task<IReadOnlyList<CompanyMemberDto>> GetMembersAsync(
        Guid companyId, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, requesterId, ct);
        return await companies.GetActiveMembersAsync(companyId, requesterId, ct);
    }

    public async Task<CompanyDto?> AddMemberAsync(Guid companyId, Guid userId, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, requesterId, ct);
        var company = await companies.GetByIdAsync(companyId, ct);
        if (company is null) return null;
        if (!await companies.IsActiveUserAsync(userId, ct))
            throw new DomainException("Only active users can be added to a company.");
        if ((await companies.GetForUserAsync(userId, ct)).Count != 0)
            throw new DomainConflictException("The user is already assigned to a company.");

        company.AddMember(userId);
        await companies.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<CompanyDto?> RemoveMemberAsync(Guid companyId, Guid userId, Guid requesterId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, requesterId, ct);
        var company = await companies.GetByIdAsync(companyId, ct);
        if (company is null) return null;

        company.RemoveMember(userId);
        await companies.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken ct = default) =>
        (await companies.GetForUserAsync(userId, ct)).Select(company => company.Id).ToArray();

    public Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default) =>
        companies.IsMemberAsync(companyId, userId, ct);

    private async Task EnsureMemberAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        if (!await companies.IsMemberAsync(companyId, userId, ct))
            throw new UnauthorizedAccessException("You are not a member of this company.");
    }

    private static CompanyDto ToDto(Company company) =>
        new(company.Id, company.Name, company.Members.Count);
}
