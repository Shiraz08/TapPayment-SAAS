using Microsoft.AspNetCore.Identity.UI.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace TapPaymentIntegration.Models.Email
{
    public class EmailSender : IEmailSender
    {
        public async Task SendEmailWithFIle(byte[] bytesArray, string emails, string subject, string message, string attachmentTitle = "Invoice")
        {
            try
            {
                SmtpClient client = new SmtpClient();
                client.Host = Constants.HOST;
                client.Port = Constants.PORT;
                client.UseDefaultCredentials = false;
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Credentials = new NetworkCredential(Constants.NETWORKCREDENTIALUSERNAME, Constants.NETWORKCREDENTIALPASSWORD);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(Constants.MAINEMAILADDRESS, Constants.MAINDISPLAYNAME);

                foreach (var address in emails.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries))
                {
                    mail.To.Add(new MailAddress(address));
                }

                mail.Subject = subject;
                mail.IsBodyHtml = true;
                mail.CC.Add(new MailAddress(Constants.MAINEMAILADDRESS));
                mail.Bcc.Add(new MailAddress(Constants.BCC));
                mail.Body = message;
                mail.Attachments.Add(new Attachment(new MemoryStream(bytesArray), $"{attachmentTitle}.pdf"));
                client.Send(mail);
            }
            catch (Exception ex)
            {
                // log exception
            }
            await Task.CompletedTask;
        }
        public async Task SendEmailAsync(string emails, string subject, string message)
        {
            try
            {
                SmtpClient client = new SmtpClient();
                client.Host = Constants.HOST;
                client.Port = Constants.PORT;
                client.UseDefaultCredentials = false;
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Credentials = new NetworkCredential(Constants.NETWORKCREDENTIALUSERNAME, Constants.NETWORKCREDENTIALPASSWORD);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(Constants.MAINEMAILADDRESS, Constants.MAINDISPLAYNAME);

                foreach (var address in emails.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries))
                {
                    mail.To.Add(new MailAddress(address));
                }

                mail.Subject = subject;
                mail.IsBodyHtml = true;
                mail.CC.Add(new MailAddress(Constants.MAINEMAILADDRESS));
                mail.Bcc.Add(new MailAddress(Constants.BCC));
                mail.Body = message;
                client.Send(mail);
            }
            catch (Exception ex)
            {
                // log exception
            }
            await Task.CompletedTask;
        }
    }
}
