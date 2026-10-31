using ESEvents.Application.Departments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEvents.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/departments")]
public sealed class DepartmentsController(IDepartmentService departments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken ct) =>
        Ok(await departments.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentDetailDto>> Get(Guid id, CancellationToken ct)
    {
        var department = await departments.GetAsync(id, ct);
        return department is null ? NotFound() : department;
    }

    [HttpPost]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<DepartmentDetailDto>> Create(CreateDepartmentRequest request, CancellationToken ct)
    {
        var department = await departments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = department.Id }, department);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<DepartmentDetailDto>> Update(Guid id, UpdateDepartmentRequest request, CancellationToken ct)
    {
        var department = await departments.UpdateAsync(id, request, ct);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await departments.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<DepartmentDetailDto>> AddMember(Guid id, Guid userId, CancellationToken ct)
    {
        var department = await departments.AddMemberAsync(id, userId, ct);
        return department is null ? NotFound() : Ok(department);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = "Administrator")]
    public async Task<ActionResult<DepartmentDetailDto>> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var department = await departments.RemoveMemberAsync(id, userId, ct);
        return department is null ? NotFound() : Ok(department);
    }
}
