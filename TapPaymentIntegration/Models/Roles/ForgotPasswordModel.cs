using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.Roles
{
    public class ForgotPasswordModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
