using System;
using System.Collections.Generic;

namespace TapPaymentIntegration.Models.PaymentDTO
{
    public class ChargeListDTO
    {
        public string ChargeId { get; set; }
        public string UserId { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
        public string FullName { get; set; }
        public string GYMName { get; set; }
        public string Email { get; set; }
        public string status { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Tap_CustomerID { get; set; }
        public DateTime PaymentDate { get; set; }
    }
    public class ChargeDetailDTO
    {
        public string ChargeId { get; set; }
        public string UserId { get; set; }
        public double amount { get; set; }
        public string currency { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string status { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Tap_CustomerID { get; set; }
        public List<Activity> activities { get; set; }
    }
}
