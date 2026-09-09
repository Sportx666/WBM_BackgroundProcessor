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
        public string FetchFromAPIWithKeyID(string jsonStringSearch, string endpoint)
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
                            logger.Error("CommonAPI FetchFromAPIWithKeyID Endpoint " + endpoint + ", search " + jsonStringSearch + ": " + ex);
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
            catch (Exception excpt)
            {
                logger.Error("CommonAPI FetchFromAPIWithKeyID Endpoint " + endpoint + ", search " + jsonStringSearch + ": " + excpt);
                return null;
            }
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
