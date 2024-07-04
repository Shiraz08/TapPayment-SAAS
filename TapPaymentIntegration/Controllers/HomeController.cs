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
using Google.Cloud.RecaptchaEnterprise.V1;
using System.Text.RegularExpressions;
using System.Security.Policy;
using Card = TapPaymentIntegration.Models.Card.Card;

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
            try
            {
                var subscriptions = _context.subscriptions.Where(x => x.Status).ToList();
                return View(subscriptions);
            }
            catch(Exception ex)
            {
                ViewBag.PageName = "Index";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
        }

        public IActionResult Error()
        {
            return View();
        }
        public IActionResult DashboardError()
        {
            return View();
        }
        public async Task<IActionResult> Subscription(int id, string link, string userid, string invoiceid)
        {
            try
            {
                if (userid != null)
                {
                    var applicationUser = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                    await _signInManager.PasswordSignInAsync(applicationUser.UserName.ToString(), applicationUser.Password, true, lockoutOnFailure: true);
                }
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    subscriptions.VAT = "0";
                }
                ViewBag.Invoiceid = invoiceid;
                return View(subscriptions);
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "Subscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
        }
        public async Task<IActionResult> SubscriptionAdmin(int id, string link, string userid, string invoiceid, string After_vat_totalamount, string isfirstinvoice)
        {
            try
            {
                if (link != null)
                {
                    var applicationUser = _context.Users.Where(x => x.Id == userid).FirstOrDefault();
                    await _signInManager.PasswordSignInAsync(applicationUser.UserName.ToString(), applicationUser.Password, true, lockoutOnFailure: true);
                }
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
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
                ViewBag.IsFirstInvoice = isfirstinvoice;
                return View(subscriptions);
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "SubscriptionAdmin";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
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
                    var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                    Random rnd = new Random();
                    var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                    var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                    var amount = TotalPlanfee.ToString();
                    var description = subscriptions.Frequency;
                    Reference reference = new Reference();
                    reference.transaction = TransNo;
                    reference.order = OrderNo;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerify/";

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
            catch (Exception ex)
            {
                ViewBag.PageName = "CreateInvoiceMada";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
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
                var IsFirstInvoice = Request.Form.Where(x => x.Key == "IsFirstInvoice").FirstOrDefault().Value.ToString();

                if(Invoiceid == null  || Invoiceid == string.Empty || Invoiceid == "")
                {
                    Invoiceid = "0";
                }
                if (IsFirstInvoice == null || IsFirstInvoice == string.Empty || IsFirstInvoice == "")
                {
                    IsFirstInvoice = "true";
                }

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
                    var subscriptions = _context.subscriptions.Where(x =>  x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
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
                        var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                        decimal totala = finalamount + (IsFirstInvoice == "true" ? Convert.ToDecimal(subscriptions.SetupFee) : 0.0m);
                        Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                    }
                    decimal after_vat_totalamount = finalamount + (IsFirstInvoice == "true" ? Convert.ToDecimal(subscriptions.SetupFee) : 0.0m) + Vat;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + Invoiceid + "&IsFirstInvoice=" + IsFirstInvoice;

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
                    item.amount = after_vat_totalamount.ToString("0.00");
                    item.currency = subscriptions.Currency;
                    items.Add(item);

                    Order order = new Order();
                    order.amount = after_vat_totalamount.ToString("0.00");
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
            catch (Exception ex)
            {
                ViewBag.PageName = "CreateInvoiceBenefit";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
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
                var IsFirstInvoice = Request.Form.Where(x => x.Key == "IsFirstInvoice").FirstOrDefault().Value.ToString();

                if (Invoiceid == null || Invoiceid == string.Empty || Invoiceid == "")
                {
                    Invoiceid = "0";
                }
                if (IsFirstInvoice == null || IsFirstInvoice == string.Empty || IsFirstInvoice == "")
                {
                    IsFirstInvoice = "true";
                }

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
                    var subscriptions = _context.subscriptions.Where(x =>  x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                    Random rnd = new Random();
                    var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                    var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                    var amount = TotalPlanfee.ToString();
                    var description = subscriptions.Frequency;
                    Reference reference = new Reference();
                    reference.transaction = TransNo;
                    reference.order = OrderNo;

                    Redirect redirect = new Redirect();
                    redirect.url = Constants.RedirectURL + "/Home/CardVerify?invoiceid=" + Invoiceid + "&IsFirstInvoice=" + IsFirstInvoice;

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
            catch (Exception ex)
            {
                ViewBag.PageName = "CreateInvoice";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
        }
        public async Task<IActionResult> CardVerify(int invoiceid, string IsFirstInvoice)
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
                    if(IsFirstInvoice == "true")
                    {
                        var Frequency = HttpContext.Session.GetString("Frequency");
                        var Invoiceid = HttpContext.Session.GetString("Invoiceid");
                        if (Deserialized_savecard.id != null)
                        {
                            if (Invoiceid == null || Invoiceid == "" || Invoiceid == "0")
                            {
                                //Create Invoice
                                var users = GetCurrentUserAsync().Result;
                                var subscriptions = _context.subscriptions.Where(x =>  x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal VatwithoutSetupFee = 0;
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
                                    var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                                    Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                                    VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
                                }
                                decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                                if (log_user.Frequency == "DAILY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(1),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }
                                else if (log_user.Frequency == "WEEKLY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(7),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }
                                else if (log_user.Frequency == "MONTHLY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }
                                else if (log_user.Frequency == "QUARTERLY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }
                                else if (log_user.Frequency == "HALFYEARLY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }
                                else if (log_user.Frequency == "YEARLY")
                                {
                                    Invoice invoices = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddYears(1),
                                        Currency = subscriptions.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = GetCurrentUserAsync().Result.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
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
                                        Country = subscriptions.Countries,
                                        IsFirstInvoice = true
                                    };
                                    _context.invoices.Add(invoices);
                                    _context.SaveChanges();
                                }

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
                                recurringCharge.Amount = Convert.ToDecimal(finalamount + VatwithoutSetupFee);
                                recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                                recurringCharge.UserID = users.Id;
                                recurringCharge.Tap_CustomerId = Deserialized_savecard.payment_agreement.contract.customer_id;
                                recurringCharge.ChargeId = tap_id;
                                recurringCharge.Invoice = "Inv" + max_invoice_id;
                                recurringCharge.IsRun = false;
                                if (log_user.Frequency == "DAILY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddDays(1);
                                }
                                else if (log_user.Frequency == "WEEKLY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddDays(7);
                                }
                                else if (log_user.Frequency == "MONTHLY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddMonths(1);
                                }
                                else if (log_user.Frequency == "QUARTERLY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddMonths(3);
                                }
                                else if (log_user.Frequency == "HALFYEARLY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddMonths(6);
                                }
                                else if (log_user.Frequency == "YEARLY")
                                {
                                    recurringCharge.JobRunDate = nextrecurringdate.AddYears(1);
                                }
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
                                var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                                //Calculate VAT
                                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                                {
                                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                                    body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                                    var without_vat = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                                }
                                else
                                {
                                    body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                                    var without_vat = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                                }

                                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                                var emailSubject = "Tamarran – Payment Receipt - " + " Inv" + invoice_info.InvoiceId;
                                _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, emailSubject, bodyemail);
                                return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
                            }
                            else
                            {
                                //Create Invoice
                                var users = GetCurrentUserAsync().Result;
                                var subscriptions = _context.subscriptions.Where(x =>  x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                                var Amount = subscriptions.Amount;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal VatwithoutSetupFee = 0;
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
                                    var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                                    Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                                    VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
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
                                recurringCharge.Amount = finalamount + VatwithoutSetupFee;
                                recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                                recurringCharge.UserID = users.Id;
                                recurringCharge.Tap_CustomerId = Deserialized_savecard.payment_agreement.contract.customer_id;
                                recurringCharge.ChargeId = tap_id;
                                recurringCharge.Invoice = "Inv" + max_invoice_id.InvoiceId.ToString();
                                recurringCharge.IsRun = false;
                                recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate;
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
                                var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                                //Calculate VAT
                                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                                {
                                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                                    body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                                }
                                else
                                {
                                    body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                                    body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                                }
                                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                                var emailSubject = "Tamarran – Payment Receipt - " + " Inv" + invoice_info.InvoiceId;
                                _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, emailSubject, bodyemail);
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
                    }
                    else
                    {
                        //Create Invoice
                        var users = GetCurrentUserAsync().Result;
                        var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                        var Amount = subscriptions.Amount;
                        int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                        decimal finalamount = 0;
                        decimal Discount = 0;
                        decimal Vat = 0;
                        decimal VatwithoutSetupFee = 0;
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
                            var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                            Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                            VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
                        }
                        decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                        var max_invoice_id = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoiceid)).FirstOrDefault();
                        var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();

                        RecurringCharge recurringCharge = new RecurringCharge();
                        recurringCharge.Amount = finalamount + VatwithoutSetupFee;
                        recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                        recurringCharge.UserID = users.Id;
                        recurringCharge.Tap_CustomerId = Deserialized_savecard.payment_agreement.contract.customer_id;
                        recurringCharge.ChargeId = tap_id;
                        recurringCharge.Invoice = "Inv" + max_invoice_id.InvoiceId.ToString();
                        recurringCharge.IsRun = false;
                        if (log_user.Frequency == "DAILY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(1);
                        }
                        else if (log_user.Frequency == "WEEKLY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(7);
                        }
                        else if (log_user.Frequency == "MONTHLY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(1);
                        }
                        else if (log_user.Frequency == "QUARTERLY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(3);
                        }
                        else if (log_user.Frequency == "HALFYEARLY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(6);
                        }
                        else if (log_user.Frequency == "YEARLY")
                        {
                            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddYears(1);
                        }
                        _context.recurringCharges.Add(recurringCharge);
                        _context.SaveChanges();

                        Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoiceid)).FirstOrDefault();
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
                        var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                        body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                        //Calculate VAT
                        if (subscriptions.VAT == null || subscriptions.VAT == "0")
                        {
                            body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                            body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
                            body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                        }
                        else
                        {
                            body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                            body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                            body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                        }
                        var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                        var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                        var emailSubject = "Tamarran – Payment Receipt - " + " Inv" + invoice_info.InvoiceId;
                        _ = _emailSender.SendEmailWithFIle(bytes, userinfo.Email, emailSubject, bodyemail);
                        return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
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
            catch (Exception ex)
            {
                ViewBag.PageName = "CardVerify";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
        }

        public async Task<IActionResult> CardVerifyBenefit(int invoiceid, string IsFirstInvoice)
        {
            try
            {
                var users = new ApplicationUser();
                var subscriptions = new Subscriptions();
                if (invoiceid == 0)
                {
                    users = GetCurrentUserAsync().Result;
                    var SubscriptionId = HttpContext.Session.GetString("SubscriptionId");
                    subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(SubscriptionId)).FirstOrDefault();
                }
                else
                {
                    var invoice_info = _context.invoices.FirstOrDefault(a=>a.InvoiceId == invoiceid);
                    users = _context.Users.FirstOrDefault(a => a.Id == invoice_info.UserId); 
                    subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == invoice_info.SubscriptionId).FirstOrDefault();

                }
                
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
                    var amountpercentage = (decimal)(Convert.ToDecimal(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(subscriptions.VAT));
                }
                decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;

                if (IsFirstInvoice == "true")
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
                            var bodyy = await response.Content.ReadAsStringAsync();
                            Deserialized_savecard = JsonConvert.DeserializeObject<TapInvoiceResponseDTO>(bodyy);
                        }
                    }
                    var SubscriptionId = HttpContext.Session.GetString("SubscriptionId");
                    int getchargesresposemodel = _context.chargeResponses.Max(x => x.ChargeResponseId);
                    if (Deserialized_savecard.status == "PAID")
                    {
                        if(invoiceid == 0)
                        {
                            if (log_user.Frequency == "DAILY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddDays(1),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }
                            else if (log_user.Frequency == "WEEKLY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddDays(7),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }
                            else if (log_user.Frequency == "MONTHLY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }
                            else if (log_user.Frequency == "QUARTERLY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }
                            else if (log_user.Frequency == "HALFYEARLY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }
                            else if (log_user.Frequency == "YEARLY")
                            {
                                Invoice invoices = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddYears(1),
                                    Currency = subscriptions.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = GetCurrentUserAsync().Result.FullName,
                                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                                    IsDeleted = false,
                                    VAT = Vat.ToString("0.00"),
                                    Discount = Discount.ToString("0.00"),
                                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                                    SubscriptionName = subscriptions.Name,
                                    UserId = users.Id,
                                    ChargeId = tap_id,
                                    GymName = users.GYMName,
                                    Country = subscriptions.Countries,
                                    Status = "Payment Captured",
                                    ChargeResponseId = getchargesresposemodel,
                                    IsFirstInvoice = true
                                };
                                _context.invoices.Add(invoices);
                                _context.SaveChanges();
                            }

                            invoiceid = _context.invoices.Max(a => a.InvoiceId);
                        }
                        else
                        {
                            Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == invoiceid).FirstOrDefault();

                            invoice_info.Status = "Payment Captured";
                            invoice_info.ChargeResponseId = getchargesresposemodel;
                            invoice_info.ChargeId = tap_id;
                            _context.invoices.Update(invoice_info);
                            _context.SaveChanges();
                        }
                        var Frequency = HttpContext.Session.GetString("Frequency");
                        //var Invoiceid = log_user.Benefit_Invoice;
                        if (Deserialized_savecard.id != null)
                        {
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

                            DateTime nextrecurringdate = _context.invoices.Where(x => x.InvoiceId == invoiceid).Select(x => x.InvoiceEndDate).FirstOrDefault();
                            RecurringCharge recurringCharge = new RecurringCharge();
                            recurringCharge.Amount = Convert.ToDecimal(finalamount + Vat);
                            recurringCharge.SubscriptionId = subscriptions.SubscriptionId;
                            recurringCharge.UserID = users.Id;
                            recurringCharge.Tap_CustomerId = Deserialized_savecard.customer.id;
                            recurringCharge.ChargeId = tap_id;
                            recurringCharge.Invoice = "Inv" + invoiceid;
                            recurringCharge.IsRun = false;
                            if (log_user.Frequency == "DAILY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddDays(1);
                            }
                            else if (log_user.Frequency == "WEEKLY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddDays(7);
                            }
                            else if (log_user.Frequency == "MONTHLY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddMonths(1);
                            }
                            else if (log_user.Frequency == "QUARTERLY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddMonths(3);
                            }
                            else if (log_user.Frequency == "HALFYEARLY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddMonths(6);
                            }
                            else if (log_user.Frequency == "YEARLY")
                            {
                                recurringCharge.JobRunDate = nextrecurringdate.AddYears(1);
                            }
                            _context.recurringCharges.Add(recurringCharge);
                            _context.SaveChanges();
                        }
                        else
                        {
                            //Update Charge Response;
                            var chargeresponse = _context.chargeResponses.Where(x => x.ChargeResponseId == getchargesresposemodel).FirstOrDefault();
                            _context.chargeResponses.Remove(chargeresponse);
                            _context.SaveChanges();
                        }
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
                else
                {
                    Invoice invoice_info = _context.invoices.Where(x => x.InvoiceId == invoiceid).FirstOrDefault();

                    invoice_info.Status = "Payment Captured";
                    _context.invoices.Update(invoice_info);
                    _context.SaveChanges();
                }

                // Send Email
                string body = string.Empty;
                _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string contentRootPath = _environment.WebRootPath + "/htmltopdf.html";
                string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";

                //Generate PDF
                using (StreamReader reader = new StreamReader(contentRootPath))
                {
                    body = reader.ReadToEnd();
                }
                //Fill EMail By Parameter
                body = body.Replace("{title}", "Tamarran Payment Invoice");
                body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                body = body.Replace("{InvocieStatus}", "Payment Captured");
                body = body.Replace("{InvoiceID}", "Inv" + invoiceid);

                body = body.Replace("{User_Name}", users.FullName);
                body = body.Replace("{User_Email}", users.Email);
                body = body.Replace("{User_GYM}", users.GYMName);
                body = body.Replace("{User_Phone}", users.PhoneNumber);


                body = body.Replace("{SubscriptionName}", subscriptions.Name);
                body = body.Replace("{Discount}", Discount.ToString());
                body = body.Replace("{SubscriptionPeriod}", users.Frequency);
                body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
                var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                //Calculate VAT
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                    body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                }
                else
                {
                    body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                }

                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var bodyemail = EmailBodyFill.EmailBodyForPaymentReceipt(users, subscriptions);
                var emailSubject = "Tamarran - Tamarran – Payment Receipt - " + " Inv" + invoiceid;
                _ = _emailSender.SendEmailWithFIle(bytes, users.Email, emailSubject, bodyemail);
                return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "CardVerifyBenefit";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("Error");
            }
        }

        #endregion
        #region Admin Dashboard

        [Authorize]
        public IActionResult Dashboard()
        {
            try
            {
                ViewBag.CustomerCount = _userManager.Users.Where(x => x.Status == true).ToList().Count();
                ViewBag.InvoiceCount = _context.invoices.Where(x => x.Status == "Payment Captured").ToList().Count();
                ViewBag.ChangeCardCount = _context.changeCardInfos.ToList().Count();
                ViewBag.SubscriptionCount = _context.subscriptions.Where(x => x.Status == true).ToList().Count();
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "Dashboard";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        //Customer Section
        [Authorize]
        public IActionResult ViewCustomer()
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult ViewAllInvoices(string userId)
        {
            try
            {
                var incoices = _context.invoices.Where(x => x.UserId == userId).ToList();
                return View(incoices);
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewAllInvoices";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult AddCustomer()
        {
            try
            {

                ViewBag.SubscriptionList = _context.subscriptions.Select(x => new SelectListItem { Value = x.SubscriptionId.ToString(), Text = x.Name + " " + "-" + " " + x.Amount });
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "AddCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        [HttpPost]
        public async Task<IActionResult> AddCustomer(ApplicationUser applicationUser)
        {
            try
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
                    if (applicationUser.Frequency == "DAILY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddDays(1),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    }
                    else if (applicationUser.Frequency == "WEEKLY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddDays(7),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    }
                    else if (applicationUser.Frequency == "MONTHLY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    }
                    else if (applicationUser.Frequency == "QUARTERLY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    }
                    else if (applicationUser.Frequency == "HALFYEARLY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    }
                    else if (applicationUser.Frequency == "YEARLY")
                    {
                        Invoice invoices = new Invoice()
                        {
                            InvoiceStartDate = DateTime.UtcNow,
                            InvoiceEndDate = DateTime.UtcNow.AddYears(1),
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
                            Country = subscriptions.Countries,
                            IsFirstInvoice = true
                        };
                        _context.invoices.Add(invoices);
                        _context.SaveChanges();
                    } 


                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                    applicationUser.Benefit_Invoice = max_invoice_id.ToString();

                    string max_user_id = _context.Users.Where(x => x.Email == applicationUser.Email).Select(x => x.Id).FirstOrDefault();
                    //Create Invoice
                    int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

                    InvoiceHelper.GetDiscountAndFinalAmountBySubscriptionFrequency(applicationUser.Frequency, subscriptions.Amount, subscriptions.Discount, days, out decimal discount, out decimal finalAmount);
                    InvoiceHelper.CalculdateInvoiceDetails(finalAmount, subscriptions, out string subscriptionAmount, out decimal after_vat_totalamount, out decimal vat, out string vat_str, out string total, out string invoiceAmount, out string Totalinvoicewithoutvat);

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
                    var callbackUrl = @Url.Action("SubscriptionAdmin", "Home", new { id = applicationUser.SubscribeID, link = "Yes", userid = max_user_id, invoiceid = max_invoice_id, After_vat_totalamount = after_vat_totalamount, isfirstinvoice = "true" });
                    var websiteurl = HtmlEncoder.Default.Encode(Constants.RedirectURL + callbackUrl);

                    var emailSubject = "Tamarran – Payment Request - " + " Inv" + max_invoice_id; 
                    var bodyemail = EmailBodyFill.EmailBodyForPaymentRequest(applicationUser, websiteurl);
                    _ = _emailSender.SendEmailWithFIle(bytes, applicationUser.Email, emailSubject, bodyemail);


                    var adduser = _context.Users.Where(x => x.Email == applicationUser.Email).FirstOrDefault();
                    var invoiceinfo = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                    invoiceinfo.InvoiceLink = Constants.RedirectURL + callbackUrl;
                    invoiceinfo.VAT = vat.ToString();
                    invoiceinfo.Discount = discount.ToString();
                    invoiceinfo.AddedBy = "Super Admin";
                    invoiceinfo.UserId = adduser.Id;
                    invoiceinfo.SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00"));
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
            catch (Exception ex)
            {
                ViewBag.PageName = "AddCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public ActionResult DeleteCustomer(string userId)
        {
            try
            {
                var result = _context.Users.Where(x => x.Id == userId).FirstOrDefault();
                if (result != null)
                {
                    _context.Remove(result);
                    _context.SaveChanges();
                }
                return RedirectToAction("ViewCustomer", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "DeleteCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public ActionResult ViewNextPayment(string userId)
        {
            try
            {
                var users = (from um in _context.Users
                             join rc in _context.recurringCharges on um.Id equals rc.UserID
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
                                 UserType = um.UserType,
                                 IsJOnRun = rc.IsRun,
                                 JobRunDate = rc.JobRunDate,
                                 Amount = rc.Amount,
                                 InvoiceNo = rc.Invoice,
                                 RecurringId = rc.RecurringChargeId.ToString(),
                                 IsFreze = rc.IsFreeze
                             });
                return View(users.Where(x=>x.Id == userId).ToList());
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public ActionResult FreezeRecurring(string userId)
        {
            var res = _context.recurringCharges.Where(x => x.RecurringChargeId == Convert.ToInt32(userId)).FirstOrDefault();
            res.IsFreeze = true;
            _context.recurringCharges.Update(res);
            _context.SaveChanges();

            //Send Email
            var users = _context.Users.Where(x => x.Id == res.UserID).FirstOrDefault();
            var bodyemail = EmailBodyFill.EmailBodyForPauseSubscription(users);
            var emailSubject = "Tamarran – Subscription Paused";
            _ = _emailSender.SendEmailAsync(users.Email, emailSubject, bodyemail);

            return RedirectToAction("ViewNextPayment","Home", new { userId  = res.UserID});
        }

        [HttpPost]
        public ActionResult ResumeRecurring(string Recurringid, string resumedate)
        {
            var res = _context.recurringCharges.Where(x => x.RecurringChargeId == Convert.ToInt32(Recurringid)).FirstOrDefault();
            res.IsFreeze = false;
            res.JobRunDate = Convert.ToDateTime(resumedate);
            _context.recurringCharges.Update(res);
            _context.SaveChanges();

            //Send Email
            var users = _context.Users.Where(x => x.Id == res.UserID).FirstOrDefault();
            var bodyemail = EmailBodyFill.EmailBodyForResumeSubscription(users);
            var emailSubject = "Tamarran – Subscription Resume - " + res.Invoice;
            _ = _emailSender.SendEmailAsync(users.Email, emailSubject, bodyemail);

            return RedirectToAction("ViewNextPayment", "Home", new { userId = res.UserID });
        }
        public async Task<IActionResult> InActiveUser(string id)
        {
            try
            {
                var subscriptions = _context.Users.Where(x => x.Id == id).FirstOrDefault();
                await _userManager.UpdateSecurityStampAsync(subscriptions);
                subscriptions.Status = false;
                _context.Users.Update(subscriptions);
                _context.SaveChanges();
                return RedirectToAction("ViewCustomer", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "InActiveUser";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult ActiveUser(string id)
        {
            try
            {
                var subscriptions = _context.Users.Where(x => x.Id == id).FirstOrDefault();
                subscriptions.Status = true;
                _context.Users.Update(subscriptions);
                _context.SaveChanges();
                return RedirectToAction("ViewCustomer", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ActiveUser";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public ActionResult DeleteInvoice(int id, string userid, string status)
        {
            try
            {
                var result = _context.invoices.Where(x => x.InvoiceId == id && x.UserId == userid).FirstOrDefault();
                _context.invoices.Remove(result);
                _context.SaveChanges();
                return RedirectToAction("CreateInvoice", "Home", new { PaymentStatus = "All" });
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "DeleteInvoice";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        //Subscription Section
        [Authorize]
        public IActionResult Viewsubscription()
        {
            try
            {
                var subscriptions = _context.subscriptions.ToList();
                return View(subscriptions);
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "Viewsubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }

        }
        public ActionResult Deletesubscription(string userId)
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "Deletesubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult Addsubscription()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Addsubscription(Subscriptions subscription, string[] Frequency)
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "Addsubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult Editsubscription(string userId)
        {
            try
            {
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(userId)).FirstOrDefault();
                var s = string.Join(",", subscriptions.Frequency.Split(',').Select(x => string.Format("'{0}'", x)));
                ViewBag.Fre = s;
                return View(subscriptions);
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "Editsubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        [HttpPost]
        public IActionResult Editsubscription(Subscriptions subscription, string[] Frequency)
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "Editsubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        [HttpPost]
        public ActionResult getFrequency()
        {
            try
            {
                var Currenturl = Request.Form.Where(x => x.Key == "Frequency").FirstOrDefault().Value.ToString();
                return Json(_context.subscriptions.Select(x => new
                {
                    SubscriptionId = x.SubscriptionId,
                    Frequency = x.Frequency
                }).Where(x => x.SubscriptionId == Convert.ToInt32(Currenturl)).FirstOrDefault());
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "getFrequency";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult InActiveSubscription(int id)
        {
            try
            {
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
                subscriptions.Status = false;
                _context.subscriptions.Update(subscriptions);
                _context.SaveChanges();
                return RedirectToAction("Viewsubscription", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "InActiveSubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult ActiveSubscription(int id)
        {
            try
            {
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == id).FirstOrDefault();
                subscriptions.Status = true;
                _context.subscriptions.Update(subscriptions);
                _context.SaveChanges();
                return RedirectToAction("Viewsubscription", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ActiveSubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public IActionResult GetAllCharges()
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "GetAllCharges";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public ActionResult UnSubscribeSubscription(string id)
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "UnSubscribeSubscription";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        //List Section
        public ActionResult ViewSubinfo()
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewSubinfo";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        [HttpPost]
        public ActionResult SaveInvoiceManually(string emailinvoice, string remarks)
        {
            try
            {
                Match emailinvoicematch = Regex.Match(emailinvoice, @"([A-Za-z]+)(\d+)");
                var ev = emailinvoicematch.Groups[2].Value.ToString();

                var invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(ev)).FirstOrDefault();
                if (invoice == null)
                {
                    return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All", messages = "This invoice does not exist." });
                }
                if (invoice.Status == "Payment Captured")
                {
                    return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All", messages = "This invoice has already been paid." });
                }

                var max_invoice_id = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoice.InvoiceId)).FirstOrDefault();
                var users = _context.Users.Where(x => x.Id == max_invoice_id.UserId).FirstOrDefault();
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(invoice.SubscriptionId)).FirstOrDefault();
                var Amount = subscriptions.Amount;
                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                decimal finalamount = 0;
                decimal Discount = 0;
                decimal Vat = 0;
                decimal VatwithoutSetupFee = 0;
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
                    var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
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
                    Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                    VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
                }
                decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();
                //update user 
                users.Tap_CustomerID = null;
                users.Tap_Card_ID = null;
                users.SubscribeID = Convert.ToInt32(invoice.SubscriptionId);
                users.Tap_Agreement_ID = null;
                users.PaymentSource = null;
                users.First_Six = null;
                users.Last_Four = null;
                _context.Users.Update(users);
                _context.SaveChanges();

                UserSubscriptions userSubscriptions = new UserSubscriptions();
                userSubscriptions.SubID = Convert.ToInt32(invoice.SubscriptionId);
                userSubscriptions.Userid = users.Id;
                _context.userSubscriptions.Add(userSubscriptions);
                _context.SaveChanges();


                RecurringCharge recurringCharge = new RecurringCharge();
                recurringCharge.Amount = Convert.ToDecimal(invoice.SubscriptionAmount);
                recurringCharge.SubscriptionId = invoice.SubscriptionId;
                recurringCharge.UserID = users.Id;
                recurringCharge.Tap_CustomerId = null;
                recurringCharge.ChargeId = null;
                recurringCharge.Invoice = "Inv" + max_invoice_id.InvoiceId;
                recurringCharge.IsRun = false;
                recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate;
                //if (users.Frequency == "DAILY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(1);
                //}
                //else if (users.Frequency == "WEEKLY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(7);
                //}
                //else if (users.Frequency == "MONTHLY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(1);
                //}
                //else if (users.Frequency == "QUARTERLY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(3);
                //}
                //else if (users.Frequency == "HALFYEARLY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(6);
                //}
                //else if (users.Frequency == "YEARLY")
                //{
                //    recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddYears(1);
                //}
                _context.recurringCharges.Add(recurringCharge);
                _context.SaveChanges();

                invoice.Remarks = remarks;
                invoice.ChargeId = "";
                invoice.PaidBy = "Manual";
                invoice.UserId = users.Id;
                invoice.Status = "Payment Captured";
                invoice.ChargeResponseId = 0;
                _context.invoices.Update(invoice);
                _context.SaveChanges();

                // Send Email
                string body = string.Empty;
                _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string contentRootPath = _environment.WebRootPath + "/htmltopdf.html";
                string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                //Generate PDF
                var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(invoice.SubscriptionId)).FirstOrDefault();
                using (StreamReader reader = new StreamReader(contentRootPath))
                {
                    body = reader.ReadToEnd();
                }
                //Fill EMail By Parameter
                body = body.Replace("{title}", "Tamarran Payment Invoice");
                body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("dd-MM-yyyy"));

                body = body.Replace("{InvocieStatus}", "Payment Captured");
                body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.InvoiceId.ToString());


                body = body.Replace("{User_Name}", users.FullName);
                body = body.Replace("{User_Email}", users.Email);
                body = body.Replace("{User_GYM}", users.GYMName);
                body = body.Replace("{User_Phone}", users.PhoneNumber);


                body = body.Replace("{SubscriptionName}", subscriptions.Name);
                body = body.Replace("{Discount}", Discount.ToString());
                body = body.Replace("{SubscriptionPeriod}", userinfo.Frequency);
                body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
                var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                //Calculate VAT
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                    body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                }
                else
                {
                    body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
                    var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
                    body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
                }
                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var bodyemail = EmailBodyFill.EmailBodyForManuallyPaymentReceipt(users, subscriptions);
                var emailSubject = "Tamarran – Payment Receipt - " + " Inv" + max_invoice_id.InvoiceId.ToString();
                _ = _emailSender.SendEmailWithFIle(bytes, users.Email, emailSubject, bodyemail);
                return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewInvoice";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return RedirectToAction("ShowInvoice", "Home", new { PaymentStatus = "All" });
            }
        }
        static string ExtractInvoiceId(string url)
        {
            // Define a regular expression pattern to match the invoice ID
            string pattern = @"inv_[A-Za-z0-9]+";

            // Create a Regex object
            Regex regex = new Regex(pattern);

            // Match the pattern in the URL
            Match match = regex.Match(url);

            // Check if a match is found
            if (match.Success)
            {
                // Extract and return the matched invoice ID
                return match.Value;
            }
            else
            {
                // If no match is found, return null or handle the case as needed
                return null;
            }
        }
        public async Task<IActionResult> ViewInvoice(string id, int sub_id, string userid, string invoiceid)
        {
            try
            {
                if(!string.IsNullOrEmpty(id))
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
                        Deserialized_savecard.IsFirstInvoice = getinvoiceinfo.IsFirstInvoice;
                        Deserialized_savecard.gymname = users.GYMName;
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
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(subscriptions.VAT));
                                }
                                decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                                Deserialized_savecard.amount = Convert.ToDouble(after_vat_totalamount);
                                Deserialized_savecard.Frequency = users.Frequency;
                                Deserialized_savecard.finalamount = finalamount.ToString();
                                Deserialized_savecard.VAT = Vat.ToString();
                                Deserialized_savecard.remarks = string.IsNullOrEmpty(getinvoiceinfo.Remarks) ? "------" : getinvoiceinfo.Remarks;
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
                                    decimal totala = finalamount;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(subscriptions.VAT));
                                }
                                decimal after_vat_totalamount = finalamount + Vat;
                                Deserialized_savecard.amount = Convert.ToDouble(after_vat_totalamount);
                                Deserialized_savecard.Frequency = users.Frequency;
                                Deserialized_savecard.finalamount = finalamount.ToString();
                                Deserialized_savecard.VAT = Vat.ToString();
                                Deserialized_savecard.remarks = string.IsNullOrEmpty(getinvoiceinfo.Remarks) ? "------" : getinvoiceinfo.Remarks;
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
                                Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                            }
                            decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
                            chargeDetail.Frequency = users.Frequency;
                            chargeDetail.finalamount = finalamount.ToString();
                            chargeDetail.VAT = Vat.ToString();
                            chargeDetail.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                            chargeDetail.IsFirstInvoice = getinvoiceinfo.IsFirstInvoice;
                            chargeDetail.Subscriptions = subscriptions;
                            chargeDetail.gymname = users.GYMName;
                            chargeDetail.customer = Deserialized_savecard.customer;
                            chargeDetail.reference = Deserialized_savecard.reference;
                            chargeDetail.remarks = string.IsNullOrEmpty(getinvoiceinfo.Remarks) ? "------" : getinvoiceinfo.Remarks;
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

                                decimal totala = finalamount;
                                Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
                            }
                            decimal after_vat_totalamount = finalamount + Vat;
                            chargeDetail.Frequency = users.Frequency;
                            chargeDetail.finalamount = finalamount.ToString();
                            chargeDetail.VAT = Vat.ToString();
                            chargeDetail.InvoiceID = getinvoiceinfo.InvoiceId.ToString();
                            chargeDetail.Subscriptions = subscriptions;
                            chargeDetail.IsFirstInvoice = getinvoiceinfo.IsFirstInvoice;
                            chargeDetail.gymname = users.GYMName;
                            chargeDetail.remarks = string.IsNullOrEmpty(getinvoiceinfo.Remarks) ? "------" : getinvoiceinfo.Remarks;
                            chargeDetail.customer = Deserialized_savecard.customer;
                            chargeDetail.reference = Deserialized_savecard.reference;
                            chargeDetail.Created_date = Deserialized_savecard.created;
                            chargeDetail.Paymentname = Deserialized_savecard.payment_methods.First();
                            chargeDetail.amount = Convert.ToDouble(after_vat_totalamount);
                        }
                        return View(chargeDetail);
                    }
                }
                else
                {
                    var invoiceinfo = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoiceid)).FirstOrDefault();
                    var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == invoiceinfo.SubscriptionId).FirstOrDefault();
                    var userinfo = _context.Users.Where(x => x.Id == invoiceinfo.UserId).FirstOrDefault();
                    Random generator = new Random();
                    String o = "Ord_" + generator.Next(0, 1000000).ToString("D6");
                    String t = "Txn_" + generator.Next(0, 1000000).ToString("D6");

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

                    int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                    decimal finalamount = 0;
                    decimal Discount = 0;
                    decimal Vat = 0;
                    if (userinfo.Frequency == "DAILY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(sub_info.Amount) / (int)days;
                    }
                    else if (userinfo.Frequency == "WEEKLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(sub_info.Amount) / 4;
                    }
                    else if (userinfo.Frequency == "MONTHLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)Convert.ToInt32(sub_info.Amount);
                    }
                    else if (userinfo.Frequency == "QUARTERLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(sub_info.Amount) * 3) / 1;
                    }
                    else if (userinfo.Frequency == "HALFYEARLY")
                    {
                        Discount = 0;
                        finalamount = (decimal)(Convert.ToInt32(sub_info.Amount) * 6) / 1;
                    }
                    else if (userinfo.Frequency == "YEARLY")
                    {
                        var amountpercentage = (decimal)(Convert.ToInt32(sub_info.Amount) / 100) * Convert.ToDecimal(sub_info.Discount);
                        var final_amount_percentage = Convert.ToInt32(sub_info.Amount) - amountpercentage;
                        finalamount = final_amount_percentage * 12;
                        Discount = amountpercentage * 12;
                    }
                    if (sub_info.VAT == null || sub_info.VAT == "0")
                    {
                        Vat = 0;
                    }
                    else
                    {
                        decimal totala = finalamount + Convert.ToDecimal(sub_info.SetupFee);
                        Vat = (decimal)((totala / 100) * Convert.ToDecimal(sub_info.VAT));
                    }
                    decimal after_vat_totalamount = finalamount + Convert.ToDecimal(sub_info.SetupFee) + Vat;

                    var chargeDetail = new ChargeDetail
                    {
                        id = "ch_1Gq2Gi2eZvKYlo2C0uZR0k4j",
                        @object = "charge",
                        live_mode = false,
                        customer_initiated = true,
                        api_version = "2020-08-27",
                        method = "Manually",
                        gymname = userinfo.GYMName,
                        status = "succeeded",
                        amount = Convert.ToDouble(after_vat_totalamount),
                        IsFirstInvoice = invoiceinfo.IsFirstInvoice,
                        currency = "usd",
                        threeDSecure = true,
                        card_threeDSecure = true,
                        save_card = false,
                        merchant_id = "merchant_12345",
                        product = "Membership",
                        statement_descriptor = "XYZ Gym Membership",
                        description = "Monthly gym membership fee",
                        reference = new Reference { order =o, transaction = t},
                        security = new Security
                        {
                            threeDSecure = new ThreeDSecure
                            {
                                id = "3ds_1Gq2Gi2eZvKYlo2C0uZR0k4j",
                                status = "authenticated"
                            }
                        },
                        card = null,
                        receipt = new Receipt { /* Fill receipt properties here */ },
                        customer = new Customer 
                        { 
                        id= userinfo.Id,
                        phone = new Phone { country_code = countrycode, number= userinfo.PhoneNumber},
                        email = userinfo.Email
                        },
                        merchant = new Merchant { /* Fill merchant properties here */ },
                        source = new Source { /* Fill source properties here */ },
                        redirect = new Redirect { /* Fill redirect properties here */ },
                        post = new Post { /* Fill post properties here */ },
                        auto_reversed = false,
                        Subscriptions = sub_info,
                        Frequency = "monthly",
                        finalamount = finalamount.ToString(),
                        VAT = Vat.ToString(),
                        InvoiceID = invoiceinfo.InvoiceId.ToString(),
                        Paymentname = "Manually",
                        remarks = string.IsNullOrEmpty(invoiceinfo.Remarks) ? "------" : invoiceinfo.Remarks,
                        Created_date = ConvertToUnixTimeMilliseconds(invoiceinfo.AddedDate)
                };
                    return View(chargeDetail);
                }
            }
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewInvoice";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        public static long ConvertToUnixTimeMilliseconds(DateTime dateTime)
        {
            DateTimeOffset dateTimeOffset = new DateTimeOffset(dateTime);
            return dateTimeOffset.ToUnixTimeMilliseconds();
        }
        public IActionResult ShowInvoice(string PaymentStatus, string messages = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(messages))
                {
                    ViewBag.MessageError = messages;
                }
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
            catch (Exception ex)
            {
                ViewBag.PageName = "ShowInvoice";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
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
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "AddGymCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
        [Authorize]
        public IActionResult ViewGYMCustomer()
        {
            try
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
            catch (Exception ex)
            {
                ViewBag.PageName = "ViewGYMCustomer";
                ViewBag.Message = ex.Message;
                ViewBag.Details = ex.StackTrace;
                return View("DashboardError");
            }
        }
#endregion

    }
}