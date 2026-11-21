using ESEvents.Application.Companies;
using ESEvents.Domain.Companies;
using ESEvents.Domain.Common;
using Moq;
using Xunit;

namespace ESEvents.Web.Tests;

public sealed class CompanyManagementTests
{
    [Fact]
    public async Task MemberCode_CanBeRotatedAndUsedToJoin()
    {
        var company = Company.Create("Target");
        var owner = Guid.NewGuid();
        var joining = Guid.NewGuid();
        company.AddMember(owner, CompanyRole.Owner);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(joining, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(joining, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Company>());
        repository.Setup(r => r.GetByCodeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string hash, CancellationToken _) => company.MemberCodeHash == hash ? company : null);
        var service = new CompanyService(repository.Object);

        var firstCode = await service.RotateCodeAsync(company.Id, owner, false);
        var secondCode = await service.RotateCodeAsync(company.Id, owner, false);
        Assert.NotEqual(firstCode, secondCode);
        Assert.NotEqual(secondCode, company.MemberCodeHash);
        await Assert.ThrowsAsync<DomainException>(() => service.JoinAsync(firstCode, joining));
        await service.JoinAsync(secondCode, joining);
        Assert.Equal(CompanyRole.Member, company.Members.Single(m => m.UserId == joining).Role);
    }

    [Fact]
    public async Task AdminCannotRotateAdminCodeOrTransferOwnership()
    {
        var company = Company.Create("Target");
        var owner = Guid.NewGuid();
        var admin = Guid.NewGuid();
        company.AddMember(owner, CompanyRole.Owner);
        company.AddMember(admin, CompanyRole.Administrator);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, admin, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        var service = new CompanyService(repository.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RotateCodeAsync(company.Id, admin, true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.TransferOwnershipAsync(company.Id, admin, owner));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminCode_JoinsAsAdministrator()
    {
        var company = Company.Create("Target");
        var owner = Guid.NewGuid();
        var joining = Guid.NewGuid();
        company.AddMember(owner, CompanyRole.Owner);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, owner, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(joining, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(joining, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Company>());
        repository.Setup(r => r.GetByCodeHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string hash, CancellationToken _) => company.AdminCodeHash == hash ? company : null);
        var service = new CompanyService(repository.Object);

        var code = await service.RotateCodeAsync(company.Id, owner, true);
        await service.JoinAsync(code, joining);
        Assert.Equal(CompanyRole.Administrator, company.Members.Single(m => m.UserId == joining).Role);
    }

    [Fact]
    public async Task OrdinaryMemberCannotAddUsers()
    {
        var company = Company.Create("Target");
        var user = Guid.NewGuid();
        company.AddMember(user);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new CompanyService(repository.Object).AddMemberAsync(company.Id, Guid.NewGuid(), user));
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TransferOwnership_RevokesAdminCode()
    {
        var company = Company.Create("Target");
        var owner = Guid.NewGuid();
        var successor = Guid.NewGuid();
        company.AddMember(owner, CompanyRole.Owner);
        company.AddMember(successor, CompanyRole.Administrator);
        company.SetCodeHash(CompanyRole.Administrator, "hash");
        company.TransferOwnership(owner, successor);
        Assert.Null(company.AdminCodeHash);
        Assert.Equal(CompanyRole.Owner, company.Members.Single(m => m.UserId == successor).Role);
        Assert.Equal(CompanyRole.Administrator, company.Members.Single(m => m.UserId == owner).Role);
    }

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
        company.AddMember(requester, CompanyRole.Owner);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, requester, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Company.Create("Other")]);
        var service = new CompanyService(repository.Object);

        await Assert.ThrowsAsync<DomainConflictException>(() => service.AddMemberAsync(company.Id, user, requester));
        Assert.Single(company.Members);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddMember_AddsAnActiveUnassignedUser()
    {
        var company = Company.Create("Target");
        var requester = Guid.NewGuid();
        var user = Guid.NewGuid();
        company.AddMember(requester, CompanyRole.Owner);
        var repository = new Mock<ICompanyRepository>();
        repository.Setup(r => r.IsMemberAsync(company.Id, requester, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        repository.Setup(r => r.IsActiveUserAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(r => r.GetForUserAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Company>());

        var result = await new CompanyService(repository.Object).AddMemberAsync(company.Id, user, requester);

        Assert.Contains(company.Members, member => member.UserId == user);
        Assert.Equal(2, result!.MemberCount);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
