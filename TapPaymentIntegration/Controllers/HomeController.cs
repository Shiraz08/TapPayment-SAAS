using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Data;
using TapPaymentIntegration.Models.Email;
using TapPaymentIntegration.Models.InvoiceDTO;
using TapPaymentIntegration.Models.PaymentDTO;
using TapPaymentIntegration.Models.UserDTO;
using TapPaymentIntegration.Models.Card;
using ApplicationUser = TapPaymentIntegration.Areas.Identity.Data.ApplicationUser;
using System.Text.Encodings.Web;
using Order = TapPaymentIntegration.Models.InvoiceDTO.Order;
using System.Net.Http.Headers;
using TapPaymentIntegration.Models.Subscription;
using TapPaymentIntegration.Utility;
using TapPaymentIntegration.Models;

namespace TapPaymentIntegration.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private TapPaymentIntegrationContext _context;
        private readonly IUserStore<ApplicationUser> _userStore;
        private IWebHostEnvironment _environment;
        private Task<ApplicationUser> GetCurrentUserAsync() => _userManager.GetUserAsync(HttpContext.User);
        EmailSender _emailSender = new EmailSender();

        public HomeController(IWebHostEnvironment Environment, ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, TapPaymentIntegrationContext context, IUserStore<ApplicationUser> userStore)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _userStore = userStore;
            _environment = Environment;
        }

        #region Website 
        public IActionResult Index(string id, string userid)
        {
            //var applicationUser = GetCurrentUserAsync().Result;
            //var sub = _context.subscriptions.FirstOrDefault();

            //var bodyemail1 = EmailBodyFill.EmailBodyForPaymentRequest(applicationUser, "jdsjdjdsjgjdsgsdjs");
            //_ = _emailSender.SendEmailAsync("shiraznaseer786@gmail.com", "Tamarran – Payment Request", bodyemail1);

            //var bodyemail2 = EmailBodyFill.EmailBodyForPaymentReceipt(applicationUser);
            //_ = _emailSender.SendEmailAsync("shiraznaseer786@gmail.com", "Tamarran – Payment Receipt", bodyemail2);

            //var bodyemail3 = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(sub, applicationUser);
            //_ = _emailSender.SendEmailAsync("shiraznaseer786@gmail.com", "Tamarran - Automatic Payment Confirmation", bodyemail3);

            //var bodyemail4 = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(sub, applicationUser);
            //_ = _emailSender.SendEmailAsync("shiraznaseer786@gmail.com", "Tamarran - Your subscription renewal in Tamarran failed", bodyemail4);


            //var bodyemail5 = EmailBodyFill.EmailBodyForUnsubscriptionSuccessful(applicationUser);
            //_ = _emailSender.SendEmailAsync("shiraznaseer786@gmail.com", "Un-subscription Successful - You're Always Welcome Back!", bodyemail5);


            var subscriptions = _context.subscriptions.Where(x => x.Status).ToList();
            return View(subscriptions);
        }
        public async Task<IActionResult> Subscription(int id, string link, string userid, string invoiceid)
        {
            if (userid != null)
            {
                var applicationUser = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                await _signInManager.PasswordSignInAsync(applicationUser.UserName.ToString(), applicationUser.Password, true, lockoutOnFailure: true);
            }
            var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == id).FirstOrDefault();
            if (subscriptions.VAT == null || subscriptions.VAT == "0")
            {
                subscriptions.VAT = "0";
            }
            ViewBag.Invoiceid = invoiceid;
            return View(subscriptions);
        }
        public async Task<IActionResult> SubscriptionAdmin(int id, string link, string userid, string invoiceid, string After_vat_totalamount)
        {
            if (link != null)
            {
                var applicationUser = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                await _signInManager.PasswordSignInAsync(applicationUser.UserName.ToString(), applicationUser.Password, true, lockoutOnFailure: true);
            }
            var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == id).FirstOrDefault();
            if (subscriptions is null)
            {
                TempData["ErrorMessage"] = Constants.SubscriptionErrorMessage;
                return View();
            }
            var users = _context.Users.Where(x => x.Status == true && x.SubscribeID == id).FirstOrDefault();
            if (subscriptions.VAT == null || subscriptions.VAT == "0")
            {
                subscriptions.VAT = "0";
            }
            ViewBag.Frequency = users.Frequency.ToString();
            ViewBag.Invoiceid = invoiceid;
            ViewBag.After_vat_totalamount = After_vat_totalamount;
            ViewBag.userid = userid;
            ViewBag.PublicKey = users.PublicKey;
            ViewBag.RedirectURL = Constants.RedirectURL;
            return View(subscriptions);
        }
        public async Task<IActionResult> Logout()
        {
            var returnUrl = Url.Action("Index", "Home");
            await _signInManager.SignOutAsync();
            return LocalRedirect(returnUrl);
        }
        #endregion
        #region Tap Charge API
        [HttpPost]
        public async Task<IActionResult> CreateInvoiceMada()
        {
            try
            {
                var Currenturl = Request.Form.Where(x => x.Key == "Currenturl").FirstOrDefault().Value.ToString();
                HttpContext.Session.SetString("Currenturl", Currenturl);
                var Userid = Request.Form.Where(x => x.Key == "Userid").FirstOrDefault().Value.ToString();
                var Invoiceid = Request.Form.Where(x => x.Key == "Invoiceid").FirstOrDefault().Value.ToString();
                ApplicationUser usersinfo = null;
                Invoice invoice = null;
                var Frequency = "";
                var VAT = "";
                if (Userid != "")
                {
                    usersinfo = _context.Users.Where(x => x.Id == Userid).FirstOrDefault();
                    invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                    VAT = invoice.VAT;
                    Frequency = usersinfo.Frequency;
                }
                else
                {
                    Frequency = Request.Form.Where(x => x.Key == "Frequency").FirstOrDefault().Value.ToString();
                    VAT = Request.Form.Where(x => x.Key == "VAT").FirstOrDefault().Value.ToString();
                }
                var TotalPlanfee = Request.Form.Where(x => x.Key == "TotalPlanfee").FirstOrDefault().Value.ToString();
                var SubscriptionId = Request.Form.Where(x => x.Key == "SubscriptionId").FirstOrDefault().Value.ToString();
                var Token = Request.Form.Where(x => x.Key == "Token").FirstOrDefault().Value.ToString();
                if (SubscriptionId != null && Frequency != null)
                {
                    var userinfo = _context.Users.Where(x => x.Id == GetCurrentUserAsync().Result.Id).FirstOrDefault();
                    var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                    Random rnd = new Random();
                    var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                    var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                    var amount = TotalPlanfee.ToString();
                    var description = subscriptions.Frequency;
                    Reference reference = new Reference();
                    reference.transaction = TransNo;
                    reference.order = OrderNo;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerify";

                    Post post = new Post();
                    post.url = Constants.RedirectURL + "/Home/CardVerifyurl";

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
                    var currency = subscriptions.Currency;
                    Phone phone = new Phone();
                    phone.number = userinfo.PhoneNumber;
                    phone.country_code = countrycode;

                    Customer customer = new Customer();
                    customer.first_name = userinfo.FullName;
                    customer.email = userinfo.Email;
                    customer.phone = phone;

                    Receipt receipt = new Receipt();
                    receipt.sms = true;
                    receipt.email = true;

                    Metadata metadata = new Metadata();
                    metadata.udf1 = "Metadata 1";

                    Source source = new Source();
                    source.id = Token;

                    Merchant merchant = new Merchant();
                    merchant.id = userinfo.MarchantID;

                    FillChargeModel fillChargeModel = new FillChargeModel();
                    fillChargeModel.threeDSecure = true;
                    fillChargeModel.amount = amount;
                    fillChargeModel.save_card = true;
                    fillChargeModel.currency = currency;
                    fillChargeModel.redirect = redirect;
                    fillChargeModel.post = post;
                    fillChargeModel.customer = customer;
                    fillChargeModel.metadata = metadata;
                    fillChargeModel.reference = reference;
                    fillChargeModel.receipt = receipt;
                    fillChargeModel.source = source;
                    fillChargeModel.merchant = merchant;
                    fillChargeModel.customer_initiated = true;
                    var jsonmodel = JsonConvert.SerializeObject(fillChargeModel);
                    var client_charge = new HttpClient();
                    var request_charge = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                    request_charge.Headers.Add("Authorization", "Bearer " + userinfo.SecertKey);
                    request_charge.Headers.Add("accept", "application/json");
                    var content_charge = new StringContent(jsonmodel, null, "application/json");
                    request_charge.Content = content_charge;
                    var response_charge = await client_charge.SendAsync(request_charge);
                    var body = await response_charge.Content.ReadAsStringAsync();
                    CreateCharge deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(body);
                    if (deserialized_CreateCharge.status == "INITIATED")
                    {
                        HttpContext.Session.SetString("SubscriptionId", SubscriptionId);
                        HttpContext.Session.SetString("Frequency", Frequency);
                        HttpContext.Session.SetString("Token", Token);
                        HttpContext.Session.SetString("Invoiceid", Invoiceid);
                        ChargeResponse chargeResponse = new ChargeResponse
                        {
                            UserId = userinfo.Id,
                            ChargeId = deserialized_CreateCharge.id,
                            amount = deserialized_CreateCharge.amount,
                            currency = currency,
                            status = deserialized_CreateCharge.status,
                        };
                        _context.chargeResponses.Add(chargeResponse);
                        _context.SaveChanges();
                        //update user 
                        userinfo.Tap_CustomerID = deserialized_CreateCharge.customer.id;
                        userinfo.Frequency = Frequency;
                        userinfo.VAT = VAT;
                        _context.Users.Update(userinfo);
                        _context.SaveChanges();
                        return Json(new { status = true, URL = deserialized_CreateCharge.transaction.url });
                    }
                    else
                    {
                        return Json(new { status = false, URL = deserialized_CreateCharge.response.message });
                    }
                }
                return Json(false);
            }
            catch (Exception e)
            {

                throw;
            }
        }
        [HttpPost]
        public async Task<IActionResult> CreateInvoiceBenefit()
        {
            try
            {
                var Currenturl = Request.Form.Where(x => x.Key == "Currenturl").FirstOrDefault().Value.ToString();
                HttpContext.Session.SetString("Currenturl", Currenturl);
                var Userid = Request.Form.Where(x => x.Key == "Userid").FirstOrDefault().Value.ToString();
                var Invoiceid = Request.Form.Where(x => x.Key == "Invoiceid").FirstOrDefault().Value.ToString();
                ApplicationUser usersinfo = null;
                Invoice invoice = null;
                var Frequency = "";
                var VAT = "";
                if (Userid != "")
                {
                    usersinfo = _context.Users.Where(x => x.Id == Userid).FirstOrDefault();
                    invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                    VAT = invoice.VAT;
                    Frequency = usersinfo.Frequency;
                }
                else
                {
                    Frequency = Request.Form.Where(x => x.Key == "Frequency").FirstOrDefault().Value.ToString();
                    VAT = Request.Form.Where(x => x.Key == "VAT").FirstOrDefault().Value.ToString();
                }
                var TotalPlanfee = Request.Form.Where(x => x.Key == "TotalPlanfee").FirstOrDefault().Value.ToString();
                var SubscriptionId = Request.Form.Where(x => x.Key == "SubscriptionId").FirstOrDefault().Value.ToString();
                var Token = Request.Form.Where(x => x.Key == "Token").FirstOrDefault().Value.ToString();
                if (SubscriptionId != null && Frequency != null)
                {
                    var userinfo = _context.Users.Where(x => x.Id == GetCurrentUserAsync().Result.Id).FirstOrDefault();
                    var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                    Random rnd = new Random();
                    var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                    var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                    var description = subscriptions.Frequency;
                    Reference reference = new Reference();
                    reference.transaction = TransNo;
                    reference.order = OrderNo;

                    long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                    long Due = 0;
                    int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                    decimal finalamount = 0;
                    decimal Discount = 0;
                    decimal Vat = 0;

                    if (Frequency == "DAILY")
                    {
                        Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                    }
                    else if (Frequency == "WEEKLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                        Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                    }
                    else if (Frequency == "MONTHLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                        Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                    }
                    else if (Frequency == "QUARTERLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                        Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                    }
                    else if (Frequency == "HALFYEARLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                        Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                    }
                    else if (Frequency == "YEARLY")
                    {
                        var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * 10;
                        var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                        finalamount = final_amount_percentage * 12;
                        Discount = amountpercentage * 12;
                        Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                    }
                    if (subscriptions.VAT == null || subscriptions.VAT == "0")
                    {
                        Vat = 0;
                    }
                    else
                    {
                        decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                        Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                    }
                    decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit";

                    Post post = new Post();
                    post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

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
                    var currency = subscriptions.Currency;
                    Phone phone = new Phone();
                    phone.number = userinfo.PhoneNumber;
                    phone.country_code = countrycode;

                    Customer customer = new Customer();
                    customer.first_name = userinfo.FullName;
                    customer.email = userinfo.Email;
                    customer.phone = phone;

                    Receipt receipt = new Receipt();
                    receipt.sms = true;
                    receipt.email = true;

                    Notifications notifications = new Notifications();
                    List<string> receipts = new List<string>();
                    receipts.Add("SMS");
                    receipts.Add("EMAIL");
                    notifications.channels = receipts;
                    notifications.dispatch = true;

                    List<string> currencies = new List<string>();
                    currencies.Add(subscriptions.Currency);

                    Charge charge = new Charge();
                    charge.receipt = receipt;
                    charge.statement_descriptor = Invoiceid.ToString();

                    List<string> p_methods = new List<string>();
                    p_methods.Add(Token);

                    List<Item> items = new List<Item>();
                    Item item = new Item();
                    item.image = "";
                    item.quantity = 1;
                    item.name = "Invoice Amount";
                    item.amount = Math.Round(after_vat_totalamount, 2).ToString("0.00");
                    item.currency = subscriptions.Currency;
                    items.Add(item);

                    Order order = new Order();
                    order.amount = Math.Round(after_vat_totalamount, 2).ToString("0.00");
                    order.currency = subscriptions.Currency;
                    order.items = items;


                    TapInvoice tapInvoice = new TapInvoice();
                    tapInvoice.redirect = redirect;
                    tapInvoice.post = post;
                    tapInvoice.customer = customer;
                    tapInvoice.draft = false;
                    tapInvoice.due = Due;
                    tapInvoice.expiry = ExpireLink;
                    tapInvoice.description = "Invoice Create - Frequency(" + Frequency + ")";
                    tapInvoice.mode = "INVOICE";
                    tapInvoice.note = "Invoice Create - Frequency(" + Frequency + ")";
                    tapInvoice.notifications = notifications;
                    tapInvoice.currencies = currencies;
                    tapInvoice.charge = charge;
                    tapInvoice.payment_methods = p_methods;
                    tapInvoice.reference = reference;
                    tapInvoice.order = order;


                    var jsonmodel = JsonConvert.SerializeObject(tapInvoice);
                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri("https://api.tap.company/v2/invoices/"),
                        Headers =
                        {
                            { "accept", "application/json" },
                            { "Authorization", "Bearer " + userinfo.SecertKey },
                        },
                        Content = new StringContent(jsonmodel)
                        {
                            Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                        }
                    };
                    TapInvoiceResponse myDeserializedClass = null;
                    using (var response = await client.SendAsync(request))
                    {
                        var bodys = await response.Content.ReadAsStringAsync();
                        myDeserializedClass = JsonConvert.DeserializeObject<TapInvoiceResponse>(bodys);
                    }
                    if (myDeserializedClass.status == "CREATED")
                    {
                        HttpContext.Session.SetString("SubscriptionId", SubscriptionId);
                        HttpContext.Session.SetString("Frequency", Frequency);
                        HttpContext.Session.SetString("Token", Token);
                        HttpContext.Session.SetString("Invoiceid", Invoiceid);
                        ChargeResponse chargeResponse = new ChargeResponse
                        {
                            UserId = userinfo.Id,
                            ChargeId = myDeserializedClass.id,
                            amount = Convert.ToDouble(myDeserializedClass.amount),
                            currency = currency,
                            status = myDeserializedClass.status,
                        };
                        _context.chargeResponses.Add(chargeResponse);
                        _context.SaveChanges();
                        //update user 
                        userinfo.Tap_CustomerID = myDeserializedClass.customer.id;
                        userinfo.Frequency = Frequency;
                        userinfo.VAT = VAT;
                        _context.Users.Update(userinfo);
                        _context.SaveChanges();
                        return Json(new { status = true, URL = myDeserializedClass.url });
                    }
                    else
                    {
                        return Json(new { status = false, URL = myDeserializedClass.status });
                    }
                }
                return Json(false);
            }
            catch (Exception e)
            {

                throw;
            }
        }
        [HttpPost]
        public async Task<IActionResult> CreateInvoice()
        {
            try
            {
                var Currenturl = Request.Form.Where(x => x.Key == "Currenturl").FirstOrDefault().Value.ToString();
                HttpContext.Session.SetString("Currenturl", Currenturl);
                var Userid = Request.Form.Where(x => x.Key == "Userid").FirstOrDefault().Value.ToString();
                var Invoiceid = Request.Form.Where(x => x.Key == "Invoiceid").FirstOrDefault().Value.ToString();
                ApplicationUser usersinfo = null;
                Invoice invoice = null;
                var Frequency = "";
                var VAT = "";
                if (Userid != "")
                {
                    usersinfo = _context.Users.Where(x => x.Id == Userid).FirstOrDefault();
                    invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                    VAT = invoice.VAT;
                    Frequency = usersinfo.Frequency;
                }
                else
                {
                    Frequency = Request.Form.Where(x => x.Key == "Frequency").FirstOrDefault().Value.ToString();
                    VAT = Request.Form.Where(x => x.Key == "VAT").FirstOrDefault().Value.ToString();
                }
                var TotalPlanfee = Request.Form.Where(x => x.Key == "TotalPlanfee").FirstOrDefault().Value.ToString();
                var SubscriptionId = Request.Form.Where(x => x.Key == "SubscriptionId").FirstOrDefault().Value.ToString();
                var Token = Request.Form.Where(x => x.Key == "Token").FirstOrDefault().Value.ToString();
                if (SubscriptionId != null && Frequency != null)
                {
                    var userinfo = _context.Users.Where(x => x.Id == GetCurrentUserAsync().Result.Id).FirstOrDefault();
                    var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                    Random rnd = new Random();
                    var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                    var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                    var amount = TotalPlanfee.ToString();
                    var description = subscriptions.Frequency;
                    Reference reference = new Reference();
                    reference.transaction = TransNo;
                    reference.order = OrderNo;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerify";

                    Post post = new Post();
                    post.url = Constants.RedirectURL + "/Home/CardVerifyurl";

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
                    var currency = subscriptions.Currency;
                    Phone phone = new Phone();
                    phone.number = userinfo.PhoneNumber;
                    phone.country_code = countrycode;

                    Customer customer = new Customer();
                    customer.first_name = userinfo.FullName;
                    customer.email = userinfo.Email;
                    customer.phone = phone;

                    Receipt receipt = new Receipt();
                    receipt.sms = true;
                    receipt.email = true;

                    Metadata metadata = new Metadata();
                    metadata.udf1 = "Metadata 1";

                    Source source = new Source();
                    source.id = Token;

                    Merchant merchant = new Merchant();
                    merchant.id = userinfo.MarchantID;

                    FillChargeModel fillChargeModel = new FillChargeModel();
                    fillChargeModel.threeDSecure = true;
                    fillChargeModel.amount = amount;
                    fillChargeModel.save_card = true;
                    fillChargeModel.currency = currency;
                    fillChargeModel.redirect = redirect;
                    fillChargeModel.post = post;
                    fillChargeModel.customer = customer;
                    fillChargeModel.metadata = metadata;
                    fillChargeModel.reference = reference;
                    fillChargeModel.receipt = receipt;
                    fillChargeModel.source = source;
                    fillChargeModel.merchant = merchant;
                    fillChargeModel.customer_initiated = true;
                    var jsonmodel = JsonConvert.SerializeObject(fillChargeModel);
                    var client_charge = new HttpClient();
                    var request_charge = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                    request_charge.Headers.Add("Authorization", "Bearer " + userinfo.SecertKey);
                    request_charge.Headers.Add("accept", "application/json");
                    var content_charge = new StringContent(jsonmodel, null, "application/json");
                    request_charge.Content = content_charge;
                    var response_charge = await client_charge.SendAsync(request_charge);
                    var body = await response_charge.Content.ReadAsStringAsync();
                    CreateCharge deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(body);
                    if (deserialized_CreateCharge.status == "INITIATED")
                    {
                        HttpContext.Session.SetString("SubscriptionId", SubscriptionId);
                        HttpContext.Session.SetString("Frequency", Frequency);
                        HttpContext.Session.SetString("Token", Token);
                        HttpContext.Session.SetString("Invoiceid", Invoiceid);
                        ChargeResponse chargeResponse = new ChargeResponse
                        {
                            UserId = userinfo.Id,
                            ChargeId = deserialized_CreateCharge.id,
                            amount = deserialized_CreateCharge.amount,
                            currency = currency,
                            status = deserialized_CreateCharge.status,
                        };
                        _context.chargeResponses.Add(chargeResponse);
                        _context.SaveChanges();
                        //update user 
                        userinfo.Tap_CustomerID = deserialized_CreateCharge.customer.id;
                        userinfo.Frequency = Frequency;
                        userinfo.VAT = VAT;
                        _context.Users.Update(userinfo);
                        _context.SaveChanges();
                        return Json(new { status = true, URL = deserialized_CreateCharge.transaction.url });
                    }
                    else
                    {
                        return Json(new { status = false, URL = deserialized_CreateCharge.response.message });
                    }
                }
                return Json(false);
            }
            catch (Exception e)
            {

                throw;
            }
        }
        public async Task<IActionResult> CardVerify()
        {
            try
            {
                string tap_id = HttpContext.Request.Query["tap_id"].ToString();
                ChargeDetail Deserialized_savecard = null;
                var log_user = GetCurrentUserAsync().Result;
                if (tap_id != null && log_user.Id != null)
                {
                    //Get Charge Detail
                    var client_ChargeDetail = new HttpClient();
                    var request_ChargeDetail = new HttpRequestMessage(HttpMethod.Get, "https://api.tap.company/v2/charges/" + tap_id);
                    request_ChargeDetail.Headers.Add("Authorization", "Bearer " + log_user.SecertKey);
                    request_ChargeDetail.Headers.Add("accept", "application/json");
                    var response_ChargeDetail = await client_ChargeDetail.SendAsync(request_ChargeDetail);
                    var result_ChargeDetail = await response_ChargeDetail.Content.ReadAsStringAsync();
                    Deserialized_savecard = JsonConvert.DeserializeObject<ChargeDetail>(result_ChargeDetail);
                }
                var SubscriptionId = HttpContext.Session.GetString("SubscriptionId");
                int getchargesresposemodel = _context.chargeResponses.Max(x => x.ChargeResponseId);
                if (Deserialized_savecard.status == "CAPTURED")
                {
                    var Frequency = HttpContext.Session.GetString("Frequency");
                    var Invoiceid = HttpContext.Session.GetString("Invoiceid");
                    if (Deserialized_savecard.id != null)
                    {
                        if (Invoiceid == null || Invoiceid == "")
                        {
                            //Create Invoice
                            var users = GetCurrentUserAsync().Result;
                            var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                            int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                            decimal finalamount = 0;
                            decimal Discount = 0;
                            decimal Vat = 0;
                            if (users.Frequency == "DAILY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                            }
                            else if (users.Frequency == "WEEKLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                            }
                            else if (users.Frequency == "MONTHLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                            }
                            else if (users.Frequency == "QUARTERLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                            }
                            else if (users.Frequency == "HALFYEARLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                            }
                            else if (users.Frequency == "YEARLY")
                            {
                                var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * 10;
                                var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                                finalamount = final_amount_percentage * 12;
                                Discount = amountpercentage * 12;
                            }
                            if (subscriptions.VAT == null || subscriptions.VAT == "0")
                            {
                                Vat = 0;
                            }
                            else
                            {
                                decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                            }
                            decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                            Invoice invoices = new Invoice
                            {
                                InvoiceStartDate = DateTime.UtcNow,
                                InvoiceEndDate = DateTime.UtcNow,
                                Currency = subscriptions.Currency,
                                AddedDate = DateTime.UtcNow,
                                AddedBy = GetCurrentUserAsync().Result.FullName,
                                SubscriptionAmount = Convert.ToDouble(decimal.Round(after_vat_totalamount)),
                                SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                Status = "Un-Paid",
                                IsDeleted = false,
                                VAT = Vat.ToString(),
                                Discount = Discount.ToString(),
                                Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                SubscriptionName = subscriptions.Name,
                                UserId = users.Id,
                                ChargeId = tap_id,
                                GymName = users.GYMName,
                                Country = subscriptions.Countries
                            };
                            _context.invoices.Add(invoices);
                            _context.SaveChanges();
                            // Update Recurring Job data
                            int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                            var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();
                            //update user 
                            users.Tap_CustomerID = Deserialized_savecard.payment_agreement.contract.customer_id;
                            users.Tap_Card_ID = Deserialized_savecard.payment_agreement.contract.id;
                            users.SubscribeID = Convert.ToInt32(SubscriptionId);
                            users.Tap_Agreement_ID = Deserialized_savecard.payment_agreement.id;
                            users.PaymentSource = Deserialized_savecard.source.payment_method;
                            users.First_Six = Deserialized_savecard.card.first_six;
                            users.Last_Four = Deserialized_savecard.card.last_four;
                            _context.Users.Update(users);
                            _context.SaveChanges();

                            UserSubscriptions userSubscriptions = new UserSubscriptions();
                            userSubscriptions.SubID = Convert.ToInt32(SubscriptionId);
                            userSubscriptions.Userid = userinfo.Id;
                            _context.userSubscriptions.Add(userSubscriptions);
                            _context.SaveChanges();


                            DateTime nextrecurringdate = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).Select(x => x.InvoiceEndDate).FirstOrDefault();
                            RecurringCharge recurringCharge = new RecurringCharge();
                            recurringCharge.Amount = Convert.ToDecimal(finalamount);
                            recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                            recurringCharge.UserID = users.Id;
                            recurringCharge.Tap_CustomerId = Deserialized_savecard.payment_agreement.contract.customer_id;
                            recurringCharge.ChargeId = tap_id;
                            recurringCharge.Invoice = "Inv" + max_invoice_id;
                            recurringCharge.IsRun = false;
                            recurringCharge.JobRunDate = nextrecurringdate.AddDays(1);
                            _context.recurringCharges.Add(recurringCharge);
                            _context.SaveChanges();

                            Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                            invoice_info.ChargeId = tap_id;
                            invoice_info.Status = "Payment Captured";
                            invoice_info.ChargeResponseId = getchargesresposemodel;
                            _context.invoices.Update(invoice_info);
                            _context.SaveChanges();
                            // Send Email
                            string body = string.Empty;
                            _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                            string contentRootPath = _environment.WebRootPath + "/htmltopdf.html";
                            string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                            //Generate PDF
                            var cr = _context.chargeResponses.Where(x => x.ChargeId == tap_id).FirstOrDefault();
                            var sub_id = HttpContext.Session.GetString("SubscriptionId");
                            var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(sub_id)).FirstOrDefault();
                            using (StreamReader reader = new StreamReader(contentRootPath))
                            {
                                body = reader.ReadToEnd();
                            }
                            //Fill EMail By Parameter
                            body = body.Replace("{title}", "Tamarran Payment Invoice");
                            body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                            body = body.Replace("{InvocieStatus}", "Payment Captured");
                            body = body.Replace("{InvoiceID}", "Inv" + invoice_info.InvoiceId);

                            body = body.Replace("{User_Name}", userinfo.FullName);
                            body = body.Replace("{User_Email}", userinfo.Email);
                            body = body.Replace("{User_GYM}", userinfo.GYMName);
                            body = body.Replace("{User_Phone}", userinfo.PhoneNumber);


                            body = body.Replace("{SubscriptionName}", subscriptions.Name);
                            body = body.Replace("{Discount}", Discount.ToString());
                            body = body.Replace("{SubscriptionPeriod}", userinfo.Frequency);
                            body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
                            int amount = Convert.ToInt32(finalamount) + Convert.ToInt32(Math.Round(decimal.Round(Convert.ToDecimal(subscriptions.SetupFee), 2), 0, MidpointRounding.AwayFromZero));
                            body = body.Replace("{SubscriptionAmount}", decimal.Round(Convert.ToDecimal(finalamount), 2).ToString() + " " + subscriptions.Currency);
                            //Calculate VAT
                            if (subscriptions.VAT == null || subscriptions.VAT == "0")
                            {
                                body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                                body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                                var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                            }
                            else
                            {
                                body = body.Replace("{VAT}", decimal.Round(Convert.ToDecimal(Vat), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                                var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                            }

                            var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                            var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                            _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, "Tamarran – Payment Receipt", bodyemail);
                            return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
                        }
                        else
                        {
                            //Create Invoice
                            var users = GetCurrentUserAsync().Result;
                            var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                            var Amount = subscriptions.Amount;
                            int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                            decimal finalamount = 0;
                            decimal Discount = 0;
                            decimal Vat = 0;
                            if (users.Frequency == "DAILY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                            }
                            else if (users.Frequency == "WEEKLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                            }
                            else if (users.Frequency == "MONTHLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                            }
                            else if (users.Frequency == "QUARTERLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                            }
                            else if (users.Frequency == "HALFYEARLY")
                            {
                                Discount = 0;
                                finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                            }
                            else if (users.Frequency == "YEARLY")
                            {
                                var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * 10;
                                var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                                finalamount = final_amount_percentage * 12;
                                Discount = amountpercentage * 12;
                            }
                            if (subscriptions.VAT == null || subscriptions.VAT == "0")
                            {
                                Vat = 0;
                            }
                            else
                            {
                                decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                            }
                            decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                            var max_invoice_id = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                            var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();


                            //update user 
                            users.Tap_CustomerID = Deserialized_savecard.payment_agreement.contract.customer_id;
                            users.Tap_Card_ID = Deserialized_savecard.payment_agreement.contract.id;
                            users.SubscribeID = Convert.ToInt32(SubscriptionId);
                            users.Tap_Agreement_ID = Deserialized_savecard.payment_agreement.id;
                            users.PaymentSource = Deserialized_savecard.source.payment_method;
                            users.First_Six = Deserialized_savecard.card.first_six;
                            users.Last_Four = Deserialized_savecard.card.last_four;
                            _context.Users.Update(users);
                            _context.SaveChanges();

                            UserSubscriptions userSubscriptions = new UserSubscriptions();
                            userSubscriptions.SubID = Convert.ToInt32(SubscriptionId);
                            userSubscriptions.Userid = userinfo.Id;
                            _context.userSubscriptions.Add(userSubscriptions);
                            _context.SaveChanges();


                            RecurringCharge recurringCharge = new RecurringCharge();
                            recurringCharge.Amount = decimal.Round(finalamount);
                            recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                            recurringCharge.UserID = users.Id;
                            recurringCharge.Tap_CustomerId = Deserialized_savecard.payment_agreement.contract.customer_id;
                            recurringCharge.ChargeId = tap_id;
                            recurringCharge.IsRun = false;
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(1);
                            _context.recurringCharges.Add(recurringCharge);
                            _context.SaveChanges();

                            Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                            invoice_info.ChargeId = tap_id;
                            invoice_info.UserId = users.Id;
                            invoice_info.Status = "Payment Captured";
                            invoice_info.ChargeResponseId = getchargesresposemodel;
                            _context.invoices.Update(invoice_info);
                            _context.SaveChanges();
                            // Send Email
                            string body = string.Empty;
                            _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                            string contentRootPath = _environment.WebRootPath + "/htmltopdf.html";
                            string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                            //Generate PDF
                            var cr = _context.chargeResponses.Where(x => x.ChargeId == tap_id).FirstOrDefault();
                            var sub_id = HttpContext.Session.GetString("SubscriptionId");
                            var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(sub_id)).FirstOrDefault();
                            using (StreamReader reader = new StreamReader(contentRootPath))
                            {
                                body = reader.ReadToEnd();
                            }
                            //Fill EMail By Parameter
                            body = body.Replace("{title}", "Tamarran Payment Invoice");
                            body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                            body = body.Replace("{InvocieStatus}", "Payment Captured");
                            body = body.Replace("{InvoiceID}", "Inv" + invoice_info.InvoiceId.ToString());


                            body = body.Replace("{User_Name}", userinfo.FullName);
                            body = body.Replace("{User_Email}", userinfo.Email);
                            body = body.Replace("{User_GYM}", userinfo.GYMName);
                            body = body.Replace("{User_Phone}", userinfo.PhoneNumber);


                            body = body.Replace("{SubscriptionName}", subscriptions.Name);
                            body = body.Replace("{Discount}", Discount.ToString());
                            body = body.Replace("{SubscriptionPeriod}", userinfo.Frequency);
                            body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
                            int amount = Convert.ToInt32(finalamount) + Convert.ToInt32(Math.Round(decimal.Round(Convert.ToDecimal(subscriptions.SetupFee), 1), 0, MidpointRounding.AwayFromZero));
                            body = body.Replace("{SubscriptionAmount}", decimal.Round(Convert.ToDecimal(finalamount), 2).ToString() + " " + subscriptions.Currency);
                            //Calculate VAT
                            if (subscriptions.VAT == null || subscriptions.VAT == "0")
                            {
                                body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                                body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                                var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                            }
                            else
                            {
                                body = body.Replace("{VAT}", decimal.Round(Convert.ToDecimal(Vat), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                                body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                                var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                            }
                            var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                            var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                            _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, "Tamarran – Payment Receipt", bodyemail);
                            return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
                        }
                    }
                    else
                    {
                        //Update Charge Response;
                        var chargeresponse = _context.chargeResponses.Where(x => x.ChargeResponseId == getchargesresposemodel).FirstOrDefault();
                        _context.chargeResponses.Remove(chargeresponse);
                        _context.SaveChanges();
                    }
                    return View();
                }
                else
                {
                    //Remove Charge Response;
                    var chargeresponse = _context.chargeResponses.Where(x => x.ChargeResponseId == getchargesresposemodel).FirstOrDefault();
                    _context.chargeResponses.Remove(chargeresponse);
                    _context.SaveChanges();

                    var _uri = HttpContext.Session.GetString("Currenturl");
                    string[] arrs = _uri.Split('/');
                    if (arrs[4] == "Subscription")
                    {
                        TempData["Message"] = Deserialized_savecard.status + " - (" + Deserialized_savecard.source.payment_method + ")";
                        return Redirect(_uri);
                    }
                    else
                    {
                        TempData["Message"] = Deserialized_savecard.status + " - (" + Deserialized_savecard.source.payment_method + ")";
                        return Redirect(_uri);
                    }

                }
            }
            catch (Exception e)
            {

                throw;
            }
        }
        public async Task<IActionResult> CardVerifyBenefit()
        {
            try
            {
                string tap_id = HttpContext.Request.Query["tap_id"].ToString();
                TapInvoiceResponseDTO Deserialized_savecard = null;
                var log_user = GetCurrentUserAsync().Result;
                if (tap_id != null && log_user.Id != null)
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://api.tap.company/v2/invoices/" + tap_id),
                        Headers =
                        {
                            { "accept", "application/json" },
                            { "Authorization", "Bearer " + log_user.SecertKey },
                        },
                    };
                    using (var response = await client.SendAsync(request))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Deserialized_savecard = JsonConvert.DeserializeObject<TapInvoiceResponseDTO>(body);
                    }
                }
                var SubscriptionId = HttpContext.Session.GetString("SubscriptionId");
                int getchargesresposemodel = _context.chargeResponses.Max(x => x.ChargeResponseId);
                if (Deserialized_savecard.status == "PAID")
                {
                    var Frequency = HttpContext.Session.GetString("Frequency");
                    var Invoiceid = log_user.Benefit_Invoice;
                    if (Deserialized_savecard.id != null)
                    {
                        //Create Invoice
                        var users = GetCurrentUserAsync().Result;
                        var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                        int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                        decimal finalamount = 0;
                        decimal Discount = 0;
                        decimal Vat = 0;
                        if (users.Frequency == "DAILY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                        }
                        else if (users.Frequency == "WEEKLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                        }
                        else if (users.Frequency == "MONTHLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                        }
                        else if (users.Frequency == "QUARTERLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                        }
                        else if (users.Frequency == "HALFYEARLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                        }
                        else if (users.Frequency == "YEARLY")
                        {
                            var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * 10;
                            var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                            finalamount = final_amount_percentage * 12;
                            Discount = amountpercentage * 12;
                        }
                        if (subscriptions.VAT == null || subscriptions.VAT == "0")
                        {
                            Vat = 0;
                        }
                        else
                        {
                            decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                            Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                        }
                        decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                        //Remove Old Invoice
                        if (Invoiceid != null)
                        {
                            var getoldinvoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(Invoiceid)).FirstOrDefault();
                            _context.Remove(getoldinvoice);
                            _context.SaveChanges();
                        }
                        Invoice invoices = new Invoice
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow,
                            Currency = subscriptions.Currency,
                            AddedDate = DateTime.UtcNow,
                            AddedBy = GetCurrentUserAsync().Result.FullName,
                            SubscriptionAmount = Convert.ToDouble(decimal.Round(after_vat_totalamount, 2)),
                            SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                            Status = "Un-Paid",
                            IsDeleted = false,
                            VAT = Vat.ToString(),
                            Discount = Discount.ToString(),
                            Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                            SubscriptionName = subscriptions.Name,
                            UserId = users.Id,
                            ChargeId = tap_id,
                            GymName = users.GYMName,
                            Country = subscriptions.Countries
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                        // Update Recurring Job data
                        int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                        var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();
                        //update user 
                        users.SubscribeID = Convert.ToInt32(SubscriptionId);
                        users.Tap_CustomerID = Deserialized_savecard.customer.id;
                        users.First_Six = "******";
                        users.Last_Four = "****";
                        users.PaymentSource = "BENEFIT";
                        _context.Users.Update(users);
                        _context.SaveChanges();

                        UserSubscriptions userSubscriptions = new UserSubscriptions();
                        userSubscriptions.SubID = Convert.ToInt32(SubscriptionId);
                        userSubscriptions.Userid = userinfo.Id;
                        _context.userSubscriptions.Add(userSubscriptions);
                        _context.SaveChanges();

                        DateTime nextrecurringdate = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).Select(x => x.InvoiceEndDate).FirstOrDefault();
                        RecurringCharge recurringCharge = new RecurringCharge();
                        recurringCharge.Amount = Convert.ToDecimal(finalamount);
                        recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                        recurringCharge.UserID = users.Id;
                        recurringCharge.Tap_CustomerId = Deserialized_savecard.customer.id;
                        recurringCharge.ChargeId = tap_id;
                        recurringCharge.Invoice = "Inv" + max_invoice_id;
                        recurringCharge.IsRun = false;
                        recurringCharge.JobRunDate = nextrecurringdate.AddDays(1);
                        _context.recurringCharges.Add(recurringCharge);
                        _context.SaveChanges();

                        Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                        invoice_info.ChargeId = tap_id;
                        invoice_info.Status = "Payment Captured";
                        invoice_info.ChargeResponseId = getchargesresposemodel;
                        _context.invoices.Update(invoice_info);
                        _context.SaveChanges();
                        // Send Email
                        string body = string.Empty;
                        _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        string contentRootPath = _environment.WebRootPath + "/htmltopdf.html";
                        string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                        //Generate PDF
                        var cr = _context.chargeResponses.Where(x => x.ChargeId == tap_id).FirstOrDefault();
                        var sub_id = HttpContext.Session.GetString("SubscriptionId");
                        var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(sub_id)).FirstOrDefault();
                        using (StreamReader reader = new StreamReader(contentRootPath))
                        {
                            body = reader.ReadToEnd();
                        }
                        //Fill EMail By Parameter
                        body = body.Replace("{title}", "Tamarran Payment Invoice");
                        body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                        body = body.Replace("{InvocieStatus}", "Payment Captured");
                        body = body.Replace("{InvoiceID}", "Inv" + invoice_info.InvoiceId);

                        body = body.Replace("{User_Name}", userinfo.FullName);
                        body = body.Replace("{User_Email}", userinfo.Email);
                        body = body.Replace("{User_GYM}", userinfo.GYMName);
                        body = body.Replace("{User_Phone}", userinfo.PhoneNumber);


                        body = body.Replace("{SubscriptionName}", subscriptions.Name);
                        body = body.Replace("{Discount}", Discount.ToString());
                        body = body.Replace("{SubscriptionPeriod}", userinfo.Frequency);
                        body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
                        int amount = Convert.ToInt32(finalamount) + Convert.ToInt32(Math.Round(decimal.Round(Convert.ToDecimal(subscriptions.SetupFee), 2), 0, MidpointRounding.AwayFromZero));
                        body = body.Replace("{SubscriptionAmount}", decimal.Round(Convert.ToDecimal(finalamount), 2).ToString() + " " + subscriptions.Currency);
                        //Calculate VAT
                        if (subscriptions.VAT == null || subscriptions.VAT == "0")
                        {
                            body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                            body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                            body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(amount), 2).ToString() + " " + subscriptions.Currency);
                            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                            body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                        }
                        else
                        {
                            body = body.Replace("{VAT}", decimal.Round(Convert.ToDecimal(Vat), 2).ToString() + " " + subscriptions.Currency);
                            body = body.Replace("{Total}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                            body = body.Replace("{InvoiceAmount}", decimal.Round(Convert.ToDecimal(after_vat_totalamount), 2).ToString() + " " + subscriptions.Currency);
                            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                            body = body.Replace("{Totalinvoicewithoutvat}", decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptions.Currency);
                        }

                        var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                        var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                        _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, "Tamarran – Payment Receipt", bodyemail);
                        return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
                    }
                    else
                    {
                        //Update Charge Response;
                        var chargeresponse = _context.chargeResponses.Where(x => x.ChargeResponseId == getchargesresposemodel).FirstOrDefault();
                        _context.chargeResponses.Remove(chargeresponse);
                        _context.SaveChanges();
                    }
                    return View();
                }
                else
                {
                    //Remove Charge Response;
                    var chargeresponse = _context.chargeResponses.Where(x => x.ChargeResponseId == getchargesresposemodel).FirstOrDefault();
                    _context.chargeResponses.Remove(chargeresponse);
                    _context.SaveChanges();

                    var _uri = HttpContext.Session.GetString("Currenturl");
                    if (_uri != null)
                    {
                        string[] arrs = _uri.Split('/');
                        if (arrs[4] == "Subscription")
                        {
                            TempData["Message"] = Deserialized_savecard.status;
                            return Redirect(_uri);
                        }
                        else
                        {
                            TempData["Message"] = Deserialized_savecard.status;
                            return Redirect(_uri);
                        }
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
            }
            catch (Exception e)
            {

                throw;
            }
        }
        #endregion
        #region Admin Dashboard

        [Authorize]
        public IActionResult Dashboard()
        {
            ViewBag.CustomerCount = _userManager.Users.Where(x => x.Status == true).ToList().Count();
            ViewBag.InvoiceCount = _context.invoices.Where(x => x.Status == "Payment Captured").ToList().Count();
            ViewBag.ChangeCardCount = _context.changeCardInfos.ToList().Count();
            ViewBag.SubscriptionCount = _context.subscriptions.Where(x => x.Status == true).ToList().Count();
            return View();
        }
        //Customer Section
        [Authorize]
        public IActionResult ViewCustomer()
        {
            var users = (from um in _context.Users
                         join sub in _context.subscriptions on um.SubscribeID equals sub.SubscriptionId into ps
                         from sub in ps.DefaultIfEmpty()
                         select new UserInfoDTO
                         {
                             Id = um.Id,
                             FullName = um.FullName,
                             Email = um.Email,
                             PhoneNumber = um.PhoneNumber,
                             Country = um.Country,
                             City = um.City,
                             Currency = um.Currency,
                             SubscribeName = sub.Name + " " + "-" + " " + "(" + sub.Amount + ")",
                             SubscribeID = um.SubscribeID,
                             Status = um.Status,
                             PaymentSource = um.PaymentSource,
                             Tap_CustomerID = um.Tap_CustomerID,
                             GYMName = um.GYMName,
                             UserType = um.UserType
                         });
            return View(users);
        }
        public IActionResult ViewAllInvoices(string userId)
        {
            var incoices = _context.invoices.Where(x => x.UserId == userId).ToList();
            return View(incoices);
        }
        public IActionResult AddCustomer()
        {
            ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> AddCustomer(ApplicationUser applicationUser)
        {

#if !DEBUG
            if (applicationUser.recaptchaToken != null ||
                applicationUser.recaptchaToken != "" ||
                applicationUser.recaptchaToken != "null" ||
                applicationUser.recaptchaToken != string.Empty)
            {
                bool isCapthcaValid = CreateAssessmentSample.ValidateCaptcha(applicationUser.recaptchaToken);
                if (ModelState.IsValid)
                {
                    if (!isCapthcaValid)
                    {
                        ModelState.AddModelError(string.Empty, "You have put wrong Captcha,Please ensure the authenticity !!!");
                    }
                }
            }
#endif

            var resultuser = await _userManager.FindByEmailAsync(applicationUser.UserName);
            if (resultuser != null)
            {
                ViewBag.SubscriptionList = applicationUser.SubscribeID;
                ModelState.AddModelError(string.Empty, "Email Already Exists..!");
                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                return View(applicationUser);
            }
            //Save data to tap side
            InvoiceHelper.SetCountryAndCurrency(applicationUser, out string countrycode, out string currencycode);
            applicationUser.Currency = currencycode;
            var email = applicationUser.UserName;

            // save data to database
            applicationUser.Email = email;
            applicationUser.Status = true;
            applicationUser.UserType = "Customer";
            applicationUser.EmailConfirmed = true;
            applicationUser.PhoneNumberConfirmed = true;
            applicationUser.PhoneNumber = applicationUser.PhoneNumber;
            applicationUser.Password = applicationUser.Password;
            applicationUser.Tap_CustomerID = null;
            if (applicationUser.Password == null)
            {
                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                ModelState.AddModelError(string.Empty, "Please Enter The Password...!");
                return View();
            }
            var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == applicationUser.SubscribeID).FirstOrDefault();
            if (subscriptions.Countries == "Bahrain")
            {
                applicationUser.PublicKey = Constants.BHD_Public_Key;
                applicationUser.SecertKey = Constants.BHD_Test_Key;
                applicationUser.MarchantID = Constants.BHD_Merchant_Key;
            }
            else if (subscriptions.Countries == "KSA")
            {
                applicationUser.PublicKey = Constants.KSA_Public_Key;
                applicationUser.SecertKey = Constants.KSA_Test_Key;
                applicationUser.MarchantID = Constants.KSA_Merchant_Key;
            }
            Guid guid = Guid.NewGuid();
            string str = guid.ToString();
            applicationUser.Id = str;

            var result = await _userManager.CreateAsync(applicationUser, applicationUser.Password);
            if (result.Succeeded)
            {
                Invoice invoices = new Invoice()
                {
                    InvoiceStartDate = DateTime.UtcNow,
                    InvoiceEndDate = DateTime.UtcNow,
                    Currency = subscriptions.Currency,
                    AddedDate = DateTime.UtcNow,
                    VAT = subscriptions.VAT == null ? "0" : subscriptions.VAT,
                    Discount = subscriptions.Discount == null ? "0" : subscriptions.Discount,
                    AddedBy = "Super Admin",
                    SubscriptionAmount = 0,
                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                    Status = "Un-Paid",
                    IsDeleted = false,
                    Description = "Invoice Create - Frequency(" + applicationUser.Frequency + ")",
                    SubscriptionName = subscriptions.Name,
                    UserId = "",
                    ChargeId = "",
                    GymName = applicationUser.GYMName,
                    Country = subscriptions.Countries
                };
                _context.invoices.Add(invoices);
                _context.SaveChanges();

                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                applicationUser.Benefit_Invoice = max_invoice_id.ToString();

                string max_user_id = _context.Users.Where(x => x.Email == applicationUser.Email).Select(x => x.Id).FirstOrDefault();
                //Create Invoice
                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

                InvoiceHelper.GetDiscountAndFinalAmountBySubscriptionFrequency(applicationUser.Frequency, subscriptions.Amount, days, out decimal discount, out decimal finalAmount);
                InvoiceHelper.CalculdateInvoiceDetails(finalAmount, subscriptions, out string subscriptionAmount, out decimal after_vat_totalamount, out decimal vat,out string vat_str, out string total, out string invoiceAmount, out string Totalinvoicewithoutvat);

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
                body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                body = body.Replace("{InvocieStatus}", "Unpaid");
                body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);

                body = body.Replace("{User_Name}", applicationUser.FullName);
                body = body.Replace("{User_Email}", applicationUser.Email);
                body = body.Replace("{User_GYM}", applicationUser.GYMName);
                body = body.Replace("{User_Phone}", applicationUser.PhoneNumber);

                body = body.Replace("{SubscriptionName}", subscriptions.Name);
                body = body.Replace("{Discount}", discount.ToString());
                body = body.Replace("{SubscriptionPeriod}", applicationUser.Frequency);
                body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);

                body = body.Replace("{SubscriptionAmount}", subscriptionAmount);
                //Calculate VAT
                body = body.Replace("{VAT}", vat_str);
                body = body.Replace("{Total}", total);
                body = body.Replace("{InvoiceAmount}", invoiceAmount);
                body = body.Replace("{Totalinvoicewithoutvat}", Totalinvoicewithoutvat);

                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var callbackUrl = @Url.Action("SubscriptionAdmin", "Home", new { id = applicationUser.SubscribeID, link = "Yes", userid = max_user_id, invoiceid = max_invoice_id, After_vat_totalamount = after_vat_totalamount });
                var websiteurl = HtmlEncoder.Default.Encode(Constants.RedirectURL + callbackUrl);

                var subject = "Tamarran – Payment Request";
                var bodyemail = EmailBodyFill.EmailBodyForPaymentRequest(applicationUser, websiteurl);
                _ = _emailSender.SendEmailWithFIle(bytes, applicationUser.Email, subject, bodyemail);


                var adduser = _context.Users.Where(x => x.Email == applicationUser.Email).FirstOrDefault();
                var invoiceinfo = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                invoiceinfo.InvoiceLink = Constants.RedirectURL + callbackUrl;
                invoiceinfo.VAT = vat.ToString();
                invoiceinfo.Discount = discount.ToString();
                invoiceinfo.AddedBy = "Super Admin";
                invoiceinfo.UserId = adduser.Id;
                invoiceinfo.SubscriptionAmount = Convert.ToDouble(decimal.Round(after_vat_totalamount));
                _context.invoices.Update(invoiceinfo);
                _context.SaveChanges();
                return RedirectToAction("ViewCustomer", "Home");
            }
            foreach (var error in result.Errors)
            {
                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }
        public ActionResult DeleteCustomer(string userId)
        {
            var result = _context.Users.Where(x => x.Id == userId).FirstOrDefault();
            if (result != null)
            {
                _context.Remove(result);
                _context.SaveChanges();
            }
            return RedirectToAction("ViewCustomer", "Home");
        }
        public async Task<IActionResult> InActiveUser(string id)
        {
            var subscriptions = _context.Users.Where(x => x.Id == id).FirstOrDefault();
            await _userManager.UpdateSecurityStampAsync(subscriptions);
            subscriptions.Status = false;
            _context.Users.Update(subscriptions);
            _context.SaveChanges();
            return RedirectToAction("ViewCustomer", "Home");
        }
        public IActionResult ActiveUser(string id)
        {
            var subscriptions = _context.Users.Where(x => x.Id == id).FirstOrDefault();
            subscriptions.Status = true;
            _context.Users.Update(subscriptions);
            _context.SaveChanges();
            return RedirectToAction("ViewCustomer", "Home");
        }
        public ActionResult DeleteInvoice(int id, string userid, string status)
        {
            var result = _context.invoices.Where(x => x.InvoiceId == id && x.UserId == userid).FirstOrDefault();
            _context.invoices.Remove(result);
            _context.SaveChanges();
            return RedirectToAction("CreateInvoice", "Home", new { PaymentStatus = "All" });
        }
        //Subscription Section
        [Authorize]
        public IActionResult Viewsubscription()
        {
            var subscriptions = _context.subscriptions.ToList();
            return View(subscriptions);
        }
        public ActionResult Deletesubscription(string userId)
        {
            var result = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(userId)).FirstOrDefault();
            result.Status = false;
            if (result != null)
            {
                _context.Remove(result);
                _context.SaveChanges();
            }
            return RedirectToAction("Viewsubscription", "Home");
        }
        public IActionResult Addsubscription()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Addsubscription(Subscriptions subscription, string[] Frequency)
        {
            if (ModelState.IsValid)
            {
                var frequency = string.Join(",", Frequency);
                bool vat = string.IsNullOrWhiteSpace(subscription.VAT.ToString());
                if (vat == true)
                {
                    subscription.VAT = null;
                }
                if (subscription.VAT == "0")
                {
                    subscription.VAT = null;
                }
                subscription.CreatedDate = DateTime.UtcNow;
                subscription.Status = true;
                subscription.Frequency = frequency;
                _context.subscriptions.Add(subscription);
                _context.SaveChanges();
                return RedirectToAction("Viewsubscription", "Home");
            }

            ModelState.AddModelError(string.Empty, "");
            return View();
        }
        public IActionResult Editsubscription(string userId)
        {
            var subscriptions = _context.subscriptions.Where(x => x.Status == true && x.SubscriptionId == Convert.ToInt32(userId)).FirstOrDefault();
            var s = string.Join(",", subscriptions.Frequency.Split(',').Select(x => string.Format("'{0}'", x)));
            ViewBag.Fre = s;
            return View(subscriptions);
        }
        [HttpPost]
        public IActionResult Editsubscription(Subscriptions subscription, string[] Frequency)
        {
            if (ModelState.IsValid)
            {
                var frequency = string.Join(",", Frequency);
                subscription.Status = true;
                subscription.Frequency = frequency;
                _context.subscriptions.Update(subscription);
                _context.SaveChanges();
                return RedirectToAction("Viewsubscription", "Home");
            }
            else
            {
                return View(subscription);
            }
        }
        [HttpPost]
        public ActionResult getFrequency()
        {
            var Currenturl = Request.Form.Where(x => x.Key == "Frequency").FirstOrDefault().Value.ToString();
            return Json(_context.subscriptions.Select(x => new
            {
                SubscriptionId = x.SubscriptionId,
                Frequency = x.Frequency
            }).Where(x => x.SubscriptionId == Convert.ToInt32(Currenturl)).FirstOrDefault());
        }
        public IActionResult InActiveSubscription(int id)
        {
            var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
            subscriptions.Status = false;
            _context.subscriptions.Update(subscriptions);
            _context.SaveChanges();
            return RedirectToAction("Viewsubscription", "Home");
        }
        public IActionResult ActiveSubscription(int id)
        {
            var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
            subscriptions.Status = true;
            _context.subscriptions.Update(subscriptions);
            _context.SaveChanges();
            return RedirectToAction("Viewsubscription", "Home");
        }
        public IActionResult GetAllCharges()
        {
            var users = (from cr in _context.invoices
                         join um in _context.Users on cr.UserId equals um.Id
                         where cr.Status == "Payment Captured"
                         select new ChargeListDTO
                         {
                             Tap_CustomerID = um.Tap_CustomerID,
                             UserId = um.Id,
                             FullName = um.FullName,
                             Email = um.Email,
                             Country = um.Country,
                             City = um.City,
                             currency = um.Currency,
                             ChargeId = cr.ChargeId,
                             status = cr.Status,
                             PaymentDate = cr.AddedDate,
                             amount = cr.SubscriptionAmount,
                             GYMName = um.GYMName
                         }).ToList();
            return View(users);
        }
        public ActionResult UnSubscribeSubscription(string id)
        {
            var userinfo = _context.Users.Where(x => x.Id == id).FirstOrDefault();
            userinfo.SubscribeID = 0;
            _context.Users.Update(userinfo);
            _context.SaveChanges();

            // Send Email
            var bodyemail = EmailBodyFill.EmailBodyForUnsubscriptionSuccessful(userinfo);
            _ = _emailSender.SendEmailAsync(userinfo.Email, "Un-subscription Confirmation - You're always welcome back!", bodyemail);
            return RedirectToAction("ViewGYMCustomer");
        }
        //List Section
        public ActionResult ViewSubinfo()
        {
            var list = _context.userSubscriptions.Where(x => x.Userid == GetCurrentUserAsync().Result.Id).ToList();
            List<UserInfoDTO> userlist = new List<UserInfoDTO>(); ;
            foreach (var item in list)
            {
                var users = (from um in _context.Users
                             where um.Id == item.Userid
                             select new UserInfoDTO
                             {
                                 Id = um.Id,
                                 FullName = um.FullName,
                                 Email = um.Email,
                                 PhoneNumber = um.PhoneNumber,
                                 Country = um.Country,
                                 City = um.City,
                                 Currency = um.Currency,
                                 SubscribeName = _context.subscriptions.Where(x => x.SubscriptionId == item.SubID).Select(x => x.Name).FirstOrDefault() + " " + "-" + " " + "(" + _context.subscriptions.Where(x => x.SubscriptionId == item.SubID).Select(x => x.Amount).FirstOrDefault() + ")",
                                 SubscribeID = um.SubscribeID,
                                 Status = um.Status,
                                 GYMName = um.GYMName,
                                 Tap_CustomerID = um.Tap_CustomerID,
                             }).FirstOrDefault();
                userlist.Add(users);
            }
            return View(userlist);
        }
        public async Task<IActionResult> ViewInvoice(string id, int sub_id, string userid, string invoiceid)
        {
            string[] result = id.Split('_').ToArray();
            if (result[0] == "chg")
            {
                //Get Charge Detail
                var users = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                var client_ChargeDetail = new HttpClient();
                var request_ChargeDetail = new HttpRequestMessage(HttpMethod.Get, "https://api.tap.company/v2/charges/" + id);
                request_ChargeDetail.Headers.Add("Authorization", "Bearer " + users.SecertKey);
                request_ChargeDetail.Headers.Add("accept", "application/json");
                var response_ChargeDetail = await client_ChargeDetail.SendAsync(request_ChargeDetail);
                var result_ChargeDetail = await response_ChargeDetail.Content.ReadAsStringAsync();
                ChargeDetail Deserialized_savecard = JsonConvert.DeserializeObject<ChargeDetail>(result_ChargeDetail);
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == sub_id).FirstOrDefault();
                var getinvoiceinfo = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoiceid)).FirstOrDefault();
                Deserialized_savecard.Subscriptions = subscriptions;
                if (Deserialized_savecard.id != null)
                {
                    if (getinvoiceinfo.ChargeResponseId > 0)
                    {
                        int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                        decimal finalamount = 0;
                        decimal Discount = 0;
                        decimal Vat = 0;
                        if (users.Frequency == "DAILY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                        }
                        else if (users.Frequency == "WEEKLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                        }
                        else if (users.Frequency == "MONTHLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                        }
                        else if (users.Frequency == "QUARTERLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                        }
                        else if (users.Frequency == "HALFYEARLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                        }
                        else if (users.Frequency == "YEARLY")
                        {
                            var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * Convert.ToDecimal(subscriptions.Discount);
                            var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                            finalamount = final_amount_percentage * 12;
                            Discount = amountpercentage * 12;
                        }
                        if (subscriptions.VAT == null || subscriptions.VAT == "0")
                        {
                            Vat = 0;
                        }
                        else
                        {
                            decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                            Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                        }
                        decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                        Deserialized_savecard.amount = Convert.ToDouble(after_vat_totalamount);
                        Deserialized_savecard.Frequency = users.Frequency;
                        Deserialized_savecard.finalamount = finalamount.ToString();
                        Deserialized_savecard.VAT = Vat.ToString();
                        Deserialized_savecard.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                        Deserialized_savecard.Created_date = Deserialized_savecard.activities.Skip(1).Select(x => x.created).FirstOrDefault();
                    }
                    else
                    {
                        int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                        decimal finalamount = 0;
                        decimal Discount = 0;
                        decimal Vat = 0;
                        if (users.Frequency == "DAILY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                        }
                        else if (users.Frequency == "WEEKLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                        }
                        else if (users.Frequency == "MONTHLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                        }
                        else if (users.Frequency == "QUARTERLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                        }
                        else if (users.Frequency == "HALFYEARLY")
                        {
                            Discount = 0;
                            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                        }
                        else if (users.Frequency == "YEARLY")
                        {
                            var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * Convert.ToDecimal(subscriptions.Discount);
                            var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                            finalamount = final_amount_percentage * 12;
                            Discount = amountpercentage * 12;
                        }
                        if (subscriptions.VAT == null || subscriptions.VAT == "0")
                        {
                            Vat = 0;
                        }
                        else
                        {
                            decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                            Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                        }
                        decimal after_vat_totalamount = finalamount + Vat;
                        Deserialized_savecard.amount = Convert.ToDouble(after_vat_totalamount);
                        Deserialized_savecard.Frequency = users.Frequency;
                        Deserialized_savecard.finalamount = finalamount.ToString();
                        Deserialized_savecard.VAT = Vat.ToString();
                        Deserialized_savecard.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                        Deserialized_savecard.Created_date = Deserialized_savecard.activities.Skip(1).Select(x => x.created).FirstOrDefault();
                    }
                    return View(Deserialized_savecard);
                }
                else
                {
                    return View(Deserialized_savecard);
                }
            }
            else
            {
                //Get Charge Detail
                ChargeDetail chargeDetail = new ChargeDetail();
                TapInvoiceResponseDTO Deserialized_savecard = null;
                var log_user = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                if (id != null && log_user.Id != null)
                {
                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://api.tap.company/v2/invoices/" + id),
                        Headers =
                        {
                            { "accept", "application/json" },
                            { "Authorization", "Bearer " + log_user.SecertKey },
                        },
                    };
                    using (var response = await client.SendAsync(request))
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        Deserialized_savecard = JsonConvert.DeserializeObject<TapInvoiceResponseDTO>(body);
                    }
                }
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == sub_id).FirstOrDefault();
                var users = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                var getinvoiceinfo = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoiceid)).FirstOrDefault();
                Deserialized_savecard.Subscriptions = subscriptions;

                if (getinvoiceinfo.ChargeResponseId > 0)
                {
                    int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                    decimal finalamount = 0;
                    decimal Discount = 0;
                    decimal Vat = 0;
                    if (users.Frequency == "DAILY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                    }
                    else if (users.Frequency == "WEEKLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                    }
                    else if (users.Frequency == "MONTHLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                    }
                    else if (users.Frequency == "QUARTERLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                    }
                    else if (users.Frequency == "HALFYEARLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                    }
                    else if (users.Frequency == "YEARLY")
                    {
                        var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * Convert.ToDecimal(subscriptions.Discount);
                        var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                        finalamount = final_amount_percentage * 12;
                        Discount = amountpercentage * 12;
                    }
                    if (subscriptions.VAT == null || subscriptions.VAT == "0")
                    {
                        Vat = 0;
                    }
                    else
                    {
                        decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                        Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                    }
                    decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                    chargeDetail.Frequency = users.Frequency;
                    chargeDetail.finalamount = finalamount.ToString();
                    chargeDetail.VAT = Vat.ToString();
                    chargeDetail.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                    chargeDetail.Subscriptions = subscriptions;
                    chargeDetail.customer = Deserialized_savecard.customer;
                    chargeDetail.reference = Deserialized_savecard.reference;
                    chargeDetail.Created_date = Deserialized_savecard.created;
                    chargeDetail.Paymentname = Deserialized_savecard.payment_methods.First();
                    chargeDetail.amount = Convert.ToDouble(after_vat_totalamount);
                    chargeDetail.Created_date = Deserialized_savecard.created;
                }
                else
                {
                    int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                    decimal finalamount = 0;
                    decimal Discount = 0;
                    decimal Vat = 0;
                    if (users.Frequency == "DAILY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
                    }
                    else if (users.Frequency == "WEEKLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
                    }
                    else if (users.Frequency == "MONTHLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
                    }
                    else if (users.Frequency == "QUARTERLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
                    }
                    else if (users.Frequency == "HALFYEARLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
                    }
                    else if (users.Frequency == "YEARLY")
                    {
                        var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * Convert.ToDecimal(subscriptions.Discount);
                        var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                        finalamount = final_amount_percentage * 12;
                        Discount = amountpercentage * 12;
                    }
                    if (subscriptions.VAT == null || subscriptions.VAT == "0")
                    {
                        Vat = 0;
                    }
                    else
                    {

                        decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                        Vat = (decimal)((totala / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                    }
                    decimal after_vat_totalamount = finalamount + Vat;
                    chargeDetail.Frequency = users.Frequency;
                    chargeDetail.finalamount = finalamount.ToString();
                    chargeDetail.VAT = Vat.ToString();
                    chargeDetail.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                    chargeDetail.Subscriptions = subscriptions;
                    chargeDetail.customer = Deserialized_savecard.customer;
                    chargeDetail.reference = Deserialized_savecard.reference;
                    chargeDetail.Created_date = Deserialized_savecard.created;
                    chargeDetail.Paymentname = Deserialized_savecard.payment_methods.First();
                    chargeDetail.amount = Convert.ToDouble(after_vat_totalamount);
                }
                return View(chargeDetail);
            }
        }
        public IActionResult ShowInvoice(string PaymentStatus)
        {
            var current_user = GetCurrentUserAsync().Result;
            if (current_user.UserType == "SuperAdmin")
            {
                if (PaymentStatus == "All")
                {
                    var invoices = _context.invoices.OrderByDescending(x => x.InvoiceStartDate).ToList();
                    return View(invoices);
                }
                else
                {
                    var invoices = _context.invoices.Where(x => x.Status == PaymentStatus).OrderByDescending(x => x.InvoiceStartDate).ToList();
                    return View(invoices);
                }
            }
            else
            {
                if (PaymentStatus == "All")
                {
                    var invoices = _context.invoices.Where(x => x.UserId == current_user.Id).OrderByDescending(x => x.InvoiceStartDate).ToList();
                    return View(invoices);
                }
                else
                {
                    var invoices = _context.invoices.Where(x => x.UserId == current_user.Id && x.Status == PaymentStatus).OrderByDescending(x => x.InvoiceStartDate).ToList();
                    return View(invoices);
                }
            }
        }
        #endregion
        #region Gym Customer Registration
        public IActionResult AddGymCustomer(int id)
        {
            ViewBag.SubscriptionList = id;
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> AddGymCustomer(ApplicationUser applicationUser)
        {

#if !DEBUG
            if (applicationUser.recaptchaToken != null ||
                applicationUser.recaptchaToken != "" ||
                applicationUser.recaptchaToken != "null" ||
                applicationUser.recaptchaToken != string.Empty)
            {
                bool isCapthcaValid = CreateAssessmentSample.ValidateCaptcha(applicationUser.recaptchaToken);
                if (ModelState.IsValid)
                {
                    if (!isCapthcaValid)
                    {
                        ModelState.AddModelError(string.Empty, "You have put wrong Captcha,Please ensure the authenticity !!!");
                    }
                }
            }
#endif
            var subid = applicationUser.SubscribeID;
            var resultuser = await _userManager.FindByEmailAsync(applicationUser.UserName);
            if (resultuser != null)
            {
                ViewBag.SubscriptionList = subid;
                ModelState.AddModelError(string.Empty, "Email Already Exist..!");
                return View(applicationUser);
            }
            var countrycode = "";
            var currencycode = "";
            if (applicationUser.Country == "Bahrain")
            {
                countrycode = "+973";
                currencycode = "BHD";
            }
            else if (applicationUser.Country == "KSA")
            {
                countrycode = "+966";
                currencycode = "SAR";
            }
            else if (applicationUser.Country == "Kuwait")
            {
                countrycode = "+965";
                currencycode = "KWD";
            }
            else if (applicationUser.Country == "UAE")
            {
                countrycode = "+971";
                currencycode = "AED";
            }
            else if (applicationUser.Country == "Qatar")
            {
                countrycode = "+974";
                currencycode = "QAR";
            }
            else if (applicationUser.Country == "Oman")
            {
                countrycode = "+968";
                currencycode = "OMR";
            }
            applicationUser.Currency = currencycode;
            var selectsub_country = _context.subscriptions.Where(x => x.SubscriptionId == applicationUser.SubscribeID).Select(x => x.Countries).FirstOrDefault();
            // save data to database
            applicationUser.Email = applicationUser.UserName;
            applicationUser.Status = true;
            applicationUser.UserType = "Customer";
            applicationUser.EmailConfirmed = true;
            applicationUser.PhoneNumberConfirmed = true;
            applicationUser.PhoneNumber = applicationUser.PhoneNumber;
            applicationUser.Password = applicationUser.Password;
            applicationUser.Tap_CustomerID = null;
            applicationUser.SubscribeID = 0;
            if (applicationUser.Password == null)
            {
                ViewBag.SubscriptionList = subid;
                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                ModelState.AddModelError(string.Empty, "Please Enter The Password...!");
            }

            if (selectsub_country == "Bahrain")
            {
                applicationUser.PublicKey = Constants.BHD_Public_Key;
                applicationUser.SecertKey = Constants.BHD_Test_Key;
                applicationUser.MarchantID = Constants.BHD_Merchant_Key;
            }
            else if (selectsub_country == "KSA")
            {
                applicationUser.PublicKey = Constants.KSA_Public_Key;
                applicationUser.SecertKey = Constants.KSA_Test_Key;
                applicationUser.MarchantID = Constants.KSA_Merchant_Key;
            }
            Guid guid = Guid.NewGuid();
            string str = guid.ToString();
            applicationUser.Id = str;
            var result = await _userManager.CreateAsync(applicationUser, applicationUser.Password);
            if (result.Succeeded)
            {
                var istrue = await _signInManager.PasswordSignInAsync(applicationUser.UserName.ToString(), applicationUser.Password, true, lockoutOnFailure: true);
                return RedirectToAction("Subscription", "Home", new { id = subid });
            }
            foreach (var error in result.Errors)
            {
                ViewBag.SubscriptionList = subid;
                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }
        [Authorize]
        public IActionResult ViewGYMCustomer()
        {
            var users = (from um in _context.Users
                         join sub in _context.subscriptions on um.SubscribeID equals sub.SubscriptionId into ps
                         from sub in ps.DefaultIfEmpty()
                         where um.Id == GetCurrentUserAsync().Result.Id
                         select new UserInfoDTO
                         {
                             Id = um.Id,
                             FullName = um.FullName,
                             Email = um.Email,
                             PhoneNumber = um.PhoneNumber,
                             Country = um.Country,
                             City = um.City,
                             Currency = um.Currency,
                             SubscribeName = sub.Name + " " + "-" + " " + "(" + sub.Amount + ")",
                             SubscribeID = um.SubscribeID,
                             Status = um.Status,
                             PaymentSource = um.PaymentSource,
                             GYMName = um.GYMName
                         });
            return View(users);
        }
#endregion

    }
}