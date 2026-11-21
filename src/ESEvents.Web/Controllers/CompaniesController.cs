using System.Security.Claims;
using ESEvents.Application.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/companies")]
public sealed class CompaniesController(ICompanyService companies) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetMine(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await companies.GetForUserAsync(userId, ct));
    }

    [HttpGet("{companyId:guid}")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> Get(Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var company = await companies.GetAsync(companyId, userId, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpGet("{companyId:guid}/members")]
    [ProducesResponseType<IReadOnlyList<CompanyMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CompanyMemberDto>>> GetMembers(Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await companies.GetMembersAsync(companyId, userId, ct));
    }

    [HttpPost]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var company = await companies.CreateAsync(request.Name, userId, ct);
        return CreatedAtAction(nameof(Get), new { companyId = company.Id }, company);
    }

    [HttpPut("{companyId:guid}/members/{userId:guid}")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> AddMember(Guid companyId, Guid userId, CancellationToken ct)
    {
        if (!TryGetUserId(out var requesterId)) return Unauthorized();
        var company = await companies.AddMemberAsync(companyId, userId, requesterId, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpDelete("{companyId:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid companyId, Guid userId, CancellationToken ct)
    {
        if (!TryGetUserId(out var requesterId)) return Unauthorized();
        var company = await companies.RemoveMemberAsync(companyId, userId, requesterId, ct);
        return company is null ? NotFound() : NoContent();
    }

    [HttpPost("join")]
    public async Task<ActionResult<CompanyDto>> Join(JoinCompanyRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await companies.JoinAsync(request.Code, userId, ct));
    }

    [HttpPost("{companyId:guid}/codes/{kind}")]
    public async Task<IActionResult> RotateCode(Guid companyId, string kind, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (kind is not ("member" or "admin")) return BadRequest();
        var code = await companies.RotateCodeAsync(companyId, userId, kind == "admin", ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(new { Code = code });
    }

    [HttpPost("{companyId:guid}/members")]
    public async Task<IActionResult> AddByEmail(Guid companyId, AddCompanyMemberRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await companies.AddByEmailAsync(companyId, userId, request.Email, ct);
        return NoContent();
    }

    [HttpGet("{companyId:guid}/management-members")]
    public async Task<ActionResult<IReadOnlyList<CompanyManagementMemberDto>>> ManagementMembers(Guid companyId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await companies.GetManagementMembersAsync(companyId, userId, ct));
    }

    [HttpPut("{companyId:guid}/members/{memberId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid companyId, Guid memberId, ChangeCompanyRoleRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await companies.ChangeRoleAsync(companyId, userId, memberId, request.Administrator, ct);
        return NoContent();
    }

    [HttpPost("{companyId:guid}/ownership")]
    public async Task<IActionResult> TransferOwnership(Guid companyId, TransferCompanyOwnershipRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await companies.TransferOwnershipAsync(companyId, userId, request.UserId, ct);
        return NoContent();
    }

    [HttpPost("{companyId:guid}/recover-owner")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> RecoverOwnership(Guid companyId, TransferCompanyOwnershipRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await companies.RecoverOwnershipAsync(companyId, userId, request.UserId, ct);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
