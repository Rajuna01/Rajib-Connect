
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class LeadConversionViewModel
{
    public int LeadId { get; set; }

    public string LeadName { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    [Required(ErrorMessage = "Opportunity name is required.")]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "Opportunity amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Range(0, 100,
        ErrorMessage = "Probability must be between 0 and 100.")]
    public int Probability { get; set; } = 50;

    [Required]
    [DataType(DataType.Date)]
    public DateTime ExpectedCloseDate { get; set; } =
        DateTime.Today.AddDays(30);
}
