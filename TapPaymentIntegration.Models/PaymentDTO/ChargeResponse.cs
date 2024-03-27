using System.ComponentModel.DataAnnotations;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class ChargeResponse
    {
        [Key]
        public int ChargeResponseId { get; set; }
        public string ChargeId { get; set; } 
        public string status { get; set; }
        public string UserId { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
    }
}
