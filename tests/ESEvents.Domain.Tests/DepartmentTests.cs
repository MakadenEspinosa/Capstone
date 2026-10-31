using ESEvents.Domain.Common;
using ESEvents.Domain.Departments;

namespace ESEvents.Domain.Tests;

public class DepartmentTests
{
    [Fact]
    public void Create_TrimsAndNormalizesName()
    {
        var department = Department.Create("  Human Resources ", "  People ops ");

        Assert.Equal("Human Resources", department.Name);
        Assert.Equal("HUMAN RESOURCES", department.NormalizedName);
        Assert.Equal("People ops", department.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RequiresName(string? name) =>
        Assert.Throws<DomainException>(() => Department.Create(name));

    [Fact]
    public void Create_RejectsTooLongName() =>
        Assert.Throws<DomainException>(() => Department.Create(new string('x', Department.MaxNameLength + 1)));

    [Fact]
    public void Update_ChangesDetails()
    {
        var department = Department.Create("Sales");

        department.Update("Marketing", null);

        Assert.Equal("MARKETING", department.NormalizedName);
        Assert.Null(department.Description);
        Assert.NotNull(department.UpdatedAtUtc);
    }

    [Fact]
    public void AddMember_RejectsDuplicates()
    {
        var department = Department.Create("Sales");
        var userId = Guid.NewGuid();
        department.AddMember(userId);

        Assert.Single(department.Members);
        Assert.Throws<DomainException>(() => department.AddMember(userId));
    }

    [Fact]
    public void RemoveMember_RemovesOnlyThatMember()
    {
        var department = Department.Create("Sales");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        department.AddMember(first);
        department.AddMember(second);

        department.RemoveMember(first);

        Assert.Equal(second, Assert.Single(department.Members).UserId);
        Assert.Throws<DomainException>(() => department.RemoveMember(first));
    }
}
