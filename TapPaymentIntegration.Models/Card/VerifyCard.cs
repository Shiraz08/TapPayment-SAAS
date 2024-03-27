using TapPaymentIntegration.Models.PaymentDTO;

namespace TapPaymentIntegration.Models.Card
{
    public class VerifyCard
    {
        public string id { get; set; }
        public string @object { get; set; }
        public bool live_mode { get; set; }
        public string api_version { get; set; }
        public string status { get; set; }
        public string currency { get; set; }
        public bool threeDSecure { get; set; }
        public bool save_card { get; set; }
        public Metadata metadata { get; set; }
        public Transaction transaction { get; set; }
        public Customer customer { get; set; }
        public Source source { get; set; }
        public Redirect redirect { get; set; }
        public Card card { get; set; }
        public Response response { get; set; }
        public bool risk { get; set; }
        public bool issuer { get; set; }
        public bool promo { get; set; }
        public bool loyalty { get; set; }
        public CardIssuer card_issuer { get; set; }
    }

    public partial class Transaction
    {
        public string url { get; set; }
    }



    public class Customer
    {
        public string id  { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string email { get; set; }
        public Phone phone { get; set; }
    }
}
