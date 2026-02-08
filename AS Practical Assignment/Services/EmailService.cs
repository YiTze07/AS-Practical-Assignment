using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AS_Practical_Assignment.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            string smtpServer = _configuration["Email:SmtpServer"];
            int smtpPort = int.Parse(_configuration["Email:SmtpPort"]);
            string senderEmail = _configuration["Email:SenderEmail"];
            string senderPassword = _configuration["Email:SenderPassword"];

            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Ace Job Agency"),
                To = { new MailAddress(toEmail) },
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}