using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Customer
{
    public int Id { get; set; }
    [Required, StringLength(32)] public string CustomerCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string CustomerName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254)] public string? Email { get; set; }
    [StringLength(10, MinimumLength = 10, ErrorMessage = "Enter a valid phone number."), RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(250)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? State { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "Active";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? CreatedById { get; set; }
    public DateTime? ModifiedDate { get; set; }
    [StringLength(450)] public string? ModifiedById { get; set; }
    [StringLength(450)] public string? OwnerId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public class Lead
{
    public int Id { get; set; }
    [Required, StringLength(32)] public string LeadCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string LeadName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(254)] public string? Email { get; set; }
    [StringLength(10, MinimumLength = 10, ErrorMessage = "Enter a valid phone number."), RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a valid phone number.")] public string? Phone { get; set; }
    [StringLength(150)] public string? CompanyName { get; set; }
    [StringLength(80)] public string? Source { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "New";
    [Required, StringLength(20)] public string Priority { get; set; } = "Normal";
    [Range(0, 999999999999)] public decimal ExpectedValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? CreatedById { get; set; }
    [StringLength(450)] public string? AssignedToId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public int? ConvertedOpportunityId { get; set; }
}

public class Opportunity
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string OpportunityName { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    [Range(0, 999999999999)] public decimal Amount { get; set; }
    [Required, StringLength(30)] public string Stage { get; set; } = "Qualification";
    [Range(0, 100)] public int Probability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "Open";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? CreatedById { get; set; }
    [StringLength(450)] public string? AssignedToId { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public decimal WeightedAmount => Amount * Probability / 100m;
}

public class FollowUp
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public DateTime FollowUpDate { get; set; }
    [Required, StringLength(20)] public string FollowUpType { get; set; } = "Task";
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [StringLength(2000)] public string? Remarks { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "Planned";
    [StringLength(450)] public string? AssignedToId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }
}

public class CrmActivity
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string ActivityType { get; set; } = "Task";
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    [StringLength(450)] public string? AssignedToId { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = "Open";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(450)] public string? CreatedById { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    [StringLength(450)] public string? UserId { get; set; }
    [Required, StringLength(80)] public string Action { get; set; } = string.Empty;
    [StringLength(80)] public string? EntityName { get; set; }
    [StringLength(100)] public string? RecordId { get; set; }
    [StringLength(4000)] public string? OldValue { get; set; }
    [StringLength(4000)] public string? NewValue { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [StringLength(45)] public string? IpAddress { get; set; }
    [StringLength(40)] public string? Module { get; set; }
    [Required, StringLength(20)] public string Result { get; set; } = "Success";
    [StringLength(1000)] public string? Details { get; set; }
}
