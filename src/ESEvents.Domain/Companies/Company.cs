using ESEvents.Domain.Common;

namespace ESEvents.Domain.Companies;

public sealed class Company : Entity
{
    public const int MaxNameLength = 150;

    private readonly List<CompanyMember> _members = [];

    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public IReadOnlyCollection<CompanyMember> Members => _members;
    public string? MemberCodeHash { get; private set; }
    public string? AdminCodeHash { get; private set; }

    private Company() { }

    public static Company Create(string? name)
    {
        var company = new Company();
        company.SetName(name);
        return company;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void AddMember(Guid userId, CompanyRole role = CompanyRole.Member)
    {
        if (userId == Guid.Empty) throw new DomainException("A user is required.");
        if (_members.Any(member => member.UserId == userId))
            throw new DomainException("User is already a member of this company.");

        if (!Enum.IsDefined(role)) throw new DomainException("Invalid company role.");
        if (role == CompanyRole.Owner && _members.Any(member => member.Role == CompanyRole.Owner))
            throw new DomainException("The company already has an owner.");
        _members.Add(new CompanyMember(Id, userId, role));
        Touch();
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(item => item.UserId == userId)
            ?? throw new DomainException("User is not a member of this company.");
        if (member.Role == CompanyRole.Owner)
            throw new DomainException("Transfer ownership before removing the owner.");

        _members.Remove(member);
        Touch();
    }

    public void SetCodeHash(CompanyRole role, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new DomainException("A code hash is required.");
        if (role == CompanyRole.Member) MemberCodeHash = hash;
        else if (role == CompanyRole.Administrator) AdminCodeHash = hash;
        else throw new DomainException("Invalid join code role.");
        Touch();
    }

    public void ChangeRole(Guid userId, CompanyRole role)
    {
        if (role is not (CompanyRole.Member or CompanyRole.Administrator))
            throw new DomainException("Invalid company role.");
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("User is not a member of this company.");
        if (member.Role == CompanyRole.Owner)
            throw new DomainException("Transfer ownership before changing the owner's role.");
        member.ChangeRole(role);
        Touch();
    }

    public void TransferOwnership(Guid ownerId, Guid newOwnerId)
    {
        var owner = _members.FirstOrDefault(m => m.UserId == ownerId && m.Role == CompanyRole.Owner)
            ?? throw new UnauthorizedAccessException("Only the owner can transfer ownership.");
        var successor = _members.FirstOrDefault(m => m.UserId == newOwnerId && m.Role == CompanyRole.Administrator)
            ?? throw new DomainException("Choose a company administrator as the new owner.");
        owner.ChangeRole(CompanyRole.Administrator);
        successor.ChangeRole(CompanyRole.Owner);
        AdminCodeHash = null;
        Touch();
    }

    public void RecoverOwnership(Guid userId)
    {
        if (_members.Any(m => m.Role == CompanyRole.Owner))
            throw new DomainConflictException("The company already has an owner.");
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("The new owner must be a company member.");
        member.ChangeRole(CompanyRole.Owner);
        AdminCodeHash = null;
        Touch();
    }

    private void SetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Company name is required.");
        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new DomainException($"Company name cannot exceed {MaxNameLength} characters.");

        Name = trimmed;
        NormalizedName = trimmed.ToUpperInvariant();
    }
}
