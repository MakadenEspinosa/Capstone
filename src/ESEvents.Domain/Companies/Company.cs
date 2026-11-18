using ESEvents.Domain.Common;

namespace ESEvents.Domain.Companies;

public sealed class Company : Entity
{
    public const int MaxNameLength = 150;

    private readonly List<CompanyMember> _members = [];

    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public IReadOnlyCollection<CompanyMember> Members => _members;

    private Company() { }

    public static Company Create(string? name)
    {
        var company = new Company();
        company.SetName(name);
        return company;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void AddMember(Guid userId)
    {
        if (userId == Guid.Empty) throw new DomainException("A user is required.");
        if (_members.Any(member => member.UserId == userId))
            throw new DomainException("User is already a member of this company.");

        _members.Add(new CompanyMember(Id, userId));
        Touch();
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(item => item.UserId == userId)
            ?? throw new DomainException("User is not a member of this company.");

        _members.Remove(member);
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
