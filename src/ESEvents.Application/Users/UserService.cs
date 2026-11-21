using ESEvents.Application.Abstractions;
using ESEvents.Application.Companies;
using ESEvents.Domain.Common;
using ESEvents.Domain.Users;

namespace ESEvents.Application.Users;

public sealed class UserService(IUserRepository users, IPasswordHasher hasher, IUnitOfWork uow, ICompanyRepository companies) : IUserService
{
    public async Task<UserDto> RegisterAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        var email = Email.Create(request.Email);
        if (await users.EmailExistsAsync(email, ct))
            throw new DomainException("Email is already registered.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");

        var profile = UserProfile.Create(request.FirstName, request.LastName);
        var user = User.Register(email, hasher.Hash(request.Password), profile);

        await users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    public async Task<UserDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user is null ? null : UserDto.From(user);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var results = await users.GetAllAsync(ct);
        return results.Select(UserDto.From).ToArray();
    }

    public async Task<UserDto?> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct);
        if (user is null) return null;

        user.UpdateProfile(UserProfile.Create(request.FirstName, request.LastName, request.PhoneNumber, request.Bio));
        await users.UpdateAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var memberships = await companies.GetForUserAsync(id, ct);
        if (memberships.Any(company => company.Members.Any(member => member.UserId == id && member.Role == ESEvents.Domain.Companies.CompanyRole.Owner)))
            throw new DomainConflictException("Transfer company ownership before deleting this account.");
        if (memberships.Count > 0)
            throw new DomainConflictException("Account deletion is unavailable for company members until safe removal is implemented.");
        var deleted = await users.DeleteAsync(id, ct);
        if (!deleted)
            return false;

        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task SuspendAsync(Guid id, CancellationToken ct = default)
    {
        var memberships = await companies.GetForUserAsync(id, ct);
        if (memberships.Any(company => company.Members.Any(member => member.UserId == id && member.Role == ESEvents.Domain.Companies.CompanyRole.Owner)))
            throw new DomainConflictException("Transfer company ownership before suspending this account.");
        await MutateAsync(id, u => u.Suspend(), ct);
    }
    public Task ReinstateAsync(Guid id, CancellationToken ct = default) => MutateAsync(id, u => u.Reinstate(), ct);
    private async Task MutateAsync(Guid id, Action<User> action, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new DomainException("User not found.");
        action(user);
        await users.UpdateAsync(user, ct);
        await uow.SaveChangesAsync(ct);
    }
}
