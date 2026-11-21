using ESEvents.Application.Companies;
using ESEvents.Domain.Companies;
using ESEvents.Domain.Common;
using Moq;
using Xunit;

namespace ESEvents.Web.Tests;

public sealed class CompanyManagementTests
{
    [Fact]
    public async Task Create_RejectsUserAlreadyInACompany()
    {
        var user = Guid.NewGuid();
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsActiveUserAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Company.Create("Existing")]);
        var service = new CompanyService(repository.Object);

        await Assert.ThrowsAsync<DomainConflictException>(() => service.CreateAsync("New", user));
        repository.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMember_RejectsRequesterOutsideCompany()
    {
        var repository = new Mock<ICompanyRepository>();
        var service = new CompanyService(repository.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddMemberAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveMember_RejectsRequesterOutsideCompany()
    {
        var repository = new Mock<ICompanyRepository>();
        var service = new CompanyService(repository.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMember_RejectsUserAlreadyAssigned()
    {
        var company = Company.Create("Target");
        var requester = Guid.NewGuid();
        var user = Guid.NewGuid();
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, requester, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Company.Create("Other")]);
        var service = new CompanyService(repository.Object);

        await Assert.ThrowsAsync<DomainConflictException>(() => service.AddMemberAsync(company.Id, user, requester));
        Assert.Empty(company.Members);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMember_AddsAnActiveUnassignedUser()
    {
        var company = Company.Create("Target");
        var requester = Guid.NewGuid();
        var user = Guid.NewGuid();
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, requester, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Company>());

        var result = await new CompanyService(repository.Object).AddMemberAsync(company.Id, user, requester);

        Assert.Equal(user, Assert.Single(company.Members).UserId);
        Assert.Equal(1, result!.MemberCount);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
