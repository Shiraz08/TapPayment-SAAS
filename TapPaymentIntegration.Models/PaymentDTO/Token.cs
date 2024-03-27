namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class Token
    {
        public string id { get; set; }
        public long created { get; set; }
        public string @object { get; set; }
        public bool live_mode { get; set; }
        public string type { get; set; }
        public string source { get; set; }
        public bool used { get; set; }
        public Card card { get; set; }
    }
    public class Card
    {
        public string id { get; set; }
        public string @object { get; set; }
        public string funding { get; set; }
        public string fingerprint { get; set; }
        public string brand { get; set; }
        public string scheme { get; set; }
        public string category { get; set; }
        public int exp_month { get; set; }
        public int exp_year { get; set; }
        public string last_four { get; set; }
        public string first_six { get; set; }
        public string name { get; set; }
        public Issuer issuer { get; set; }
    }

    public class Issuer
    {
        public string bank { get; set; }
        public string country { get; set; }
        public string id { get; set; }
    }
}
