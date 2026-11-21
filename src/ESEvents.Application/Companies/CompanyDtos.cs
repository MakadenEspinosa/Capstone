namespace ESEvents.Application.Companies;

/// <summary>Company available to the authenticated user.</summary>
public sealed record CompanyDto(Guid Id, string Name, int MemberCount, string? CurrentUserRole = null);

/// <summary>Company member and their company-specific role.</summary>
public sealed record CompanyManagementMemberDto(Guid UserId, string FirstName, string LastName, string Role);

/// <summary>Company join code supplied by an unassigned account.</summary>
public sealed record JoinCompanyRequest(string Code);

/// <summary>Email of an unassigned account to add.</summary>
public sealed record AddCompanyMemberRequest(string Email);

/// <summary>Company administrator status to assign.</summary>
public sealed record ChangeCompanyRoleRequest(bool Administrator);

/// <summary>Existing company administrator to receive ownership.</summary>
public sealed record TransferCompanyOwnershipRequest(Guid UserId);

/// <summary>Company member available to start a chat with.</summary>
public sealed record CompanyMemberDto(Guid UserId, string FirstName, string LastName);

/// <summary>Request to create a company.</summary>
public sealed record CreateCompanyRequest(string Name);
