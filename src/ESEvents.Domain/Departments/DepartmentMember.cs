namespace ESEvents.Domain.Departments;

public sealed class DepartmentMember
{
    public Guid DepartmentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    private DepartmentMember() { }

    internal DepartmentMember(Guid departmentId, Guid userId)
    {
        DepartmentId = departmentId;
        UserId = userId;
        AddedAtUtc = DateTime.UtcNow;
    }
}
