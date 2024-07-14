using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Migrations;
using TapPaymentIntegration.Models.Subscription;

namespace TapPaymentIntegration.Utility
{
    public class InvoiceHelper
    {
        public static class CountryInfo
        {
            public const string Bahrain = "+973";
            public const string KSA = "+966";
            public const string Kuwait = "+965";
            public const string UAE = "+971";
            public const string Qatar = "+974";
            public const string Oman = "+968";
        }

        public static class CurrencyInfo
        {
            public const string Bahrain = "BHD";
            public const string KSA = "SAR";
            public const string Kuwait = "KWD";
            public const string UAE = "AED";
            public const string Qatar = "QAR";
            public const string Oman = "OMR";
        }

        public static void SetCountryAndCurrency(ApplicationUser applicationUser, out string countrycode, out string currencycode)
        {
            countrycode = string.Empty;
            currencycode = string.Empty;

            switch (applicationUser.Country)
            {
                case "Bahrain":
                    countrycode = CountryInfo.Bahrain;
                    currencycode = CurrencyInfo.Bahrain;
                    break;

                case "KSA":
                    countrycode = CountryInfo.KSA;
                    currencycode = CurrencyInfo.KSA;
                    break;

                case "Kuwait":
                    countrycode = CountryInfo.Kuwait;
                    currencycode = CurrencyInfo.Kuwait;
                    break;

                case "UAE":
                    countrycode = CountryInfo.UAE;
                    currencycode = CurrencyInfo.UAE;
                    break;

                case "Qatar":
                    countrycode = CountryInfo.Qatar;
                    currencycode = CurrencyInfo.Qatar;
                    break;

                case "Oman":
                    countrycode = CountryInfo.Oman;
                    currencycode = CurrencyInfo.Oman;
                    break;

            }
        }

        public static void CalculdateInvoiceDetails(decimal finalAmount, Subscriptions subscriptions, out string subscriptionAmount, out decimal after_vat_total_amount, out decimal vat, out string vat_str, out string invoiceTotal, out string invoiceAmount, out string Totalinvoicewithoutvat)
        {
            after_vat_total_amount = 0;
            string subscriptionsSetupFee = subscriptions.SetupFee;
            string subscriptionsCurrency = subscriptions.Currency;

            decimal amount = finalAmount + Convert.ToDecimal(subscriptionsSetupFee);
            subscriptionAmount = finalAmount.ToString("0.00") + " " + subscriptionsCurrency;

            ///If selected subscription doesn't contain any VAT amount then it must be 0.
            if (subscriptions.VAT == null || subscriptions.VAT == "0")
            {
                vat_str = "0.00" + " " + subscriptionsCurrency; vat = 0.0m;
                invoiceTotal = amount.ToString() + " " + subscriptionsCurrency;
                var without_vat = finalAmount + Convert.ToDecimal(subscriptionsSetupFee);
                invoiceAmount = without_vat.ToString("0.00") + " " + subscriptionsCurrency;
                Totalinvoicewithoutvat = without_vat.ToString("0.00") + " " + subscriptionsCurrency;
                after_vat_total_amount = Convert.ToDecimal((finalAmount + Convert.ToDecimal(subscriptionsSetupFee) + vat).ToString("0.00"));
            }
            ///If selected subscription contain VAT Amount.
            else
            {
                decimal total = finalAmount + Convert.ToDecimal(subscriptionsSetupFee);
                vat = (decimal)((total / 100) * Convert.ToDecimal(subscriptions.VAT));
                after_vat_total_amount = Convert.ToDecimal((finalAmount + Convert.ToDecimal(subscriptionsSetupFee) + vat).ToString("0.00"));

                var without_vat = finalAmount + Convert.ToDecimal(subscriptionsSetupFee);
                var totalincVATAmount = finalAmount + Convert.ToDecimal(subscriptionsSetupFee) + vat;
                vat_str = vat.ToString("0.00") + " " + subscriptionsCurrency;
                vat = Convert.ToDecimal(vat.ToString("0.00"));
                invoiceTotal = totalincVATAmount.ToString("0.00") + " " + subscriptionsCurrency;
                invoiceAmount = after_vat_total_amount.ToString("0.00") + " " + subscriptionsCurrency;
                Totalinvoicewithoutvat = without_vat.ToString("0.00") + " " + subscriptionsCurrency;
            }
        }

        public static void GetDiscountAndFinalAmountBySubscriptionFrequency(string frequency, string subscriptionsAmount, string Discount, int days, out decimal discount, out decimal finalAmount)
        {
            discount = 0; finalAmount = 0;
            switch (frequency)
            {
                case "DAILY":
                    finalAmount = (decimal)Convert.ToInt32(subscriptionsAmount) / (int)days;
                    break;
                case "WEEKLY":
                    finalAmount = (decimal)Convert.ToInt32(subscriptionsAmount) / 4;
                    break;
                case "MONTHLY":
                    finalAmount = (decimal)Convert.ToInt32(subscriptionsAmount);
                    break;
                case "QUARTERLY":
                    finalAmount = (decimal)(Convert.ToInt32(subscriptionsAmount) * 3);
                    break;
                case "HALFYEARLY":
                    finalAmount = (decimal)(Convert.ToInt32(subscriptionsAmount) * 6);
                    break;
                case "YEARLY":
                    decimal amountpercentage = (decimal.Parse(subscriptionsAmount) / 100) * decimal.Parse(Discount);
                    var final_amount_percentage = Convert.ToInt32(subscriptionsAmount) - amountpercentage;
                    finalAmount = decimal.Parse(subscriptionsAmount) * 12;
                    discount = amountpercentage * 12;
                    break;
            }
        }

        public static void DailyRecurringJob_AutoChargeJobTotalCalculation(ApplicationUser user, Subscriptions subscription, int days, out decimal finalamount, out decimal Discount, out decimal Vat, out decimal sun_amount, out decimal after_vat_totalamount)
        {
            finalamount = 0;
            Discount = 0;
            Vat = 0;
            sun_amount = 0;

            if (user.Frequency == "DAILY")
            {
                Discount = 0;
                finalamount = (decimal)Convert.ToInt32(subscription.Amount) / (int)days;
            }
            else if (user.Frequency == "WEEKLY")
            {
                Discount = 0;
                finalamount = (decimal)Convert.ToInt32(subscription.Amount) / 4;
            }
            else if (user.Frequency == "MONTHLY")
            {
                Discount = 0;
                finalamount = (decimal)Convert.ToInt32(subscription.Amount);
            }
            else if (user.Frequency == "QUARTERLY")
            {
                Discount = 0;
                finalamount = (decimal)(Convert.ToInt32(subscription.Amount) * 3) / 1;
            }
            else if (user.Frequency == "HALFYEARLY")
            {
                Discount = 0;
                finalamount = (decimal)(Convert.ToInt32(subscription.Amount) * 6) / 1;
            }
            else if (user.Frequency == "YEARLY")
            {
                decimal amountpercentage = (decimal.Parse(subscription.Amount) / 100) * decimal.Parse(subscription.Discount);
                var final_amount_percentage = Convert.ToInt32(subscription.Amount) - amountpercentage;
                finalamount = decimal.Parse(subscription.Amount) * 12;
                Discount = amountpercentage * 12;
            }
            if (subscription.VAT == null || subscription.VAT == "0")
            {
                Vat = 0;
            }
            else
            {
                decimal totala = finalamount;// + Convert.ToDecimal(subscription.SetupFee);
                sun_amount = finalamount;
                Decimal finalTotal = 0;
                if (Discount != 0)
                {
                    finalTotal = Decimal.Subtract(totala, Discount);
                    Vat = CalculatePercentage(finalTotal, Convert.ToDecimal(subscription.VAT));
                }
                else
                {
                    Vat = CalculatePercentage(totala, Convert.ToDecimal(subscription.VAT));
                }
            }
            after_vat_totalamount = finalamount + Vat;// Convert.ToDecimal(subscription.SetupFee) + Vat;
        }
        public static decimal CalculatePercentage(decimal num, decimal percent)
        {
            return (num / 100) * percent;
        }
        public static string TruncateAfterSpace(string input)
        {
            int spaceIndex = input.IndexOf(' ');
            if (spaceIndex != -1)
            {
                return input.Substring(0, spaceIndex);
            }
            return input;
        }

    }
}
