using NLog;
using NLog.Web;
using System.Configuration;
using System.Net.Mail;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public static class EmailHelper
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public enum IssueType
        {
            Site,
            Owner,
            Multiple,
            Format
        }
        private static Dictionary<IssueType, List<string>> IdentifiersEmailed = new System.Collections.Generic.Dictionary<IssueType, List<string>>();
        private static Dictionary<IssueType, List<string>> KEYIDsEmailed = new Dictionary<IssueType, List<string>>();
        private static DateTime lastEmail = DateTime.MinValue;

        private static void checkDate()
        {
            if (lastEmail.Date != DateTime.Now.Date)
            {
                IdentifiersEmailed = new Dictionary<IssueType, List<string>>();
                IdentifiersEmailed.Add(IssueType.Site, new List<string>());
                IdentifiersEmailed.Add(IssueType.Owner, new List<string>());
                IdentifiersEmailed.Add(IssueType.Format, new List<string>());
                KEYIDsEmailed = new Dictionary<IssueType, List<string>>();
                KEYIDsEmailed.Add(IssueType.Site, new List<string>());
                KEYIDsEmailed.Add(IssueType.Owner, new List<string>());
                KEYIDsEmailed.Add(IssueType.Format, new List<string>());
                lastEmail = DateTime.Now.Date;
            }
        }

        public static void SendEmail(string KeyID, string Identifier, IssueType issue, string caller)
        {
            try
            {
                checkDate();
                string body = "";
                if (issue == IssueType.Format) // this means we didn't even get to a site id or an owner id etc
                {
                    if (KEYIDsEmailed[issue].Contains(KeyID))
                        // already emailed
                        return;
                    else
                        KEYIDsEmailed[issue].Add(KeyID);
                    body = "The format was wrong on KEYID " + KeyID + " when tring to process through " + caller;
                }
                else
                {
                    if (IdentifiersEmailed[issue].Contains(Identifier))
                        // already emailed
                        return;
                    else
                        IdentifiersEmailed[issue].Add(Identifier);
                    body = issue.ToString() + " Identifier(s) " + Identifier.ToString() + " wasn't found from KEYID " + KeyID + " when searching through " + caller;
                }
                // also log
                logger.Error(body);

                SmtpClient smtpClient = new SmtpClient();
                smtpClient.Host = ConfigurationManager.AppSettings["smtphost"];

                MailMessage myMM = new MailMessage();

                myMM.From = new MailAddress("WBMBackgroundProcessor@ctilogistics.com");
                myMM.To.Add(ConfigurationManager.AppSettings["MailAddress"]);

                myMM.Subject = "WBM Background Processor failed";
                myMM.Body = body;
                myMM.IsBodyHtml = true;
                //smtpClient.Send(myMM);
            }
            catch (Exception e)
            {
                logger.Error("Error sending email: " + e.Message);
            }
        }
    }
}
