using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text.Encodings.Web;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Data;
using TapPaymentIntegration.Models;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.UserDTO;
using static Google.Cloud.RecaptchaEnterprise.V1.AccountVerificationInfo.Types;

namespace TapPaymentIntegration.Controllers
{
    public class TapPaymentController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private TapPaymentIntegrationContext _context;
        private readonly IUserStore<ApplicationUser> _userStore;
        private IWebHostEnvironment _environment;
        private Task<ApplicationUser> GetCurrentUserAsync() => _userManager.GetUserAsync(HttpContext.User);
        public TapPaymentController(IWebHostEnvironment Environment, ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, TapPaymentIntegrationContext context, IUserStore<ApplicationUser> userStore)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
            _environment = Environment;
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
        public async Task<ActionResult> Deletecardinfo()
        {
            var userinfo = GetCurrentUserAsync().Result;
            DeleteCard Deserialized_savecard = null;
            if (!string.IsNullOrEmpty(userinfo.Tap_CustomerID))
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri("https://api.tap.company/v2/card/"+ userinfo.Tap_CustomerID+"/"+ userinfo.Tap_Card_ID),
                    Headers =
                              {
                                  { "accept", "application/json" },
                                  { "Authorization","Bearer " + userinfo.SecertKey },
                              },
                };
                using (var response = await client.SendAsync(request))
                {
                    var result = await response.Content.ReadAsStringAsync();
                    Deserialized_savecard = JsonConvert.DeserializeObject<DeleteCard>(result);
                }
            }

            if(Deserialized_savecard.delete == true)
            {

                var invoiceinfo = _context.invoices.Where(x => x.UserId == userinfo.Id).ToList().LastOrDefault();
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == invoiceinfo.SubscriptionId).FirstOrDefault();
                var applicationUser = _context.Users.Where(x => x.Id == invoiceinfo.UserId).FirstOrDefault();

                //Create Invoice
                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                decimal finalamount = 0;
                decimal Discount = 0;
                decimal Vat = 0;
                decimal VatwithoutSetupFee = 0;
                if (applicationUser.Frequency == "DAILY")
                {
                    Discount = 0;
                    finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                }
                else if (applicationUser.Frequency == "WEEKLY")
                {
                    Discount = 0;
                    finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                }
                else if (applicationUser.Frequency == "MONTHLY")
                {
                    Discount = 0;
                    finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                }
                else if (applicationUser.Frequency == "QUARTERLY")
                {
                    Discount = 0;
                    finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                }
                else if (applicationUser.Frequency == "HALFYEARLY")
                {
                    Discount = 0;
                    finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                }
                else if (applicationUser.Frequency == "YEARLY")
                {
                    decimal amountpercentage = (decimal.Parse(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
                    var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                    finalamount = decimal.Parse(subscriptions.Amount) * 12;
                    Discount = amountpercentage * 12;
                }
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    Vat = 0;
                }
                else
                {
                    decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                    Decimal finalTotal = 0;
                    if (Discount != 0)
                    {
                        finalTotal = Decimal.Subtract(totala, Discount);
                        Vat = CalculatePercentage(finalTotal, Convert.ToDecimal(subscriptions.VAT));
                    }
                    else
                    {
                        Vat = CalculatePercentage(totala, Convert.ToDecimal(subscriptions.VAT));
                    }

                    VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
                }
                decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;

                // Send Email
                string body = string.Empty;
                _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string contentRootPath = _environment.WebRootPath + "/htmltopdfP.html";
                string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                //Generate PDF
                using (StreamReader reader = new StreamReader(contentRootPath))
                {
                    body = reader.ReadToEnd();
                }
                //Fill EMail By Parameter
                body = body.Replace("{title}", "Tamarran Payment Invoice");
                body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                body = body.Replace("{InvocieStatus}", "Unpaid");
                body = body.Replace("{InvoiceID}", "Inv" + invoiceinfo.InvoiceId);

                body = body.Replace("{User_Name}", applicationUser.FullName);
                body = body.Replace("{User_Email}", applicationUser.Email);
                body = body.Replace("{User_GYM}", applicationUser.GYMName);
                body = body.Replace("{User_Phone}", applicationUser.PhoneNumber);

                var setupFree = Convert.ToDecimal(subscriptions.SetupFee).ToString("0.00");
                var discount = Convert.ToDecimal(Discount).ToString("0.00");

                body = body.Replace("{SubscriptionName}", subscriptions.Name);
                body = body.Replace("{Discount}", discount + " " + subscriptions.Currency);
                body = body.Replace("{SubscriptionPeriod}", applicationUser.Frequency);
                body = body.Replace("{SetupFee}", setupFree + " " + subscriptions.Currency);
                var amount = finalamount + Convert.ToDecimal(setupFree);
                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                //Calculate VAT
                var linkamount = "";
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                    Decimal finalTotal = 0;
                    if (Discount != 0)
                    {
                        finalTotal = Decimal.Subtract(after_vat_totalamount, Discount);
                        linkamount = finalTotal.ToString("0.00");
                        body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        linkamount = after_vat_totalamount.ToString("0.00");
                        body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = finalamount + Convert.ToDecimal(setupFree);
                    Decimal finalValueWithOutVAT = 0;
                    if (Discount != 0)
                    {
                        finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                        body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                    }

                }
                else
                {
                    body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                    Decimal finalTotal = 0;
                    if (Discount != 0)
                    {
                        finalTotal = Decimal.Subtract(after_vat_totalamount, Discount);
                        linkamount = finalTotal.ToString("0.00");
                        body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        linkamount = after_vat_totalamount.ToString("0.00");
                        body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    }

                    body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = finalamount + Convert.ToDecimal(setupFree);
                    Decimal finalValueWithOutVAT = 0;
                    if (Discount != 0)
                    {
                        finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                        body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                    }
                }
                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var callbackUrl = @Url.Action("SubscriptionAdmin", "Home", new { id = applicationUser.SubscribeID, link = "Yes", userid = applicationUser.Id, invoiceid = invoiceinfo.InvoiceId, After_vat_totalamount = linkamount, isfirstinvoice = "true" });
                var websiteurl = HtmlEncoder.Default.Encode(Constants.RedirectURL + callbackUrl);


                var subject = "Tamarran – Payment Request - " + " Inv" + invoiceinfo.InvoiceId.ToString();
                RecurringInvoiceInfo invoiceDetails = new RecurringInvoiceInfo
                {
                    InvoiceID = "Inv" + invoiceinfo.InvoiceId.ToString(),
                    UserName = applicationUser.FullName,
                    UserEmail = applicationUser.Email,
                    UserGYM = applicationUser.GYMName,
                    UserPhone = applicationUser.PhoneNumber,
                    SubscriptionName = subscriptions.Name,
                    Discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + subscriptions.Currency,
                    SubscriptionPeriod = applicationUser.Frequency,
                    SetupFee = Convert.ToDecimal(subscriptions.SetupFee).ToString("0.00") + " " + subscriptions.Currency,
                    SubscriptionAmount = finalamount.ToString("0.00") + " " + subscriptions.Currency,
                    VAT = (subscriptions.VAT == null || subscriptions.VAT == "0") ? "0.00" + " " + subscriptions.Currency : Vat.ToString("0.00") + " " + subscriptions.Currency,
                    Total = (subscriptions.VAT == null || subscriptions.VAT == "0") ?
                    (Discount != 0 ? Decimal.Subtract(after_vat_totalamount, Discount).ToString("0.00") : after_vat_totalamount.ToString("0.00")) + " " + subscriptions.Currency :
                    (Discount != 0 ? Decimal.Subtract(after_vat_totalamount, Discount).ToString("0.00") : after_vat_totalamount.ToString("0.00")) + " " + subscriptions.Currency,
                    InvoiceAmount = (subscriptions.VAT == null || subscriptions.VAT == "0") ?
                    amount.ToString("0.00") + " " + subscriptions.Currency :
                    after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency,
                    TotalInvoiceWithoutVAT = (subscriptions.VAT == null || subscriptions.VAT == "0") ?
                    (Discount != 0 ? Decimal.Subtract(finalamount + Convert.ToDecimal(setupFree), Discount).ToString("0.00") : (finalamount + Convert.ToDecimal(setupFree)).ToString("0.00")) + " " + subscriptions.Currency :
                    (Discount != 0 ? Decimal.Subtract(finalamount + Convert.ToDecimal(setupFree), Discount).ToString("0.00") : (finalamount + Convert.ToDecimal(setupFree)).ToString("0.00")) + " " + subscriptions.Currency,
                    InvoiceLink = websiteurl,
                    UserId = applicationUser.Id,
                    Subject = subject,
                    SubscriptionId = subscriptions.SubscriptionId,
                    InvoiceIds = invoiceinfo.InvoiceId,
                    InvoiceSendDate = (DateTime)invoiceinfo.InvoiceEndDate.AddDays(1)
                };
                _context.RecurringInvoiceInfo.Add(invoiceDetails);
                _context.SaveChanges();

                applicationUser.InvoiceSendDate = invoiceDetails.InvoiceSendDate;
                applicationUser.Tap_CustomerID =null;
                applicationUser.Tap_Agreement_ID = null;
                applicationUser.Tap_Card_ID = null;
                applicationUser.First_Six = null;
                applicationUser.Last_Four = null;
                applicationUser.PaymentSource = null;
                _context.Users.Update(applicationUser);
                _context.SaveChanges();

                var recurreningJobs = _context.recurringCharges.Where(x => x.UserID == userinfo.Id).ToList();
                foreach (var item in recurreningJobs)
                {
                    item.IsRun = true;
                    _context.Update(item);
                    _context.SaveChanges();
                }
            }
            return RedirectToAction("ChangeCard", "TapPayment");
        }
        public class DeleteCard
        {
            public bool deleted { get; set; }
            public string id { get; set; }
            public bool delete { get; set; }

        }
        public static decimal CalculatePercentage(decimal num, decimal percent)
        {
            return (num / 100) * percent;
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
