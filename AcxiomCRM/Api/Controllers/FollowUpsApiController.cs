using AcxiomCRM.Authorization;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/followups")]
[Authorize]
public sealed class FollowUpsApiController(FollowUpService followUpService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await followUpService.GetPageAsync(status, UserId(), CanReadAll(), page, pageSize, cancellationToken);
        var items = result.Items.Select(ToDto).ToArray();
        return Ok(new ApiResponse<object>(true, "Follow-ups retrieved.", new { items, result.Page, result.PageSize, result.TotalCount, result.PageCount }));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,SalesExecutive")]
    public async Task<IActionResult> Create(FollowUpWriteDto dto, CancellationToken cancellationToken)
    {
        var followUp = Map(dto);
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await followUpService.CreateAsync(followUp, UserId(), cancellationToken, User.IsInRole(RoleNames.Admin), canAssign);
        if (!result.Succeeded) return BadRequest(new ApiResponse<object>(false, result.Error!));
        if (!result.Succeeded) return BadRequest(new ApiResponse<object>(false, result.Error!));
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<FollowUpDto>(true, "Follow-up scheduled successfully.", ToDto(result.Data!)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, FollowUpWriteDto dto, CancellationToken cancellationToken)
    {
        ServiceResult<FollowUp> result;
        if (dto.Status is "Completed" or "Cancelled" or "Missed")
        {
            result = await followUpService.SetStatusAsync(id, dto.Status, UserId(), CanReadAll(), cancellationToken);
        }
        else
        {
            result = await followUpService.RescheduleAsync(id, dto.FollowUpDate, UserId(), CanReadAll(), cancellationToken);
        }
        if (!result.Succeeded)
        {
            if (result.Error == "Follow-up not found.") return NotFound(new ApiResponse<object>(false, result.Error));
            return BadRequest(new ApiResponse<object>(false, result.Error!));
        }
        return Ok(new ApiResponse<FollowUpDto>(true, "Follow-up updated successfully.", ToDto(result.Data!)));
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    private bool CanReadAll() => User.IsInRole(RoleNames.Admin);
    private static FollowUp Map(FollowUpWriteDto dto) => new() { CustomerId = dto.CustomerId, LeadId = dto.LeadId, OpportunityId = dto.OpportunityId, FollowUpDate = dto.FollowUpDate, FollowUpType = dto.FollowUpType, Subject = dto.Subject, Remarks = dto.Remarks, AssignedToId = dto.AssignedToId };
    private static FollowUpDto ToDto(FollowUp item) => new(item.Id, item.CustomerId, item.LeadId, item.OpportunityId, item.FollowUpDate, item.FollowUpType, item.Subject, item.Status, item.CompletedDate);
}
