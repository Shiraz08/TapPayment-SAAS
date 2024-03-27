using TapPaymentIntegration.Areas.Identity.Data;
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
            //int amount = Convert.ToInt32(finalamount) + Convert.ToInt32(Math.Round(decimal.Round(Convert.ToDecimal(subscriptions.SetupFee), 1), 0, MidpointRounding.AwayFromZero));

            decimal amount = Math.Round(finalAmount + Convert.ToDecimal(subscriptionsSetupFee), 2);
            subscriptionAmount = decimal.Round(Convert.ToDecimal(finalAmount), 2).ToString() + " " + subscriptionsCurrency;

            ///If selected subscription doesn't contain any VAT amount then it must be 0.
            if (subscriptions.VAT == null || subscriptions.VAT == "0")
            {
                vat_str = "0.00" + " " + subscriptionsCurrency; vat = 0.0m;
                invoiceTotal = amount.ToString() + " " + subscriptionsCurrency;
                var without_vat = Convert.ToDecimal(finalAmount) + Convert.ToDecimal(subscriptionsSetupFee);
                invoiceAmount = decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptionsCurrency;
                Totalinvoicewithoutvat = decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptionsCurrency;
                after_vat_total_amount = decimal.Round((finalAmount + Convert.ToDecimal(subscriptionsSetupFee) + vat), 2);
            }
            ///If selected subscription contain VAT Amount.
            else
            {
                decimal total = finalAmount + Convert.ToDecimal(subscriptionsSetupFee);
                vat = (decimal)((total / Convert.ToInt32(subscriptions.VAT)) * 100) / 100;
                after_vat_total_amount = decimal.Round((finalAmount + Convert.ToDecimal(subscriptionsSetupFee) + vat), 2);

                var without_vat = Convert.ToDecimal(finalAmount) + Convert.ToDecimal(subscriptionsSetupFee);
                var totalincVATAmount = Convert.ToDecimal(finalAmount) + Convert.ToDecimal(subscriptionsSetupFee) + decimal.Round(Convert.ToDecimal(vat), 2);
                vat_str = decimal.Round(Convert.ToDecimal(vat), 2).ToString() + " " + subscriptionsCurrency;
                vat = decimal.Round(Convert.ToDecimal(vat), 2);
                invoiceTotal = decimal.Round(Convert.ToDecimal(totalincVATAmount), 2).ToString() + " " + subscriptionsCurrency;
                invoiceAmount = decimal.Round(Convert.ToDecimal(after_vat_total_amount), 2).ToString() + " " + subscriptionsCurrency;
                Totalinvoicewithoutvat = decimal.Round(Convert.ToDecimal(without_vat), 2).ToString() + " " + subscriptionsCurrency;
            }
        }

        public static void GetDiscountAndFinalAmountBySubscriptionFrequency(string frequency, string subscriptionsAmount, int days, out decimal discount, out decimal finalAmount)
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
                    var amountpercentage = (decimal)(Convert.ToInt32(subscriptionsAmount) / 100) * 10;
                    var final_amount_percentage = Convert.ToInt32(subscriptionsAmount) - amountpercentage;
                    finalAmount = final_amount_percentage * 12;
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
                var amountpercentage = (decimal)(Convert.ToInt32(subscription.Amount) / 100) * Convert.ToDecimal(subscription.Discount);
                var final_amount_percentage = Convert.ToInt32(subscription.Amount) - amountpercentage;
                finalamount = final_amount_percentage * 12;
                Discount = amountpercentage * 12;
            }
            if (subscription.VAT == null || subscription.VAT == "0")
            {
                Vat = 0;
            }
            else
            {
                decimal total_amount = finalamount;
                sun_amount = total_amount;
                Vat = (decimal)((total_amount / Convert.ToInt32(subscription.VAT)) * 100) / 100;
            }
            after_vat_totalamount = finalamount + Vat;
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
