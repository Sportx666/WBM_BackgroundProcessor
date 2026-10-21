using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Net;
using System.Text;
using WBM_BackgroundProcessor.Models.DataHolders;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class CommonAPI
    {
        public CommonAPI() { }
        private AuthenticationResponse AuthResponse;
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

        // Why the last FetchFromAPIWithKeyID call returned null, including the API's own error text when it sent one
        public string ErrorMessage { get; private set; } = "";

        // WebClient with a settable timeout; the default is 100 seconds
        private class TimeoutWebClient : WebClient
        {
            public int TimeoutMilliseconds { get; set; }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                if (request != null && TimeoutMilliseconds > 0)
                {
                    request.Timeout = TimeoutMilliseconds;
                    if (request is HttpWebRequest httpRequest)
                        httpRequest.ReadWriteTimeout = TimeoutMilliseconds;
                }
                return request;
            }
        }

        // Large requests (eg a whole stock on hand file) are cut down so the log stays readable
        private static string TruncateForLog(string text, int maxLength = 1000)
        {
            if (text == null || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) + "... (" + text.Length + " chars)";
        }

        public string FetchFromAPIWithKeyID(string jsonStringSearch, string endpoint, int timeoutSeconds = 0)
        {
            ErrorMessage = "";
            try
            {
                // connection type may be entirely different, not sure, and will be its own class once other tables are added
                using (TimeoutWebClient client = new TimeoutWebClient { TimeoutMilliseconds = timeoutSeconds * 1000 })
                {
                    #region Authentication                
                    if (AuthResponse == null || AuthResponse.TokenExpiry.AddMinutes(-2) < DateTime.Now)
                    {
                        string url = "https://localhost:7116/api/debugauthenticate";
                        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new Authenticate
                        {
                            Username = "test_access",
                            Password = "8nine",
                            CallReference = ""
                        });
                        client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                        client.Encoding = Encoding.UTF8;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                        string response = client.UploadString(url, "POST", jsonString);
                        try
                        {
                            AuthResponse = JsonConvert.DeserializeObject<AuthenticationResponse>(response);
                        }
                        catch (Exception ex)
                        {
                            ErrorMessage = "Authentication response could not be read: " + ex.Message;
                            logger.Error("CommonAPI FetchFromAPIWithKeyID Endpoint " + endpoint + ", search " + TruncateForLog(jsonStringSearch) + ": " + ex);
                            return null;
                        }
                    }
                    #endregion

                    if (AuthResponse != null && AuthResponse.TokenExpiry.AddMinutes(-2) > DateTime.Now)
                    {
                        string url = "https://localhost:7116/api/" + endpoint;
                        client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                        client.Headers[HttpRequestHeader.Authorization] = "Bearer " + AuthResponse.Token;
                        client.Encoding = Encoding.UTF8;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                        return client.UploadString(url, "POST", jsonStringSearch);
                    }
                }
            }
            catch (WebException wex)
            {
                ErrorMessage = wex.Status == WebExceptionStatus.Timeout
                    ? "Timed out waiting for the API" + (timeoutSeconds > 0 ? " after " + timeoutSeconds + " seconds" : "")
                    : wex.Message;
                // A 400/500 from the API sends its ErrorResponse with the real reason
                try
                {
                    if (wex.Response != null)
                        using (StreamReader reader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            wbm_common.NonDatabaseObjects.ErrorResponse errorResponse = JsonConvert.DeserializeObject<wbm_common.NonDatabaseObjects.ErrorResponse>(reader.ReadToEnd());
                            if (errorResponse?.ErrorList?.Errors != null && errorResponse.ErrorList.Errors.Count() > 0)
                                ErrorMessage += " " + string.Join("; ", errorResponse.ErrorList.Errors);
                        }
                }
                catch (Exception)
                {
                    // keep the original error if the response can't be read
                }
                logger.Error("CommonAPI FetchFromAPIWithKeyID Endpoint " + endpoint + ", search " + TruncateForLog(jsonStringSearch) + ": " + ErrorMessage + " " + wex);
                return null;
            }
            catch (Exception excpt)
            {
                ErrorMessage = excpt.Message;
                logger.Error("CommonAPI FetchFromAPIWithKeyID Endpoint " + endpoint + ", search " + TruncateForLog(jsonStringSearch) + ": " + excpt);
                return null;
            }
            ErrorMessage = "Not authenticated with the API";
            return null;
        }

        public bool SaveToAPI(Object newObject, string endpoint)
        {
            try
            {
                // connection type may be entirely different, not sure, and will be its own class once other tables are added
                using (WebClient client = new WebClient())
                {
                    #region Authentication                
                    if (AuthResponse == null || AuthResponse.TokenExpiry.AddMinutes(-2) < DateTime.Now)
                    {
                        string url = "https://localhost:7116/api/debugauthenticate";
                        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new Authenticate
                        {
                            Username = "test_access",
                            Password = "8nine",
                            CallReference = ""
                        });
                        client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                        client.Encoding = Encoding.UTF8;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                        string response = client.UploadString(url, "POST", jsonString);
                        try
                        {
                            AuthResponse = JsonConvert.DeserializeObject<AuthenticationResponse>(response);
                        }
                        catch (Exception ex)
                        {
                            logger.Error("CommonAPI SaveToAPI Endpoint " + endpoint + ", object " + Newtonsoft.Json.JsonConvert.SerializeObject(newObject) + ": " + ex);
                        }
                    }
                    #endregion

                    if (AuthResponse != null && AuthResponse.TokenExpiry.AddMinutes(-2) > DateTime.Now)
                    {
                        string url = "https://localhost:7116" + endpoint;
                        string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(newObject);
                        client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                        client.Headers[HttpRequestHeader.Authorization] = "Bearer " + AuthResponse.Token;
                        client.Encoding = Encoding.UTF8;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                        string response = client.UploadString(url, "POST", jsonString);
                    }
                }
                return true;
            }
            catch (Exception excpt)
            {
                logger.Error("CommonAPI SaveToAPI Endpoint " + endpoint + ", object " + Newtonsoft.Json.JsonConvert.SerializeObject(newObject) + ": " + excpt);
                return false;
            }
        }
    }
}
