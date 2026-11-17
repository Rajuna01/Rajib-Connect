
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels.Api
{
    public class LoginApiRequest
    {
        [Required(ErrorMessage = "Username or email is required.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}
