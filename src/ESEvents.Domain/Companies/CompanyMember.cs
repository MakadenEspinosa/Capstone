namespace ESEvents.Domain.Companies;

public sealed class CompanyMember
{
    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }
    public CompanyRole Role { get; private set; }

    private CompanyMember() { }

    internal CompanyMember(Guid companyId, Guid userId, CompanyRole role)
    {
        CompanyId = companyId;
        UserId = userId;
        Role = role;
        AddedAtUtc = DateTime.UtcNow;
    }

    internal void ChangeRole(CompanyRole role) => Role = role;
}
