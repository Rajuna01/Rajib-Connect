
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class CreateUserViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "User Role")]
    public string Role { get; set; } = "SalesExecutive";
}
