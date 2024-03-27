namespace TapPaymentIntegration.Models.Card
{
    public class RetriveCardResponse
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
        public PaymentAgreement payment_agreement { get; set; }
    }

    public class Card
    {
        public string id { get; set; }
        public string @object { get; set; }
        public string first_six { get; set; }
        public string scheme { get; set; }
        public string brand { get; set; }
        public string last_four { get; set; }
    }

    public class CardIssuer
    {
        public string id { get; set; }
        public string name { get; set; }
        public string country { get; set; }
    }

    public class Contract
    {
        public string id { get; set; }
        public string customer_id { get; set; }
        public string type { get; set; }
    }


    public class Expiry
    {
        public int period { get; set; }
        public string type { get; set; }
    }

    public class Metadata
    {
        public string udf1 { get; set; }
        public string udf2 { get; set; }
        public string txn_type { get; set; }
        public string txn_id { get; set; }
        public string terminal_id { get; set; }
    }

    public class PaymentAgreement
    {
        public string id { get; set; }
        public string type { get; set; }
        public int total_payments_count { get; set; }
        public Contract contract { get; set; }
        public Metadata metadata { get; set; }
    }

    public class Phone
    {
        public string country_code { get; set; }
        public string number { get; set; }
    }

    public class Redirect
    {
        public string status { get; set; }
        public string url { get; set; }
    }

    public class Response
    {
        public string code { get; set; }
        public string message { get; set; }
    }


    public class Source
    {
        public string @object { get; set; }
        public string type { get; set; }
        public string payment_type { get; set; }
        public string payment_method { get; set; }
        public string channel { get; set; }
        public string id { get; set; }
    }

    public partial class Transaction
    {
        public string authorization_id { get; set; }
        public string timezone { get; set; }
        public string created { get; set; }
        public Expiry expiry { get; set; }
        public bool asynchronous { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
    }

}
