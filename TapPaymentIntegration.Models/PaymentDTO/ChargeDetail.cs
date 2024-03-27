using System.Collections.Generic;
using TapPaymentIntegration.Models.PaymentDTO;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.InvoiceDTO;
using TapPaymentIntegration.Models.Subscription;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class ChargeDetail
    {
        public string id { get; set; }
        public string @object { get; set; }
        public bool live_mode { get; set; }
        public bool customer_initiated { get; set; }
        public string api_version { get; set; }
        public string method { get; set; }
        public string status { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
        public bool threeDSecure { get; set; }
        public bool card_threeDSecure { get; set; }
        public bool save_card { get; set; }
        public string merchant_id { get; set; }
        public string product { get; set; }
        public string statement_descriptor { get; set; }
        public string description { get; set; }
        public Metadata metadata { get; set; }
        public Order order { get; set; }
        public Transaction transaction { get; set; }
        public Reference reference { get; set; }
        public Response response { get; set; }
        public Security security { get; set; }
        public Acquirer acquirer { get; set; }
        public Gateway gateway { get; set; }
        public Card card { get; set; }
        public Receipt receipt { get; set; }
        public Customer customer { get; set; }
        public Merchant merchant { get; set; }
        public Source source { get; set; }
        public Redirect redirect { get; set; }
        public Post post { get; set; }
        public List<Activity> activities { get; set; }
        public bool auto_reversed { get; set; }
        public PaymentAgreement payment_agreement { get; set; }
        public Subscriptions Subscriptions { get; set; }
        public string Frequency { get; set; }
        public string finalamount { get; set; }
        public string VAT { get; set; }
        public string InvoiceID { get; set; }
        public string Paymentname { get; set; }
        public long Created_date { get; set; }


    }
    public class Security
    {
        public ThreeDSecure threeDSecure { get; set; }
    }

    public class ThreeDSecure
    {
        public string id { get; set; }
        public string status { get; set; }
    }
    public class Acquirer
    {
        public Response response { get; set; }
    }


    public class Contract
    {
        public string id { get; set; }
        public string customer_id { get; set; }
        public string type { get; set; }
    }



    public class Gateway
    {
        public Response response { get; set; }
    }

    public class PaymentAgreement
    {
        public string id { get; set; }
        public string type { get; set; }
        public int total_payments_count { get; set; }
        public Contract contract { get; set; }
        public Metadata metadata { get; set; }
    }

}
