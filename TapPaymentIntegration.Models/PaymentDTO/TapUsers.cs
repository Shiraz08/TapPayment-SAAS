using TapPaymentIntegration.Models.Card;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class TapUsers
    {
        public string @object { get; set; }
        public bool live_mode { get; set; }
        public string created { get; set; }
        public string merchant_id { get; set; }
        public string description { get; set; }
        public Metadata metadata { get; set; }
        public string currency { get; set; }
        public string id { get; set; }
        public string first_name { get; set; }
        public string middle_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
        public Phone phone { get; set; }
    }
}
