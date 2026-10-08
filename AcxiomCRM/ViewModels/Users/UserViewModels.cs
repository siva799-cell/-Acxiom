using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Users;

public sealed class UserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string ManagerName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public int FailedAccessCount { get; init; }
}

public sealed class UserFormViewModel
{
    public string? Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = "SalesExecutive";
    public string? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
    [DataType(DataType.Password)] public string? Password { get; set; }
}
