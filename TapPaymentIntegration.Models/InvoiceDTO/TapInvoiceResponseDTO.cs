using System.Collections.Generic;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.PaymentDTO;
using TapPaymentIntegration.Models.Subscription;

namespace TapPaymentIntegration.Models.InvoiceDTO
{
    public class TapInvoiceResponseDTO
    {
        public string @object { get; set; }
        public bool live_mode { get; set; }
        public string api_version { get; set; }
        public string id { get; set; }
        public string method { get; set; }
        public string status { get; set; }
        public decimal amount { get; set; }
        public string currency { get; set; }
        public long created { get; set; }
        public long updated { get; set; }
        public string url { get; set; }
        public bool draft { get; set; }
        public long due { get; set; }
        public long expiry { get; set; }
        public string mode { get; set; }
        public string description { get; set; }
        public string description_id { get; set; }
        public string invoice_number { get; set; }
        public string frequency { get; set; }
        public string lang_code { get; set; }
        public Metadata metadata { get; set; }
        public Notifications notifications { get; set; }
        public Order order { get; set; }
        public Reference reference { get; set; }
        public Customer customer { get; set; }
        public Post post { get; set; }
        public Redirect redirect { get; set; }
        public Charge charge { get; set; }
        public Track track { get; set; }
        public List<string> payment_methods { get; set; }
        public List<string> currencies { get; set; }
        public bool savecard { get; set; }
        public string note { get; set; }
        public string merchant_id { get; set; }
        public Subscriptions Subscriptions { get; set; }
    }
}
