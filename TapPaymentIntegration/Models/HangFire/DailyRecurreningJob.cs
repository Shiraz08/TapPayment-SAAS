using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Controllers;
using TapPaymentIntegration.Data;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.Email;
using TapPaymentIntegration.Models.InvoiceDTO;
using TapPaymentIntegration.Models.PaymentDTO;
using TapPaymentIntegration.Models.Subscription;
using TapPaymentIntegration.Models.UserDTO;
using TapPaymentIntegration.Utility;
using ApplicationUser = TapPaymentIntegration.Areas.Identity.Data.ApplicationUser;
using Order = TapPaymentIntegration.Models.InvoiceDTO.Order;

namespace TapPaymentIntegration.Models.HangFire
{
    public class DailyRecurreningJob
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private TapPaymentIntegrationContext _context;
        private readonly IUserStore<ApplicationUser> _userStore;
        private IWebHostEnvironment _environment;
        EmailSender _emailSender = new EmailSender();

        public DailyRecurreningJob(IWebHostEnvironment Environment, ILogger<HomeController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, TapPaymentIntegrationContext context, IUserStore<ApplicationUser> userStore)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _userStore = userStore;
            _environment = Environment;
        }
        public async Task AutoChargeJob()
        {
            CreateCharge deserialized_CreateCharge = null;
            var recurringCharges_list = _context.recurringCharges.Where(x => x.JobRunDate.Date == DateTime.UtcNow.Date && x.IsRun == false && x.IsFreeze != true && x.ChargeId != null).ToList();
            foreach (var item in recurringCharges_list)
            {
                string[] result = item.ChargeId.Split('_').ToArray();
                if (result[0] == "chg")
                {
                    var getsubinfo = _context.subscriptions.Where(x => x.SubscriptionId == item.SubscriptionId).FirstOrDefault();
                    var getuserinfo = _context.Users.Where(x => x.Id == item.UserID).FirstOrDefault();
                    if (getuserinfo != null)
                    {
                        if (getuserinfo.SubscribeID > 0 && getuserinfo.Status == true)
                        {
                            string user_Email = getuserinfo.Email;
                            string attachmentTitle = $"{getuserinfo.FullName}_Invoice_Details";

                            //Save Code and get token
                            var client_savecard = new HttpClient();
                            var request_savecard = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/tokens");
                            request_savecard.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                            request_savecard.Headers.Add("accept", "application/json");
                            var content_savecard = new StringContent("\r\n{\r\n  \"saved_card\": {\r\n    \"card_id\": \"" + getuserinfo.Tap_Card_ID + "\",\r\n    \"customer_id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  }\r\n}\r\n", null, "application/json");
                            request_savecard.Content = content_savecard;
                            var response_savecard = await client_savecard.SendAsync(request_savecard);
                            var result_savecard = await response_savecard.Content.ReadAsStringAsync();
                            Token Deserialized_savecard = JsonConvert.DeserializeObject<Token>(result_savecard);
                            int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                            Random rnd = new Random();
                            var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                            var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);
                            //Create Invoice 
                            InvoiceHelper.DailyRecurringJob_AutoChargeJobTotalCalculation(getuserinfo, getsubinfo, days, out decimal finalamount, out decimal Discount, out decimal Vat, out decimal sun_amount, out decimal after_vat_totalamount);

                            if (getuserinfo.Frequency == "DAILY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://test.com/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(1),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        Currency = getsubinfo.Currency,
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Payment Captured",
                                        IsDeleted = false,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate;
                                    recurringCharge.Invoice = "Inv" + max_invoice_id.ToString();
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);


                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;

                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.00" + " " + getsubinfo.Currency);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - "+" Inv" + incoice_info.InvoiceId;
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(1),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        Currency = getsubinfo.Currency,
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Un-Paid",
                                        IsDeleted = false,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                            else if (getuserinfo.Frequency == "WEEKLY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://1f3b186efe31e8696c144578816c5443.m.pipedream.net/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(7),
                                        Currency = getsubinfo.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Payment Captured",
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id.ToString();
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();

                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.ToString());


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;

                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.00" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00").ToString() + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }

                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - " + " Inv" + incoice_info.InvoiceId;
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddDays(7),
                                        Currency = getsubinfo.Currency,
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Un-Paid",
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                            else if (getuserinfo.Frequency == "MONTHLY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://1f3b186efe31e8696c144578816c5443.m.pipedream.net/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Currency = getsubinfo.Currency,
                                        Status = "Payment Captured",
                                        IsDeleted = false,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    _context.recurringCharges.Add(recurringCharge);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id.ToString();
                                    _context.SaveChanges();

                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.ToString());


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);
                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }

                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Currency = getsubinfo.Currency,
                                        Status = "Un-Paid",
                                        IsDeleted = false,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                            else if (getuserinfo.Frequency == "QUARTERLY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://1f3b186efe31e8696c144578816c5443.m.pipedream.net/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        Currency = getsubinfo.Currency,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Payment Captured",
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal( after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id.ToString();
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();

                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.ToString());

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }

                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        Currency = getsubinfo.Currency,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Un-Paid",
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                            else if (getuserinfo.Frequency == "HALFYEARLY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://1f3b186efe31e8696c144578816c5443.m.pipedream.net/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
                                        AddedDate = DateTime.UtcNow,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        Currency = getsubinfo.Currency,
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Payment Captured",
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();

                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.ToString());


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);
                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }

                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
                                        AddedDate = DateTime.UtcNow,
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        Currency = getsubinfo.Currency,
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Un-Paid",
                                        IsDeleted = false,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                            else if (getuserinfo.Frequency == "YEARLY")
                            {
                                // Create a charge
                                var client = new HttpClient();
                                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tap.company/v2/charges");
                                request.Headers.Add("Authorization", "Bearer " + getuserinfo.SecertKey);
                                request.Headers.Add("accept", "application/json");
                                var content = new StringContent("\r\n{\r\n  \"amount\": " + after_vat_totalamount.ToString("0.00") + ",\r\n  \"currency\": \"" + getsubinfo.Currency + "\",\r\n  \"customer_initiated\": false,\r\n  \"threeDSecure\": true,\r\n  \"save_card\": false,\r\n  \"payment_agreement\": {\r\n    \"contract\": {\r\n      \"id\": \"" + getuserinfo.Tap_Card_ID + "\"\r\n    },\r\n    \"id\": \"" + getuserinfo.Tap_Agreement_ID + "\"\r\n  },\r\n  \"receipt\": {\r\n    \"email\": true,\r\n    \"sms\": true\r\n  },\"reference\": {\r\n    \"transaction\": \"" + TransNo + "\",\r\n    \"order\": \"" + OrderNo + "\"\r\n  },\r\n  \"customer\": {\r\n    \"id\": \"" + getuserinfo.Tap_CustomerID + "\"\r\n  },\r\n  \"source\": {\r\n    \"id\": \"" + Deserialized_savecard.id + "\"\r\n  },\r\n  \"redirect\": {\r\n    \"url\": \"https://1f3b186efe31e8696c144578816c5443.m.pipedream.net/\"\r\n  }\r\n}\r\n", null, "application/json");
                                request.Content = content;
                                var response = await client.SendAsync(request);
                                var bodys = await response.Content.ReadAsStringAsync();
                                deserialized_CreateCharge = JsonConvert.DeserializeObject<CreateCharge>(bodys);
                                if (deserialized_CreateCharge.status == "CAPTURED")
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(12),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Payment Captured",
                                        IsDeleted = false,
                                        Currency = getsubinfo.Currency,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);
                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal( after_vat_totalamount.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id.ToString();
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();

                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
                                    string body = string.Empty;
                                    _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                                    string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurrening.html";
                                    string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
                                    //Generate PDF
                                    using (StreamReader reader = new StreamReader(contentRootPath))
                                    {
                                        body = reader.ReadToEnd();
                                    }
                                    //Fill EMail By Parameter
                                    body = body.Replace("{title}", "Tamarran Payment Invoice");
                                    body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

                                    body = body.Replace("{InvocieStatus}", "Payment Captured");
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id.ToString());


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);
                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");


                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }

                                        body = body.Replace("{InvoiceAmount}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }

                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        Decimal finalTotal = 0;
                                        if (Discount != 0)
                                        {
                                            finalTotal = Decimal.Subtract(Convert.ToDecimal(incoice_info.SubscriptionAmount), Discount);
                                            body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Total}", incoice_info.SubscriptionAmount.ToString() + " " + getsubinfo.Currency);
                                        }
                                        body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);

                                        Decimal finalValueWithOutVAT = 0;
                                        if (Discount != 0)
                                        {
                                            finalValueWithOutVAT = Decimal.Subtract(without_vat, Discount);
                                            body = body.Replace("{Totalinvoicewithoutvat}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                        else
                                        {
                                            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        }
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForAutomaticPaymentConfirmation(getsubinfo, getuserinfo);
                                    var emailSubject = "Tamarran - Automatic Payment Confirmation - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice invoice = new Invoice
                                    {
                                        InvoiceStartDate = DateTime.UtcNow,
                                        InvoiceEndDate = DateTime.UtcNow.AddMonths(12),
                                        AddedDate = DateTime.UtcNow,
                                        AddedBy = getuserinfo.FullName,
                                        SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                                        VAT = Vat.ToString(),
                                        Discount = Discount.ToString(),
                                        SubscriptionId = getsubinfo.SubscriptionId,
                                        Status = "Un-Paid",
                                        IsDeleted = false,
                                        Currency = getsubinfo.Currency,
                                        Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                        SubscriptionName = getsubinfo.Name,
                                        UserId = getuserinfo.Id,
                                        ChargeId = deserialized_CreateCharge.id,
                                        GymName = getuserinfo.GYMName,
                                        Country = getsubinfo.Countries,
                                        IsFirstInvoice = false
                                    };
                                    _context.invoices.Add(invoice);
                                    _context.SaveChanges();
                                    int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    var nameinvoice = "Inv" + max_invoice_id.ToString();
                                    var emailSubject = "Tamarran - Your subscription renewal in Tamarran failed - " + " Inv" + max_invoice_id.ToString();
                                    var bodyemail = EmailBodyFill.EmailBodyForSubscriptionrenewalinTamarranfailed(getsubinfo, getuserinfo, nameinvoice, Constants.RedirectURL);
                                    _ = _emailSender.SendEmailAsync(user_Email, emailSubject, bodyemail);
                                }
                            }
                        }
                    }
                }
            }

        }
        public async Task AutoChargeJobForBenefit()
        {
            CreateCharge deserialized_CreateCharge = null;
            var recurringCharges_list = _context.recurringCharges.Where(x => x.JobRunDate.Date == DateTime.UtcNow.Date && x.IsRun == false && x.IsFreeze != true && x.ChargeId != null).ToList();
            foreach (var item in recurringCharges_list)
            {
                string[] result = item.ChargeId.Split('_').ToArray();
                if (result[0] != "chg")
                {
                    var getsubinfo = _context.subscriptions.Where(x => x.SubscriptionId == item.SubscriptionId).FirstOrDefault();
                    var getuserinfo = _context.Users.Where(x => x.Id == item.UserID).FirstOrDefault();
                    if (getuserinfo != null && getuserinfo.Country == "Bahrain")
                    {
                        if (getuserinfo.SubscribeID > 0 && getuserinfo.Status == true)
                        {
                            string user_Email = getuserinfo.Email;
                            string attachmentTitle = $"{getuserinfo.FullName}_Invoice_Details";

                            if (getuserinfo.Frequency == "DAILY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Decimal finalTotal = 0;
                                    if (Discount != 0)
                                    {
                                        finalTotal = Decimal.Subtract(totala, Discount);
                                        Vat = CalculatePercentage(finalTotal, Convert.ToDecimal(getsubinfo.VAT));
                                    }
                                    else
                                    {
                                        Vat = CalculatePercentage(totala, Convert.ToDecimal(getsubinfo.VAT));
                                    }
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;;
                                }
                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddDays(1),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);


                                Redirect redirect = new Redirect();
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";

                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = myDeserializedClass.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate;
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);
                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                            else if (getuserinfo.Frequency == "WEEKLY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(getsubinfo.VAT));
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;;
                                }
                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddDays(7),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                Redirect redirect = new Redirect(); 
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";


                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = myDeserializedClass.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);

                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                            else if (getuserinfo.Frequency == "MONTHLY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(getsubinfo.VAT));
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;
                                }
                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(1),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                Redirect redirect = new Redirect();
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";

                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);
                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                            else if (getuserinfo.Frequency == "QUARTERLY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(getsubinfo.VAT));
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;;
                                }
                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(3),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                Redirect redirect = new Redirect();
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";

                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);

                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                            else if (getuserinfo.Frequency == "HALFYEARLY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(getsubinfo.VAT));
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;;
                                }
                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddMonths(6),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                Redirect redirect = new Redirect();
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";

                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = deserialized_CreateCharge.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);

                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                            else if (getuserinfo.Frequency == "YEARLY")
                            {
                                Random rnd = new Random();
                                var TransNo = "Txn_" + rnd.Next(10000000, 99999999);
                                var OrderNo = "Ord_" + rnd.Next(10000000, 99999999);

                                var description = getsubinfo.Frequency;
                                Reference reference = new Reference();
                                reference.transaction = TransNo;
                                reference.order = OrderNo;

                                long ExpireLink = new DateTimeOffset(DateTime.UtcNow.AddYears(1)).ToUnixTimeMilliseconds();
                                long Due = 0;
                                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                                decimal finalamount = 0;
                                decimal Discount = 0;
                                decimal Vat = 0;
                                decimal sun_amount = 0;

                                if (getuserinfo.Frequency == "DAILY")
                                {
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(2)).ToUnixTimeMilliseconds();
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / (int)days;
                                }
                                else if (getuserinfo.Frequency == "WEEKLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount) / 4;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddDays(8)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "MONTHLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)Convert.ToInt32(getsubinfo.Amount);
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "QUARTERLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 3) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(3).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "HALFYEARLY")
                                {
                                    Discount = 0;
                                    finalamount = (decimal)(Convert.ToInt32(getsubinfo.Amount) * 6) / 1;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddMonths(6).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                else if (getuserinfo.Frequency == "YEARLY")
                                {
                                    decimal amountpercentage = (decimal.Parse(getsubinfo.Amount) / 100) * decimal.Parse(getsubinfo.Discount);
                                    var final_amount_percentage = Convert.ToInt32(getsubinfo.Amount) - amountpercentage;
                                    finalamount = decimal.Parse(getsubinfo.Amount) * 12;
                                    Discount = amountpercentage * 12;
                                    Due = new DateTimeOffset(DateTime.UtcNow.AddYears(1).AddDays(1)).ToUnixTimeMilliseconds();
                                }
                                if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                {
                                    Vat = 0;
                                }
                                else
                                {
                                    decimal totala = finalamount;// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    sun_amount = totala;
                                    Vat = (decimal)((totala / 100) * Convert.ToDecimal(getsubinfo.VAT));
                                }
                                decimal finalValueWithOutVATs = finalamount + Vat;// Convert.ToDecimal(getsubinfo.SetupFee) + Vat;
                                Decimal finalValueWithOutVAT = 0;
                                if (Discount != 0)
                                {
                                    finalValueWithOutVAT = Decimal.Subtract(finalValueWithOutVATs, Discount);
                                }
                                else
                                {
                                    finalValueWithOutVAT = finalValueWithOutVATs;;
                                }

                                Invoice invoice = new Invoice
                                {
                                    InvoiceStartDate = DateTime.UtcNow,
                                    InvoiceEndDate = DateTime.UtcNow.AddYears(1),
                                    Currency = getsubinfo.Currency,
                                    AddedDate = DateTime.UtcNow,
                                    AddedBy = getuserinfo.FullName,
                                    SubscriptionAmount = Convert.ToDouble(finalValueWithOutVAT.ToString("0.00")),
                                    SubscriptionId = Convert.ToInt32(getsubinfo.SubscriptionId),
                                    Status = "Un-Paid",
                                    IsDeleted = false,
                                    VAT = Vat.ToString(),
                                    Discount = Discount.ToString(),
                                    Description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")",
                                    SubscriptionName = getsubinfo.Name,
                                    UserId = getuserinfo.Id,
                                    GymName = getuserinfo.GYMName,
                                    Country = getsubinfo.Countries,
                                    IsFirstInvoice = false
                                };
                                _context.invoices.Add(invoice);
                                _context.SaveChanges();
                                int max_invoice_id = _context.invoices.Max(x => x.InvoiceId);

                                Redirect redirect = new Redirect();
                                redirect.url = Constants.RedirectURL + "/Home/CardVerifyBenefit?invoiceid=" + max_invoice_id + "&IsFirstInvoice=false";

                                Post post = new Post();
                                post.url = Constants.RedirectURL + "/Home/CardVerifyBenefits";

                                var countrycode = "";
                                if (getuserinfo.Country == "Bahrain")
                                {
                                    countrycode = "+973";
                                }
                                else if (getuserinfo.Country == "KSA")
                                {
                                    countrycode = "+966";
                                }
                                else if (getuserinfo.Country == "Kuwait")
                                {
                                    countrycode = "+965";
                                }
                                else if (getuserinfo.Country == "UAE")
                                {
                                    countrycode = "+971";
                                }
                                else if (getuserinfo.Country == "Qatar")
                                {
                                    countrycode = "+974";
                                }
                                else if (getuserinfo.Country == "Oman")
                                {
                                    countrycode = "+968";
                                }
                                var currency = getsubinfo.Currency;
                                Phone phone = new Phone();
                                phone.number = getuserinfo.PhoneNumber;
                                phone.country_code = countrycode;

                                Customer customer = new Customer();
                                customer.id = getuserinfo.Tap_CustomerID;

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
                                currencies.Add(getsubinfo.Currency);

                                Charge charge = new Charge();
                                charge.receipt = receipt;
                                charge.statement_descriptor = "test";

                                List<string> p_methods = new List<string>();
                                p_methods.Add("BENEFIT");

                                List<Item> items = new List<Item>();
                                Item itemss = new Item();
                                itemss.image = "";
                                itemss.quantity = 1;
                                itemss.name = "Invoice Amount";
                                itemss.amount = finalValueWithOutVAT.ToString("0.00");
                                itemss.currency = getsubinfo.Currency;
                                items.Add(itemss);

                                Order order = new Order();
                                order.amount = finalValueWithOutVAT.ToString("0.00");
                                order.currency = getsubinfo.Currency;
                                order.items = items;


                                TapInvoice tapInvoice = new TapInvoice();
                                tapInvoice.redirect = redirect;
                                tapInvoice.post = post;
                                tapInvoice.customer = customer;
                                tapInvoice.draft = false;
                                tapInvoice.due = Due;
                                tapInvoice.expiry = ExpireLink;
                                tapInvoice.description = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
                                tapInvoice.mode = "INVOICE";
                                tapInvoice.note = "Invoice Create - Frequency(" + getuserinfo.Frequency + ")";
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
                                          { "Authorization", "Bearer " + getuserinfo.SecertKey },
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
                                    var callbackUrl = $"{Constants.RedirectURL}/Home/SubscriptionAdmin/{getuserinfo.SubscribeID}?link=Yes&userid={getuserinfo.Id}&invoiceid={max_invoice_id}&finalValueWithOutVAT={finalValueWithOutVAT}&isfirstinvoice={"false"}";

                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    i.InvoiceLink = callbackUrl;
                                    i.ChargeId = myDeserializedClass.id;
                                    _context.invoices.Update(i);
                                    _context.SaveChanges();

                                    //Next Recurrening Job Date
                                    RecurringCharge recurringCharge = new RecurringCharge();
                                    recurringCharge.Amount = Convert.ToDecimal(finalValueWithOutVAT.ToString("0.00"));
                                    recurringCharge.SubscriptionId = getsubinfo.SubscriptionId;
                                    recurringCharge.UserID = getuserinfo.Id;
                                    recurringCharge.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    recurringCharge.ChargeId = myDeserializedClass.id;
                                    recurringCharge.JobRunDate = invoice.InvoiceEndDate.AddDays(1);
                                    recurringCharge.Invoice = "Inv" + max_invoice_id;
                                    _context.recurringCharges.Add(recurringCharge);
                                    _context.SaveChanges();


                                    // Update Job Table
                                    var recurreningjob = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                                    recurreningjob.IsRun = true;
                                    recurreningjob.Tap_CustomerId = getuserinfo.Tap_CustomerID;
                                    _context.recurringCharges.Update(recurreningjob);
                                    _context.SaveChanges();
                                    //Send Email
                                    var incoice_info = _context.invoices.Where(x => x.InvoiceId == max_invoice_id).FirstOrDefault();
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
                                    body = body.Replace("{InvoiceID}", "Inv" + max_invoice_id);


                                    body = body.Replace("{User_Name}", getuserinfo.FullName);
                                    body = body.Replace("{User_Email}", user_Email);
                                    body = body.Replace("{User_GYM}", getuserinfo.GYMName);
                                    body = body.Replace("{User_Phone}", getuserinfo.PhoneNumber);

                                    var discount = Convert.ToDecimal(Discount).ToString("0.00") + " " + getsubinfo.Currency;
                                    body = body.Replace("{SubscriptionName}", getsubinfo.Name);
                                    body = body.Replace("{Discount}", discount);

                                    body = body.Replace("{SubscriptionPeriod}", getuserinfo.Frequency);
                                    body = body.Replace("{SetupFee}", "0.0" + " " + getsubinfo.Currency);
                                    var amount = Convert.ToDecimal(incoice_info.SubscriptionAmount);// + Convert.ToDecimal(getsubinfo.SetupFee);
                                    body = body.Replace("{SubscriptionAmount}", sun_amount.ToString("0.00") + " " + getsubinfo.Currency);
                                    //Calculate VAT
                                    if (getsubinfo.VAT == null || getsubinfo.VAT == "0")
                                    {
                                        body = body.Replace("{VAT}", "0.00");
                                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    else
                                    {
                                        body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{Total}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        body = body.Replace("{InvoiceAmount}", finalValueWithOutVAT.ToString("0.00") + " " + getsubinfo.Currency);
                                        var without_vat = Convert.ToDecimal(finalamount);
                                        body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + getsubinfo.Currency);
                                    }
                                    var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                                    var emailSubject = "Tamarran - Automatic Payment Request - " + " Inv" + incoice_info.InvoiceId;
                                    var bodyemail = EmailBodyFill.EmailBodyForBenefitPaymentRequest(getsubinfo, getuserinfo, myDeserializedClass.url);
                                    _ = _emailSender.SendEmailWithFIle(bytes, user_Email, emailSubject, bodyemail, attachmentTitle);
                                }
                                else
                                {
                                    Invoice i = _context.invoices.FirstOrDefault(a => a.InvoiceId == max_invoice_id);

                                    _context.invoices.Remove(i);
                                    _context.SaveChanges();
                                }
                            }
                        }
                    }
                }
            }
        }
         public async Task ManuallyRecurringJob()
        {
            var recurringCharges_list = _context.recurringCharges.Where(x => x.JobRunDate.Date == DateTime.UtcNow.Date && x.IsRun == false && x.IsFreeze != true && (x.ChargeId == null || x.ChargeId == "")).ToList();
            foreach (var item in recurringCharges_list)
            {
                Match emailinvoicematch = Regex.Match(item.Invoice, @"([A-Za-z]+)(\d+)");
                var ev = emailinvoicematch.Groups[2].Value.ToString();

                var invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(ev)).FirstOrDefault();
                var users =_context.Users.Where(x=>x.Id == item.UserID).FirstOrDefault();
                var max_invoice_id = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoice.InvoiceId)).FirstOrDefault();
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(invoice.SubscriptionId)).FirstOrDefault();
                var Amount = subscriptions.Amount;
                int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
                decimal finalamount = 0;
                decimal Discount = 0;
                decimal Vat = 0;
                decimal VatwithoutSetupFee = 0;

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

                var rc = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
                rc.IsRun = true;
                _context.recurringCharges.Update(rc);
                _context.SaveChanges();
                DateTime? JobRunDate = null;
                if (users.Frequency == "DAILY")
                {
                    JobRunDate = item.JobRunDate.AddDays(1);
                }
                else if (users.Frequency == "WEEKLY")
                {
                    JobRunDate = item.JobRunDate.AddDays(7);
                }
                else if (users.Frequency == "MONTHLY")
                {
                    JobRunDate = item.JobRunDate.AddMonths(1);
                }
                else if (users.Frequency == "QUARTERLY")
                {
                    JobRunDate = item.JobRunDate.AddMonths(3);
                }
                else if (users.Frequency == "HALFYEARLY")
                {
                    JobRunDate = item.JobRunDate.AddMonths(6);
                }
                else if (users.Frequency == "YEARLY")
                {
                    JobRunDate = item.JobRunDate.AddYears(1);
                }

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
                    decimal amountpercentage = (decimal.Parse(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
                    var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
                    finalamount = decimal.Parse(subscriptions.Amount) * 12;
                    Discount = amountpercentage * 12;
                }
                Decimal finalinvoicevalue = 0;
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    Vat = 0;
                }
                else
                {
                    decimal totala = finalamount;
                    Decimal finalTotal = 0;
                    if (Discount != 0)
                    {
                        finalTotal = Decimal.Subtract(totala, Discount);
                        finalinvoicevalue = finalTotal;
                        Vat = CalculatePercentage(finalTotal, Convert.ToDecimal(subscriptions.VAT));
                    }
                    else
                    {
                        finalinvoicevalue = totala;
                        Vat = CalculatePercentage(totala, Convert.ToDecimal(subscriptions.VAT));
                    }

                    VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
                }
                decimal after_vat_totalamount = finalinvoicevalue + Vat;
                int newInvoiceId = _context.invoices.Max(x => x.InvoiceId) + 1;
                //Craete New Invoice
                Invoice invoices = new Invoice
                {
                    InvoiceStartDate = DateTime.UtcNow,
                    InvoiceEndDate = JobRunDate.Value,
                    Currency = subscriptions.Currency,
                    AddedDate = DateTime.UtcNow,
                    InvoiceLink = $"{Constants.RedirectURL}Home/SubscriptionAdmin/{users.SubscribeID}?link=Yes&userid={users.Id}&invoiceid={newInvoiceId}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice=false",
                    AddedBy = "System",
                    SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
                    SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
                    Status = "Un-Paid",
                    PaidBy = null,
                    IsDeleted = false,
                    VAT = Vat.ToString(),
                    Discount = Discount.ToString(),
                    Description = "Invoice Create - Frequency(" + users.Frequency + ")",
                    SubscriptionName = subscriptions.Name,
                    UserId = users.Id,
                    ChargeId = null,
                    GymName = users.GYMName,
                    Country = subscriptions.Countries,
                    IsFirstInvoice = false
                };
                _context.invoices.Add(invoices);
                _context.SaveChanges();

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
                body = body.Replace("{InvoiceID}", "Inv" + newInvoiceId);

                body = body.Replace("{User_Name}", users.FullName);
                body = body.Replace("{User_Email}", users.Email);
                body = body.Replace("{User_GYM}", users.GYMName);
                body = body.Replace("{User_Phone}", users.PhoneNumber);

                var setupFree = Convert.ToDecimal(0).ToString("0.00");
                var discount = Convert.ToDecimal(Discount).ToString("0.00");

                body = body.Replace("{SubscriptionName}", subscriptions.Name);
                body = body.Replace("{Discount}", discount + " " + subscriptions.Currency);
                body = body.Replace("{SubscriptionPeriod}", users.Frequency);
                body = body.Replace("{SetupFee}", setupFree + " " + subscriptions.Currency);
                var amount = finalamount + Convert.ToDecimal(Vat);
                body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
                //Calculate VAT
                var linkamount = "";
                if (subscriptions.VAT == null || subscriptions.VAT == "0")
                {
                    body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
                    Decimal finalTotal = 0;
                    if (Discount != 0)
                    {
                        finalTotal = Decimal.Subtract(amount, Discount);
                        linkamount = finalTotal.ToString("0.00");
                        body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        linkamount = amount.ToString("0.00");
                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
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
                        finalTotal = Decimal.Subtract(amount, Discount);
                        linkamount = finalTotal.ToString("0.00");
                        body = body.Replace("{Total}", finalTotal.ToString("0.00") + " " + subscriptions.Currency);
                    }
                    else
                    {
                        linkamount = amount.ToString("0.00");
                        body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
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
                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var callbackUrl = $"/Home/SubscriptionAdmin/{users.SubscribeID}?link=Yes&userid={users.Id}&invoiceid={newInvoiceId}&After_vat_totalamount={after_vat_totalamount}&isfirstinvoice=false";
                var websiteurl = HtmlEncoder.Default.Encode(Constants.RedirectURL + callbackUrl);


                var subject = "Tamarran – Payment Request - " + " Inv" + newInvoiceId.ToString();
                var bodyemail = EmailBodyFill.EmailBodyForPaymentRequest(users, websiteurl);
                _ = _emailSender.SendEmailWithFIle(bytes, users.Email, subject, bodyemail);
            }

        }
        public async Task InvoiceSendByDate() 
        {
            var recurringCharges_list = _context.RecurringInvoiceInfo.Where(x => x.InvoiceSendDate.Date == DateTime.UtcNow.Date && x.Title != null).ToList();
            foreach (var item in recurringCharges_list)
            {
                var applicationUser = _context.Users.Where(x => x.Id == item.UserId).FirstOrDefault();
                var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == item.SubscriptionId).FirstOrDefault();
                RecurringInvoiceInfo invoiceDetails = new RecurringInvoiceInfo
                {
                    InvoiceID = item.InvoiceID,
                    UserName = applicationUser.FullName,
                    UserEmail = applicationUser.Email,
                    UserGYM = applicationUser.GYMName,
                    UserPhone = applicationUser.PhoneNumber,
                    SubscriptionName = subscriptions.Name,
                    Discount = item.Discount.ToString(),
                    SubscriptionPeriod = applicationUser.Frequency,
                    SetupFee =item.SetupFee,
                    SubscriptionAmount =item.SubscriptionAmount,
                    VAT = item.VAT,
                    Total =item.Total,
                    InvoiceAmount = item.InvoiceAmount,
                    TotalInvoiceWithoutVAT = item.TotalInvoiceWithoutVAT,
                    InvoiceLink = item.InvoiceLink,
                    UserId = applicationUser.Id,
                    Subject = item.Subject,
                    SubscriptionId = subscriptions.SubscriptionId,
                    InvoiceIds = item.InvoiceIds,
                    InvoiceSendDate = (DateTime)applicationUser.InvoiceSendDate
                };

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
                // Replace body values
                body = body.Replace("{title}", invoiceDetails.Title);
                body = body.Replace("{currentdate}", invoiceDetails.CurrentDate);
                body = body.Replace("{InvocieStatus}", invoiceDetails.InvoiceStatus);
                body = body.Replace("{InvoiceID}", invoiceDetails.InvoiceID);
                body = body.Replace("{User_Name}", invoiceDetails.UserName);
                body = body.Replace("{User_Email}", invoiceDetails.UserEmail);
                body = body.Replace("{User_GYM}", invoiceDetails.UserGYM);
                body = body.Replace("{User_Phone}", invoiceDetails.UserPhone);
                body = body.Replace("{SubscriptionName}", invoiceDetails.SubscriptionName);
                body = body.Replace("{Discount}", invoiceDetails.Discount);
                body = body.Replace("{SubscriptionPeriod}", invoiceDetails.SubscriptionPeriod);
                body = body.Replace("{SetupFee}", invoiceDetails.SetupFee);
                body = body.Replace("{SubscriptionAmount}", invoiceDetails.SubscriptionAmount);
                body = body.Replace("{VAT}", invoiceDetails.VAT);
                body = body.Replace("{Total}", invoiceDetails.Total);
                body = body.Replace("{InvoiceAmount}", invoiceDetails.InvoiceAmount);
                body = body.Replace("{Totalinvoicewithoutvat}", invoiceDetails.TotalInvoiceWithoutVAT);


                var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
                var websiteurl = invoiceDetails.InvoiceLink;
                var subject = item.Subject;
                var bodyemail = EmailBodyFill.EmailBodyForPaymentRequest(applicationUser, websiteurl);
                _ = _emailSender.SendEmailWithFIle(bytes, applicationUser.Email, subject, bodyemail);

                item.Title = null;
                _context.RecurringInvoiceInfo.Update(item);
                _context.SaveChanges();

            }
        }
        public static decimal CalculatePercentage(decimal num, decimal percent)
        {
            return (num / 100) * percent;
        }
        //public async Task ManuallyRecurringJob()
        //{
        //    var recurringCharges_list = _context.recurringCharges.Where(x => x.JobRunDate.Date == DateTime.UtcNow.Date && x.IsRun == false && x.IsFreeze != true && (x.ChargeId == null || x.ChargeId == "")).ToList();
        //    foreach (var item in recurringCharges_list)
        //    {
        //        Match emailinvoicematch = Regex.Match(item.Invoice, @"([A-Za-z]+)(\d+)");
        //        var ev = emailinvoicematch.Groups[2].Value.ToString();

        //        var invoice = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(ev)).FirstOrDefault();
        //        var users =_context.Users.Where(x=>x.Id == item.UserID).FirstOrDefault();
        //        var max_invoice_id = _context.invoices.Where(x => x.InvoiceId == Convert.ToInt32(invoice.InvoiceId)).FirstOrDefault();
        //        var subscriptions = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(invoice.SubscriptionId)).FirstOrDefault();
        //        var Amount = subscriptions.Amount;
        //        int days = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        //        decimal finalamount = 0;
        //        decimal Discount = 0;
        //        decimal Vat = 0;
        //        decimal VatwithoutSetupFee = 0;
        //        if (users.Frequency == "DAILY")
        //        {
        //            Discount = 0;
        //            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / (int)days;
        //        }
        //        else if (users.Frequency == "WEEKLY")
        //        {
        //            Discount = 0;
        //            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount) / 4;
        //        }
        //        else if (users.Frequency == "MONTHLY")
        //        {
        //            Discount = 0;
        //            finalamount = (decimal)Convert.ToInt32(subscriptions.Amount);
        //        }
        //        else if (users.Frequency == "QUARTERLY")
        //        {
        //            Discount = 0;
        //            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 3) / 1;
        //        }
        //        else if (users.Frequency == "HALFYEARLY")
        //        {
        //            Discount = 0;
        //            finalamount = (decimal)(Convert.ToInt32(subscriptions.Amount) * 6) / 1;
        //        }
        //        else if (users.Frequency == "YEARLY")
        //        {
        //            var amountpercentage = (decimal)(Convert.ToInt32(subscriptions.Amount) / 100) * decimal.Parse(subscriptions.Discount);
        //            var final_amount_percentage = Convert.ToInt32(subscriptions.Amount) - amountpercentage;
        //            finalamount = decimal.Parse(subscriptions.Amount) * 12;
        //            Discount = amountpercentage * 12;
        //        }
        //        if (subscriptions.VAT == null || subscriptions.VAT == "0")
        //        {
        //            Vat = 0;
        //        }
        //        else
        //        {
        //            decimal totala = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
        //            Vat = (decimal)((totala / 100) * Convert.ToInt32(subscriptions.VAT));
        //            VatwithoutSetupFee = (decimal)((finalamount / 100) * Convert.ToInt32(subscriptions.VAT));
        //        }
        //        decimal after_vat_totalamount = finalamount + Convert.ToDecimal(subscriptions.SetupFee) + Vat;
        //        var userinfo = _context.Users.Where(x => x.Id == users.Id).FirstOrDefault();
        //        //update user 
        //        users.Tap_CustomerID = null;
        //        users.Tap_Card_ID = null;
        //        users.SubscribeID = Convert.ToInt32(invoice.SubscriptionId);
        //        users.Tap_Agreement_ID = null;
        //        users.PaymentSource = null;
        //        users.First_Six = null;
        //        users.Last_Four = null;
        //        _context.Users.Update(users);
        //        _context.SaveChanges();

        //        UserSubscriptions userSubscriptions = new UserSubscriptions();
        //        userSubscriptions.SubID = Convert.ToInt32(invoice.SubscriptionId);
        //        userSubscriptions.Userid = users.Id;
        //        _context.userSubscriptions.Add(userSubscriptions);
        //        _context.SaveChanges();


        //        var rc = _context.recurringCharges.Where(x => x.RecurringChargeId == item.RecurringChargeId).FirstOrDefault();
        //        rc.IsRun = true;
        //        _context.recurringCharges.Update(rc);
        //        _context.SaveChanges();

        //        //Craete New Invoice
        //        Invoice invoices = new Invoice
        //        {
        //            InvoiceStartDate = DateTime.UtcNow,
        //            InvoiceEndDate = DateTime.UtcNow,
        //            Currency = subscriptions.Currency,
        //            AddedDate = DateTime.UtcNow,
        //            AddedBy = "System",
        //            SubscriptionAmount = Convert.ToDouble(after_vat_totalamount.ToString("0.00")),
        //            SubscriptionId = Convert.ToInt32(subscriptions.SubscriptionId),
        //            Status = "Un-Paid",
        //            PaidBy = "Manual",
        //            IsDeleted = false,
        //            VAT = Vat.ToString(),
        //            Discount = Discount.ToString(),
        //            Description = "Invoice Create - Frequency(" + users.Frequency + ")",
        //            SubscriptionName = subscriptions.Name,
        //            UserId = users.Id,
        //            ChargeId = null,
        //            GymName = users.GYMName,
        //            Country = subscriptions.Countries,
        //            IsFirstInvoice = true
        //        };
        //        _context.invoices.Add(invoices);
        //        _context.SaveChanges();

        //        int newInvoiceId = _context.invoices.Max(x => x.InvoiceId);


        //        RecurringCharge recurringCharge = new RecurringCharge();
        //        recurringCharge.Amount = Convert.ToDecimal(invoice.SubscriptionAmount);
        //        recurringCharge.SubscriptionId = invoice.SubscriptionId;
        //        recurringCharge.UserID = users.Id;
        //        recurringCharge.Tap_CustomerId = null;
        //        recurringCharge.ChargeId = null;
        //        recurringCharge.Invoice = "Inv" + newInvoiceId;
        //        recurringCharge.IsRun = false;
        //        if (users.Frequency == "DAILY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(1);
        //        }
        //        else if (users.Frequency == "WEEKLY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddDays(7);
        //        }
        //        else if (users.Frequency == "MONTHLY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(1);
        //        }
        //        else if (users.Frequency == "QUARTERLY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(3);
        //        }
        //        else if (users.Frequency == "HALFYEARLY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddMonths(6);
        //        }
        //        else if (users.Frequency == "YEARLY")
        //        {
        //            recurringCharge.JobRunDate = max_invoice_id.InvoiceEndDate.AddYears(1);
        //        }
        //        _context.recurringCharges.Add(recurringCharge);
        //        _context.SaveChanges();

        //        invoice.Remarks = null;
        //        invoice.ChargeId = "";
        //        invoice.PaidBy = "Manual";
        //        invoice.UserId = users.Id;
        //        invoice.Status = "Payment Captured";
        //        invoice.ChargeResponseId = 0;
        //        _context.invoices.Update(invoice);
        //        _context.SaveChanges();

        //        // Send Email
        //        string body = string.Empty;
        //        _environment.WebRootPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        //        string contentRootPath = _environment.WebRootPath + "/htmltopdfRecurreningM.html";
        //        string contentRootPath1 = _environment.WebRootPath + "/css/bootstrap.min.css";
        //        //Generate PDF
        //        var sub_info = _context.subscriptions.Where(x => x.SubscriptionId == Convert.ToInt32(invoice.SubscriptionId)).FirstOrDefault();
        //        using (StreamReader reader = new StreamReader(contentRootPath))
        //        {
        //            body = reader.ReadToEnd();
        //        }
        //        //Fill EMail By Parameter
        //        body = body.Replace("{title}", "Tamarran Payment Invoice");
        //        body = body.Replace("{currentdate}", DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss tt"));

        //        body = body.Replace("{InvocieStatus}", "Unpaid");
        //        body = body.Replace("{InvoiceID}", "Inv" + newInvoiceId);


        //        body = body.Replace("{User_Name}", users.FullName);
        //        body = body.Replace("{User_Email}", users.Email);
        //        body = body.Replace("{User_GYM}", users.GYMName);
        //        body = body.Replace("{User_Phone}", users.PhoneNumber);


        //        body = body.Replace("{SubscriptionName}", subscriptions.Name);
        //        body = body.Replace("{Discount}", Discount.ToString());
        //        body = body.Replace("{SubscriptionPeriod}", userinfo.Frequency);
        //        body = body.Replace("{SetupFee}", subscriptions.SetupFee + " " + subscriptions.Currency);
        //        var amount = finalamount + Convert.ToDecimal(subscriptions.SetupFee);
        //        body = body.Replace("{SubscriptionAmount}", finalamount.ToString("0.00") + " " + subscriptions.Currency);
        //        //Calculate VAT
        //        if (subscriptions.VAT == null || subscriptions.VAT == "0")
        //        {
        //            body = body.Replace("{VAT}", "0.00" + " " + subscriptions.Currency);
        //            body = body.Replace("{Total}", amount.ToString("0.00") + " " + subscriptions.Currency);
        //            body = body.Replace("{InvoiceAmount}", amount.ToString("0.00") + " " + subscriptions.Currency);
        //            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
        //            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
        //        }
        //        else
        //        {
        //            body = body.Replace("{VAT}", Vat.ToString("0.00") + " " + subscriptions.Currency);
        //            body = body.Replace("{Total}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
        //            body = body.Replace("{InvoiceAmount}", after_vat_totalamount.ToString("0.00") + " " + subscriptions.Currency);
        //            var without_vat = Convert.ToDecimal(finalamount) + Convert.ToDecimal(subscriptions.SetupFee);
        //            body = body.Replace("{Totalinvoicewithoutvat}", without_vat.ToString("0.00") + " " + subscriptions.Currency);
        //        }
        //        var bytes = (new NReco.PdfGenerator.HtmlToPdfConverter()).GeneratePdf(body);
        //        var bodyemail = EmailBodyFill.ManuallyPaymentRequest(users, subscriptions); 
        //        var emailSubject = "Tamarran – Payment Receipt - " + " Inv" + newInvoiceId;
        //        _ = _emailSender.SendEmailWithFIle(bytes, users.Email, emailSubject, bodyemail);
        //    }

        //}
    }
}
