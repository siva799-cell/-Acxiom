using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data = default, IDictionary<string, string[]>? Errors = null);

public sealed record CustomerDto(int Id, string CustomerCode, string CustomerName, string? Email, string? Phone,
    string? CompanyName, string Status, string? OwnerId, DateTime CreatedDate);

public sealed class CustomerWriteDto
{
    [Required, StringLength(150)] public string CustomerName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254)] public string? Email { get; set; }
    [StringLength(10, MinimumLength = 10, ErrorMessage = "Enter a valid phone number."), RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [StringLength(20)] public string Status { get; set; } = "Active";
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed record LeadDto(int Id, string LeadCode, string LeadName, string? CompanyName, string? Email,
    string? Phone, string Status, string Priority, decimal ExpectedValue, string? AssignedToId, DateTime CreatedDate);

public sealed class LeadWriteDto
{
    [Required, StringLength(150)] public string LeadName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254)] public string? Email { get; set; }
    [StringLength(10, MinimumLength = 10, ErrorMessage = "Enter a valid phone number."), RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(80)] public string? Source { get; set; }
    [StringLength(20)] public string Status { get; set; } = "New";
    [StringLength(20)] public string Priority { get; set; } = "Normal";
    public string? AssignedToId { get; set; }
    [Range(0, 999999999999)] public decimal ExpectedValue { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed record OpportunityDto(int Id, string OpportunityName, int? CustomerId, int? LeadId, decimal Amount,
    string Stage, int Probability, DateTime? ExpectedCloseDate, string Status, decimal WeightedAmount, string? AssignedToId);

public sealed class OpportunityWriteDto
{
    [Required, StringLength(150)] public string OpportunityName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999.99", ErrorMessage = "Opportunity Amount must be greater than 0.")] public decimal Amount { get; set; }
    [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")] public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    [StringLength(30)] public string Stage { get; set; } = "Qualification";
    public string? AssignedToId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed record FollowUpDto(int Id, int? CustomerId, int? LeadId, int? OpportunityId, DateTime FollowUpDate,
    string FollowUpType, string Subject, string Status, DateTime? CompletedDate);

public sealed class FollowUpWriteDto
{
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    [Required] public DateTime FollowUpDate { get; set; }
    [Required, StringLength(20)] public string FollowUpType { get; set; } = "Task";
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [StringLength(2000)] public string? Remarks { get; set; }
    [StringLength(20)] public string? Status { get; set; }
    public string? AssignedToId { get; set; }
}

public sealed class ApiLoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}
