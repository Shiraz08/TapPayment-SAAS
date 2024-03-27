using TapPaymentIntegration.Models.Card;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class FillChargeModel
    {
        public string amount { get; set; }
        public string currency { get; set; }
        public bool customer_initiated { get; set; }
        public bool threeDSecure { get; set; }
        public bool save_card { get; set; }
        public Metadata metadata { get; set; }
        public Reference reference { get; set; }
        public Receipt receipt { get; set; }
        public Customer customer { get; set; }
        public Source source { get; set; }
        public Merchant merchant { get; set; }
        public Post post { get; set; }
        public Redirect redirect { get; set; }
    }
}
