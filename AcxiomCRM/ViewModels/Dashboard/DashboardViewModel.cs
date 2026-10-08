namespace AcxiomCRM.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int TotalCustomers { get; init; }
    public int TotalLeads { get; init; }
    public int OpenLeads { get; init; }
    public int TotalOpportunities { get; init; }
    public int OpenOpportunities { get; init; }
    public int WonOpportunities { get; init; }
    public int LostOpportunities { get; init; }
    public decimal PipelineValue { get; init; }
    public int UpcomingFollowUps { get; init; }
    public int OverdueFollowUps { get; init; }
}
