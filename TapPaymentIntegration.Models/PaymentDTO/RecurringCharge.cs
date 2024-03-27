using System;
using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class RecurringCharge
    {
        [Key]
        public int RecurringChargeId { get; set; }
        public string ChargeId { get; set; } 
        public DateTime JobRunDate  { get; set; }
        public string Tap_CustomerId { get; set; }
        public string UserID { get; set; }
        public string Invoice { get; set; }
        public bool IsRun { get; set; } 
        public int SubscriptionId { get; set; }
        public Decimal Amount { get; set; }



    }
}
