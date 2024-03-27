namespace TapPaymentIntegration.Models.UserDTO
{
    public class UserInfoDTO
    {
        public string Id { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Tap_CustomerID { get; set; }
        public string UserType { get; set; }
        public string Email { get; set; }
        public string PaymentSource { get; set; }
        public string GYMName { get; set; }
        public string PhoneNumber { get; set; }
        public bool Status { get; set; }
        public string SubscribeName  { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Currency { get; set; }
        public string StepFee { get; set; } 
        public int SubscribeID { get; set; }
    }
}
