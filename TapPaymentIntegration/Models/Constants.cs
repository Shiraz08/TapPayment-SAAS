using System;
using System.Net;
using Google.Api.Gax.ResourceNames;
using Google.Cloud.RecaptchaEnterprise.V1;
using Newtonsoft.Json;

namespace TapPaymentIntegration.Models
{
    public static class Constants
    {

#if !DEBUG
        public static readonly string RedirectURL = "https://billing.tamarran.com";  //don't put / in the end
#endif
#if DEBUG
        //public static readonly string RedirectURL = "https://test.softsolutionlogix.com";  //don't put / in the end
        public static readonly string RedirectURL = "https://localhost:7279";  //don't put / in the end
#endif
        public const string SubscriptionErrorMessage = "subscription is In-Active";

        #region EMAIL SENDING

        public static readonly string HOST = "email-smtp.ap-south-1.amazonaws.com";
        public static readonly int PORT = 587;
        public static readonly string NETWORKCREDENTIALUSERNAME = "AKIA4A4DJ4EYKP35ZV4I";
        public static readonly string NETWORKCREDENTIALPASSWORD = "BBjF8ecukbhW7cj15wkKMB+icuJpU84F1Ur6wSpN0MNs";
        public static readonly string MAINEMAILADDRESS = "accounts@tamarran.com";
        public static readonly string BCC = "ali.zayer@tamarran.com";
        public static readonly string MAINDISPLAYNAME = "Tamarran";

        #endregion

        #region  LIVE KEYS
#if !DEBUG
        //// Baharin
        //public static readonly string BHD_Public_Key = "pk_test_7sAiZNXvdpKax26RuJMwbIen";
        //public static readonly string BHD_Test_Key = "sk_test_Tgoy8HbxdQ40l6Ea9SIDci7B";
        //public static readonly string BHD_Merchant_Key = "";
        ////KSA 
        //public static readonly string KSA_Public_Key = "pk_test_j3yKfvbxws8khDpFQOX5JeWc";
        //public static readonly string KSA_Test_Key = "sk_test_1SU5woL8vZe6JXrBHipQu9Dn";
        //public static readonly string KSA_Merchant_Key = "22116401";

        // Baharin
        public static readonly string BHD_Public_Key = "pk_live_7MqbnXVzGkRBaO3KWEmwN8i1";
        public static readonly string BHD_Test_Key = "sk_live_85POWSybstdevAiMxYaGHNp3";
        public static readonly string BHD_Merchant_Key = "";
        //KSA   
        public static readonly string KSA_Public_Key = "pk_live_MWDV5szwGbxeUBdHnJZLk9S2";
        public static readonly string KSA_Test_Key = "sk_live_VDJ1UxM2Arq6ONbz9ptGXhoj";
        public static readonly string KSA_Merchant_Key = "22116401";
#endif
        #endregion

        #region LOCALHOST TESTING KEYS
#if DEBUG
    //Baharin
    public static readonly string BHD_Public_Key = "pk_test_7sAiZNXvdpKax26RuJMwbIen";
    public static readonly string BHD_Test_Key = "sk_test_Tgoy8HbxdQ40l6Ea9SIDci7B";
    public static readonly string BHD_Merchant_Key = "";
    //KSA 
    public static readonly string KSA_Public_Key = "pk_test_j3yKfvbxws8khDpFQOX5JeWc";
    public static readonly string KSA_Test_Key = "sk_test_1SU5woL8vZe6JXrBHipQu9Dn";
    public static readonly string KSA_Merchant_Key = "22116401";
#endif
        #endregion

        #region GOOGLE RECAPTCHA CODE

        public static readonly string SITEKEY = "6LdGaaApAAAAAMIAQn7JXWA3l98LzyI0Oa0QXCUv";
        public static readonly string SECRETKEY = "6LdGaaApAAAAAFhMlqTgeprmUxRwrfDV8Mhxb4-2";

        #endregion
    }


    public static class CreateAssessmentSample
    {
        public static bool ValidateCaptcha(string response)
        {
            var client = new WebClient();
            var reply = client.DownloadString(string.Format("https://www.google.com/recaptcha/api/siteverify?secret={0}&response={1}", Constants.SECRETKEY, response));

            var captchaResponse = JsonConvert.DeserializeObject<CaptchaResponse>(reply);

            return Convert.ToBoolean(captchaResponse.Success);

        }
    }

    public class CaptchaResponse
    {
        [JsonProperty("success")]
        public string Success { get; set; }

        [JsonProperty("error-codes")]
        public List<string> ErrorCodes { get; set; }
    }
}
