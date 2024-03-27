using System;
using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.InvoiceDTO
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }
        public int ChargeResponseId { get; set; }
        public string ChargeId { get; set; } 
        public int SubscriptionId { get; set; }
        public string SubscriptionName { get; set; }
        public double SubscriptionAmount { get; set; } 
        public string UserId { get; set;}
        public string Status { get; set;}
        public string Currency { get; set;}
        public string VAT { get; set; }
        public string Discount { get; set; }
        public string InvoiceLink { get; set; }
        public string Description { get; set;}
        public bool IsDeleted { get; set; } 
        public string AddedBy { get; set; }
        public string GymName { get; set; }
        public string Country { get; set; }
        public DateTime AddedDate { get; set; }
        public string ModifiedBy { get; set; } 
        public DateTime ModifiedDate { get; set; }
        public DateTime InvoiceStartDate { get; set; }
        public DateTime InvoiceEndDate { get; set; }



    }
}
