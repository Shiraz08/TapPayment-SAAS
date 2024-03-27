//using TapPaymentIntegration.Models.Card;
//using TapPaymentIntegration.Models.PaymentDTO;

//namespace TapPaymentIntegration.Models.Subscription
//{
//    public class SubscriptionInfo
//    {
//        public string id { get; set; }
//        public string status { get; set; }
//        public string merchant_id { get; set; }
//        public Term term { get; set; }
//        public Trial trial { get; set; }
//        public Charge charge { get; set; }
//    }

//    public class Source
//    {
//        public string id { get; set; }
//    }

//    public class Term
//    {
//        public string interval { get; set; }
//        public int period { get; set; }
//        public DateTime from { get; set; }
//        public int due { get; set; }
//        public bool auto_renew { get; set; }
//        public string timezone { get; set; }
//    }

//    public class Trial
//    {
//        public int days { get; set; }
//        public int amount { get; set; }
//    }
//    public class Charge
//    {
//        public double amount { get; set; }
//        public string currency { get; set; }
//        public string description { get; set; }
//        public Reference reference { get; set; }
//        public Receipt receipt { get; set; }
//        public Customer customer { get; set; }
//        public Source source { get; set; }
//        public Post post { get; set; }
//        public string subscription_id { get; set; }
//    }

//    public class Customer
//    {
//        public string id { get; set; }
//    }

//    public class Post
//    {
//        public string url { get; set; }
//    }

//    public class Receipt
//    {
//        public bool email { get; set; }
//        public bool sms { get; set; }
//    }

//    public class Reference
//    {
//        public string transaction { get; set; }
//    }
//}
