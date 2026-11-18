namespace ESEvents.Domain.Companies;

public sealed class CompanyMember
{
    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    private CompanyMember() { }

    internal CompanyMember(Guid companyId, Guid userId)
    {
        CompanyId = companyId;
        UserId = userId;
        AddedAtUtc = DateTime.UtcNow;
    }
}
