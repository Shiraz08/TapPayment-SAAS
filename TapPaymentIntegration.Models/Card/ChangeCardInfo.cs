using System;
using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.Card
{
    public class ChangeCardInfo
    {
        [Key]
        public int ChangeCardId { get; set; }
        public string OldCardName { get; set; }
        public string NewCardName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string SubscriptionName { get; set; }
        public DateTime ChangeCardDate { get; set; }
    }
}
