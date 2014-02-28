using System;
using System.Net;
using System.Net.Mail;

namespace AT.AutoGitUpdater.Monitor
{
    class EmailHelper
    {

        public static void SendGmail(string subject, string text, bool html, MailAddress[] to, MailAddress[] cc, MailAddress[] bcc, Attachment[] attachments)
        {
            var from = new  MailAddress("ExampleEmail@gmail.com", "Email Owner");
            var password = "password";

            MailMessage m = new MailMessage()
            {
                Subject = subject,
                Body = text,
                IsBodyHtml = true,
                From = from,
                Priority = MailPriority.High,
            };

            foreach (var a in to)
                m.To.Add(a);
            if (null != cc)
                foreach (var a in cc)
                    m.CC.Add(a);
            if (null != bcc)
                foreach (var a in bcc)
                    m.Bcc.Add(a);
            if (null != attachments)
                foreach (var a in attachments)
                    m.Attachments.Add(a);

            SmtpClient c = new SmtpClient()
            {
                Host = "smtp.gmail.com",
                Port = 587,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(from.Address, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            c.Send(m);
        }
    }
}
