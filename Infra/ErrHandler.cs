using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Leoz_25.Infra
{
    public class ErrHandler
    {
        #region Logs

        public static void ErrorWrite(string message = "", Exception ex = null)
        {
            string fileName = "Error_" + DateTime.Now.ToString("dd-MM-yyyy") + ".log";
            string path = Path.Combine(AppHttpContextAccessor.ContentRootPath + "/Logs", fileName);

            if (!File.Exists(path))
            {
                File.CreateText(path);
            }

            StreamWriter sw = File.AppendText(path);

            try
            {

                StackTrace trace = new StackTrace(ex, true);

                StringBuilder err = new StringBuilder();

                err.Append("{" + Environment.NewLine);
                err.Append("\"LogEntry\" : \"" + DateTime.Now.ToString(CultureInfo.InvariantCulture) + "\"," + Environment.NewLine);
                err.Append("\"Errorin\" : \"" + AppHttpContextAccessor.AppHttpContext.Request.Path.ToString() + "\"," + Environment.NewLine);
                err.Append("\"InnerException\" : \"" + (ex.InnerException != null ? ex.InnerException.Message : "") + "\"," + Environment.NewLine);
                err.Append("\"InnerExceptionDetail\" : \"" + (ex.InnerException != null ? ex.InnerException.Message.ToString() : "") + "\"," + Environment.NewLine);
                err.Append("\"FileName\" : \"" + trace.GetFrame(0).GetFileName() + "\"," + Environment.NewLine);
                err.Append("\"Line\" : \"" + trace.GetFrame(0).GetFileLineNumber() + "\"," + Environment.NewLine);
                err.Append("\"Column\" : \"" + trace.GetFrame(0).GetFileColumnNumber() + "\"," + Environment.NewLine);
                err.Append("\"ErrorMessage\" : \"" + ex.Message.Replace(Environment.NewLine, "-n-") + "\"," + Environment.NewLine);
                err.Append("\"Source\" : \"" + ex.Source + "\"," + Environment.NewLine);
                err.Append("\"StackTrace\" : \"" + ex.StackTrace.Trim().Replace(Environment.NewLine, "-n-") + "\"," + Environment.NewLine);
                err.Append("\"TargetSite\" : \"" + Convert.ToString(ex.TargetSite) + "\"," + Environment.NewLine);

                err.Append("\"Exception\" : \"" + Convert.ToString(ex.GetType().Name) + "\"," + Environment.NewLine);
                err.Append("\"Source\" : \"" + ex.Source.ToString().Trim() + "\"," + Environment.NewLine);
                err.Append("\"Method\" : \"" + ex.TargetSite.Name.ToString() + "\"," + Environment.NewLine);
                err.Append("\"Date\" : \"" + DateTime.Now.ToShortDateString() + "\"," + Environment.NewLine);
                err.Append("\"Time\" : \"" + DateTime.Now.ToShortTimeString() + "\"," + Environment.NewLine);
                err.Append("\"Computer\" : \"" + Dns.GetHostName().ToString() + "\"," + Environment.NewLine);
                err.Append("\"Error\" : \"" + ex.ToString().Trim().Replace(Environment.NewLine, "-n-") + "\"" + Environment.NewLine);

                err.Append("},");

                sw.WriteLine(err);

                sw.Flush();
                sw.Close();

            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());

                try
                {
                    MailMessage msg = new MailMessage();
                    SmtpClient smtp = new SmtpClient();
                    msg.From = new MailAddress("myclientuser@gmail.com");
                    msg.To.Add(new MailAddress("test_08112021@mailinator.com"));
                    msg.Subject = "Error Log - " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss");
                    msg.IsBodyHtml = false;
                    msg.Body = sw.ToString();
                    smtp.Port = 587;
                    smtp.Host = "smtp.gmail.com"; //for gmail host  
                    smtp.EnableSsl = true;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential("sagemotor1@gmail.com", "Client@123");
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Send(msg);
                }
                catch (Exception EX) { }
                sw.Close();
            }
            finally
            {
                sw.Close();
            }

        }

        public static void LogWrite(string message)
        {
            string fileName = "Log_" + DateTime.Now.ToString("dd-MM-yyyy") + ".log";
            string path = Path.Combine(AppHttpContextAccessor.ContentRootPath + "/Logs", fileName);

            if (!File.Exists(path))
            {
                File.CreateText(path);
            }

            StringWriter sw = new StringWriter();

            try
            {
                sw.WriteLine(System.DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss") + " | " + message);

                sw.WriteLine(Environment.NewLine + "**********************************************" + Environment.NewLine);

                try
                {
                    MailMessage msg = new MailMessage();
                    SmtpClient smtp = new SmtpClient();
                    msg.From = new MailAddress("myclientuser@gmail.com");
                    msg.To.Add(new MailAddress("test_08112021@mailinator.com"));
                    msg.Subject = "Test - " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss");
                    msg.IsBodyHtml = false;
                    msg.Body = sw.ToString();
                    smtp.Port = 587;
                    smtp.Host = "smtp.gmail.com"; //for gmail host  
                    smtp.EnableSsl = true;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential("sagemotor1@gmail.com", "Client@123");
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Send(msg);
                }
                catch (Exception EX) { }


                sw.Flush();
                sw.Close();

            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());

                try
                {
                    MailMessage msg = new MailMessage();
                    SmtpClient smtp = new SmtpClient();
                    msg.From = new MailAddress("myclientuser@gmail.com");
                    msg.To.Add(new MailAddress("test_08112021@mailinator.com"));
                    msg.Subject = "Error Log - " + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss");
                    msg.IsBodyHtml = false;
                    msg.Body = sw.ToString();
                    smtp.Port = 587;
                    smtp.Host = "smtp.gmail.com"; //for gmail host  
                    smtp.EnableSsl = true;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential("sagemotor1@gmail.com", "Client@123");
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.Send(msg);
                }
                catch (Exception EX) { }
                sw.Close();
            }
            finally
            {
                sw.Close();
            }
        }

        #endregion

    }
}
