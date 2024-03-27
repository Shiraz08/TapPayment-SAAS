using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Data;
using TapPaymentIntegration.Models;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.UserDTO;

namespace TapPaymentIntegration.Controllers
{
    public class TapPaymentController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private TapPaymentIntegrationContext _context;
        private readonly IUserStore<ApplicationUser> _userStore;
        private Task<ApplicationUser> GetCurrentUserAsync() => _userManager.GetUserAsync(HttpContext.User);
        public TapPaymentController(ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, TapPaymentIntegrationContext context, IUserStore<ApplicationUser> userStore)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _userStore = userStore;
        }
        //Change Card
        public ActionResult ChangeCard()
        {
            return View();
        }
        [HttpPost]
        public async Task<ActionResult> ChangeCard(string id)
        {
            var Token = Request.Form.Where(x => x.Key == "Token").FirstOrDefault().Value.ToString();
            if (Token != null)
            {
                //Get Loggedin User Info
                var userinfo = GetCurrentUserAsync().Result;
                var subinfo = _context.subscriptions.Where(x => x.SubscriptionId == userinfo.SubscribeID).FirstOrDefault();
                var countrycode = "";
                if (userinfo.Country == "Bahrain")
                {
                    countrycode = "+973";
                }
                else if (userinfo.Country == "KSA")
                {
                    countrycode = "+966";
                }
                else if (userinfo.Country == "Kuwait")
                {
                    countrycode = "+965";
                }
                else if (userinfo.Country == "UAE")
                {
                    countrycode = "+971";
                }
                else if (userinfo.Country == "Qatar")
                {
                    countrycode = "+974";
                }
                else if (userinfo.Country == "Oman")
                {
                    countrycode = "+968";
                }
                else
                {
                    countrycode = "966";
                }
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/card/verify");
                request.Headers.Add("authorization", "Bearer " + userinfo.SecertKey);
                request.Headers.Add("accept", "application/json");
                var complete_url = Constants.RedirectURL + "TapPayment/ChangeCardResponse";
                var content = new StringContent("{\"currency\":\""+ subinfo.Currency + "\",\"threeDSecure\":true,\"save_card\":true,\"metadata\":{\"udf1\":\"test1\",\"udf2\":\"test2\"},\"customer\":{\"first_name\":\""+userinfo.FullName+"\",\"middle_name\":\"\",\"last_name\":\"\",\"email\":\""+ userinfo.Email+ "\",\"phone\":{\"country_code\":\""+ countrycode + "\",\"number\":\""+userinfo.PhoneNumber+"\"}},\"source\":{\"id\":\""+ Token + "\"},\"redirect\":{\"url\":\""+ complete_url + "\"}}", null, "application/json");
                request.Content = content;
                var response = await client.SendAsync(request);
                var result_erify = await response.Content.ReadAsStringAsync();
                var Deserialized_Deserialized_savecard = JsonConvert.DeserializeObject<VerifyCard>(result_erify);
                HttpContext.Session.SetString("Verify_id", Deserialized_Deserialized_savecard.id);
                return Json(Deserialized_Deserialized_savecard.transaction.url);
            }
            return Json(false);
        }
        public async Task<ActionResult> ChangeCardResponse()
        {
            var userinfo = GetCurrentUserAsync().Result;
            var subinfo = _context.subscriptions.Where(x => x.SubscriptionId == userinfo.SubscribeID).FirstOrDefault();
            var verify_id = HttpContext.Session.GetString("Verify_id");
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tap.company/v2/card/verify/" + verify_id);
            request.Headers.Add("authorization", "Bearer " + userinfo.SecertKey);
            var collection = new List<KeyValuePair<string, string>>();
            collection.Add(new("{}", ""));
            var content = new FormUrlEncodedContent(collection);
            request.Content = content;
            var response = await client.SendAsync(request);
            var result  = await response.Content.ReadAsStringAsync();
            var Deserialized_savecard = JsonConvert.DeserializeObject<RetriveCardResponse>(result);
            //Add Change Card Info
            ChangeCardInfo changeCardInfo = new ChangeCardInfo();
            changeCardInfo.Email = userinfo.Email;
            changeCardInfo.ChangeCardDate = DateTime.UtcNow;
            changeCardInfo.UserName = userinfo.FullName;
            changeCardInfo.SubscriptionName = subinfo.Name;
            changeCardInfo.OldCardName = userinfo.PaymentSource;
            changeCardInfo.NewCardName = Deserialized_savecard.source.payment_method;
            _context.changeCardInfos.Add(changeCardInfo);
            _context.SaveChanges();
            //update user 
            userinfo.Tap_CustomerID = Deserialized_savecard.payment_agreement.contract.customer_id;
            userinfo.Tap_Card_ID = Deserialized_savecard.payment_agreement.contract.id;
            userinfo.Tap_Agreement_ID = Deserialized_savecard.payment_agreement.id;
            userinfo.PaymentSource = Deserialized_savecard.source.payment_method;
            _context.Users.Update(userinfo);
            _context.SaveChanges();
            return View();
        }
        public ActionResult ViewChangeCardInfo()
        {
            var users = _context.changeCardInfos.ToList();
            ViewBag.Userinfo = _context.Users.ToList();
            return View(users);
        }
    }
}
