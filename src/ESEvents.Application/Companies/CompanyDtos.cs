namespace ESEvents.Application.Companies;

/// <summary>Company available to the authenticated user.</summary>
public sealed record CompanyDto(Guid Id, string Name, int MemberCount);

/// <summary>Company member available to start a chat with.</summary>
public sealed record CompanyMemberDto(Guid UserId, string FirstName, string LastName);

/// <summary>Request to create a company.</summary>
public sealed record CreateCompanyRequest(string Name);
