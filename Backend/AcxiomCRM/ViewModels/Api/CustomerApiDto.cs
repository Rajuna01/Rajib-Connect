
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class CustomerApiRequest
    {
        [Required]
        [StringLength(100)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";

        // Used by Admin and Manager.
        // Sales Executives cannot change ownership.
        public string? OwnerId { get; set; }
    }

    public class CustomerApiResponse
    {
        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? OwnerId { get; set; }

        public string OwnerName { get; set; } = "Unassigned";

        public DateTime CreatedDate { get; set; }

        public DateTime? ModifiedDate { get; set; }
    }
}
