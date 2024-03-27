using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.Subscription
{
    public class UserSubscriptions
    {
        [Key]
        public int UserSubscriptionsId { get; set; }
        public string Userid { get; set; }
        public int SubID { get; set; }
    }
}
