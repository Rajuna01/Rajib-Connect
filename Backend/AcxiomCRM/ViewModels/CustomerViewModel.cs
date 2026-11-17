
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels;

public class CustomerViewModel
{
    public int CustomerId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Customer Code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    [Display(Name = "Assigned Sales Executive")]
    public string? OwnerId { get; set; }

    public List<SelectListItem> SalesExecutives { get; set; } = new();
}
