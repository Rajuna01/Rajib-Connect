
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public class EditUserViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "User Role")]
    public string Role { get; set; } = string.Empty;
}
