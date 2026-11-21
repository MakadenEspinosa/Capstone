using ESEvents.Domain.Companies;
using ESEvents.Domain.Common;
using System.Security.Cryptography;
using System.Text;

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
        company.AddMember(creatorId, CompanyRole.Owner);
        await companies.AddAsync(company, ct);
        await companies.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<IReadOnlyList<CompanyDto>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        (await companies.GetForUserAsync(userId, ct))
            .Select(company => ToDto(company) with
            {
                CurrentUserRole = company.Members.Single(member => member.UserId == userId).Role.ToString()
            }).ToArray();

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
        var company = await EnsureManagerAsync(companyId, requesterId, false, ct);
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
        await EnsureManagerAsync(companyId, requesterId, false, ct);
        throw new DomainConflictException("Member removal is unavailable until company isolation and event ownership are implemented.");
    }

    public async Task<IReadOnlyList<Guid>> GetCompanyIdsForUserAsync(Guid userId, CancellationToken ct = default) =>
        (await companies.GetForUserAsync(userId, ct)).Select(company => company.Id).ToArray();

    public Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken ct = default) =>
        companies.IsMemberAsync(companyId, userId, ct);

    public async Task<CompanyDto> JoinAsync(string code, Guid userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 64 || !code.Trim().All(Uri.IsHexDigit))
            throw new DomainException("The join code is invalid.");
        if (!await companies.IsActiveUserAsync(userId, ct))
            throw new UnauthorizedAccessException("Only an active user can join a company.");
        if ((await companies.GetForUserAsync(userId, ct)).Count != 0)
            throw new DomainConflictException("You are already assigned to a company.");
        var hash = HashCode(code.Trim().ToUpperInvariant());
        var company = await companies.GetByCodeHashAsync(hash, ct)
            ?? throw new DomainException("The join code is invalid.");
        company.AddMember(userId, company.AdminCodeHash == hash ? CompanyRole.Administrator : CompanyRole.Member);
        await companies.SaveChangesAsync(ct);
        return ToDto(company);
    }

    public async Task<string> RotateCodeAsync(Guid companyId, Guid requesterId, bool administratorCode, CancellationToken ct = default)
    {
        var company = await EnsureManagerAsync(companyId, requesterId, administratorCode, ct);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        company.SetCodeHash(administratorCode ? CompanyRole.Administrator : CompanyRole.Member, HashCode(code));
        await companies.SaveChangesAsync(ct);
        return code;
    }

    public async Task AddByEmailAsync(Guid companyId, Guid requesterId, string email, CancellationToken ct = default)
    {
        await EnsureManagerAsync(companyId, requesterId, false, ct);
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("An email is required.");
        var userId = await companies.FindActiveUserByEmailAsync(email, ct);
        if (userId is null || (await companies.GetForUserAsync(userId.Value, ct)).Count != 0)
            throw new DomainException("No active unassigned account matches that email.");
        await AddMemberAsync(companyId, userId.Value, requesterId, ct);
    }

    public async Task ChangeRoleAsync(Guid companyId, Guid requesterId, Guid userId, bool administrator, CancellationToken ct = default)
    {
        var company = await EnsureManagerAsync(companyId, requesterId, true, ct);
        company.ChangeRole(userId, administrator ? CompanyRole.Administrator : CompanyRole.Member);
        await companies.SaveChangesAsync(ct);
    }

    public async Task TransferOwnershipAsync(Guid companyId, Guid requesterId, Guid successorId, CancellationToken ct = default)
    {
        var company = await EnsureManagerAsync(companyId, requesterId, true, ct);
        if (!await companies.IsActiveUserAsync(successorId, ct))
            throw new DomainException("The new owner must be an active user.");
        company.TransferOwnership(requesterId, successorId);
        await companies.SaveChangesAsync(ct);
    }

    public async Task RecoverOwnershipAsync(Guid companyId, Guid requesterId, Guid newOwnerId, CancellationToken ct = default)
    {
        if (!await companies.IsPlatformAdministratorAsync(requesterId, ct))
            throw new UnauthorizedAccessException("Platform administrator access is required.");
        var company = await companies.GetByIdAsync(companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");
        if (!await companies.IsActiveUserAsync(newOwnerId, ct))
            throw new DomainException("The new owner must be an active user.");
        company.RecoverOwnership(newOwnerId);
        await companies.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyManagementMemberDto>> GetManagementMembersAsync(
        Guid companyId, Guid requesterId, CancellationToken ct = default)
    {
        var company = await EnsureManagerAsync(companyId, requesterId, false, ct);
        var names = await companies.GetActiveMembersAsync(companyId, Guid.Empty, ct);
        return names.Select(user => new CompanyManagementMemberDto(user.UserId, user.FirstName, user.LastName,
            company.Members.Single(m => m.UserId == user.UserId).Role.ToString())).ToArray();
    }

    private async Task<Company> EnsureManagerAsync(Guid companyId, Guid userId, bool ownerOnly, CancellationToken ct)
    {
        await EnsureMemberAsync(companyId, userId, ct);
        var company = await companies.GetByIdAsync(companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");
        var role = company.Members.Single(m => m.UserId == userId).Role;
        if (role != CompanyRole.Owner && (ownerOnly || role != CompanyRole.Administrator))
            throw new UnauthorizedAccessException(ownerOnly ? "Only the company owner can do this." : "Company administrator access is required.");
        return company;
    }

    private static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    private async Task EnsureMemberAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        if (!await companies.IsMemberAsync(companyId, userId, ct))
            throw new UnauthorizedAccessException("You are not a member of this company.");
    }

    private static CompanyDto ToDto(Company company) =>
        new(company.Id, company.Name, company.Members.Count);
}
