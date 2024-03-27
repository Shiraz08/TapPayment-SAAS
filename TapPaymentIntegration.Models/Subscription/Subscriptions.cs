using System;
using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.Subscription
{
    public class Subscriptions
    {
        [Key]
        public int SubscriptionId { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Currency { get; set; }
        public string Frequency { get; set; }
        [Required]
        public string Countries { get; set; }
        [Required]
        public string SetupFee { get; set; }
        [Required]
        public string Amount { get; set; }
        public string VAT { get; set; } 
        public string Discount { get; set; } 
        public bool Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
