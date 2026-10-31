using ESEvents.Domain.Common;

namespace ESEvents.Domain.Departments;

public sealed class Department : Entity
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    private readonly List<DepartmentMember> _members = [];

    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public string? Description { get; private set; }
    public IReadOnlyCollection<DepartmentMember> Members => _members;

    private Department() { }

    public static Department Create(string? name, string? description = null)
    {
        var department = new Department();
        department.SetDetails(name, description);
        return department;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void Update(string? name, string? description)
    {
        SetDetails(name, description);
        Touch();
    }

    public void AddMember(Guid userId)
    {
        if (userId == Guid.Empty) throw new DomainException("A user is required.");
        if (_members.Any(m => m.UserId == userId))
            throw new DomainException("User is already a member of this department.");

        _members.Add(new DepartmentMember(Id, userId));
        Touch();
    }

    public void RemoveMember(Guid userId)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("User is not a member of this department.");

        _members.Remove(member);
        Touch();
    }

    private void SetDetails(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Department name is required.");
        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
            throw new DomainException($"Department name cannot exceed {MaxNameLength} characters.");

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > MaxDescriptionLength })
            throw new DomainException($"Department description cannot exceed {MaxDescriptionLength} characters.");

        Name = trimmedName;
        NormalizedName = Normalize(trimmedName);
        Description = trimmedDescription;
    }
}
