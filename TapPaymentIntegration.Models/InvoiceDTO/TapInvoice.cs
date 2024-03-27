using System.Collections.Generic;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.PaymentDTO;

namespace TapPaymentIntegration.Models.InvoiceDTO
{
    public class TapInvoice
    {
        public bool draft { get; set; }
        public long due { get; set; }
        public long expiry { get; set; }
        public string description { get; set; }
        public string mode { get; set; }
        public string note { get; set; }
        public Notifications notifications { get; set; }
        public List<string> currencies { get; set; }
        public Charge charge { get; set; }
        public Customer customer { get; set; }
        public Order order { get; set; }
        public List<string> payment_methods { get; set; }
        public Post post { get; set; }
        public Redirect redirect { get; set; }
        public Reference reference { get; set; }
    }
    public class Shipping
    {
        public int amount { get; set; }
        public string currency { get; set; }
    }

    public class Tax
    {
        public string description { get; set; }
        public string name { get; set; }
        public Rate rate { get; set; }
    }

    public class Charge
    {
        public Receipt receipt { get; set; }
        public string statement_descriptor { get; set; }
    }

    public class Discount
    {
        public string type { get; set; }
        public int value { get; set; }
    }

    public class Item
    {
        public string amount { get; set; }
        public string currency { get; set; }
        public string description { get; set; }
        public Discount discount { get; set; }
        public string image { get; set; }
        public string name { get; set; }
        public int quantity { get; set; }
    }

    public class Notifications
    {
        public List<string> channels { get; set; }
        public bool dispatch { get; set; }
    }

    public class Order
    {
        public string amount { get; set; }
        public string currency { get; set; }
        public List<Item> items { get; set; }
    }



    public class Rate
    {
        public string type { get; set; }
        public int value { get; set; }
    }

}
