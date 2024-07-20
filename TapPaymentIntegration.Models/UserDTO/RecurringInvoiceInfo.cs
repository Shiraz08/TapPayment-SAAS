using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TapPaymentIntegration.Models.UserDTO
{
    public class RecurringInvoiceInfo
    {
        [Key]
        public int RecurringInvoiceInfoId { get; set; } 
        public string Title { get; set; } = "Tamarran Payment Invoice";
        public string CurrentDate { get; set; } = DateTime.UtcNow.ToString("dd-MM-yyyy");
        public string InvoiceStatus { get; set; } = "Unpaid"; 
        public string InvoiceID { get; set; }
        public DateTime InvoiceSendDate { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserGYM { get; set; }
        public string UserPhone { get; set; }
        public string SubscriptionName { get; set; }
        public string Discount { get; set; }
        public string SubscriptionPeriod { get; set; }
        public string SetupFee { get; set; }
        public string SubscriptionAmount { get; set; }
        public string VAT { get; set; }
        public string Total { get; set; }
        public string InvoiceAmount { get; set; }
        public string TotalInvoiceWithoutVAT { get; set; }
        public string InvoiceLink { get; set; }
        public string UserId { get; set; }
        public string Subject { get; set; }
        public int? InvoiceIds { get; set; }
        public int? SubscriptionId { get; set; }
    }
}
