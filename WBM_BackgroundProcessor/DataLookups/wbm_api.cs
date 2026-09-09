using Azure;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.DataLookups
{
    public static class wbm_api
    {
        public enum FieldState
        {
            Active,
            Inactive,
            NotFound
        }

        static wbm_api()
        {
        }

        public static List<int> ListActiveSiteID = new List<int>();
        public static List<int> ListActiveOwnerID = new List<int>();
        public static Dictionary<string, (int, int)> SiteNameToNumberCompany = new Dictionary<string, (int, int)>();
        public static Dictionary<string, int> OwnerSourceToID = new Dictionary<string, int>();
        private static DateTime lastCalled = DateTime.MinValue;
        private static CommonAPI CAPI = new CommonAPI();
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

        private static Dictionary<int, Owner.dbRow> DictOwner = new Dictionary<int, Owner.dbRow>();

        #region Site
        public static Boolean IsSiteActive(int SiteID)
        {
            if (ListActiveSiteID.Contains(SiteID))
                return true;
            else return false;
        }

        public static (int, int) GetSiteNumberCompanyID(string SiteName)
        {
            (int, int) rtnSiteID = (0, 0);

            SiteNameToNumberCompany.TryGetValue(SiteName, out rtnSiteID);

            return rtnSiteID;
        }
        private static bool getSites()
        {
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.Site.Search
            {
                SearchMode = wbm_common.DataObjects.Site.Search.SearchModeType.AllRecords
            });
            string response = CAPI.FetchFromAPIWithKeyID(jsonString, "SiteSearch");
            if (response == null)
            {
                logger.Error("wbm_api ReadFromAPI SiteSearch return nothing");
                return false;
            }
            else
            {
                try
                {
                    List<Site.dbRow> sites = JsonConvert.DeserializeObject<List<Site.dbRow>>(response);
                    ListActiveSiteID = new List<int>();
                    SiteNameToNumberCompany = new Dictionary<string, (int, int)>();
                    foreach (Site.dbRow dbrow in sites)
                    {
                        SiteNameToNumberCompany.Add(dbrow.PublicID, (dbrow.ID, dbrow.CompanyID));
                        if (dbrow.Active)
                            ListActiveSiteID.Add(dbrow.ID);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("wbm_api ReadFromAPI SiteSearch: " + ex);
                    return false;
                }
            }
        }

        #endregion

        #region Owner
        public static Boolean IsOwnerActive(int OwnerID)
        {
            if (ListActiveOwnerID.Contains(OwnerID))
                return true;
            else return false;
        }

        public static int GetOwnerID(string SourceOwner)
        {
            int rtnOwnerID = 0;

            OwnerSourceToID.TryGetValue(SourceOwner, out rtnOwnerID);

            return rtnOwnerID;
        }
        private static bool getOwners()
        {
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.Owner.Search
            {
                SearchMode = wbm_common.DataObjects.Owner.Search.SearchModeType.AllRecords
            });
            string response = CAPI.FetchFromAPIWithKeyID(jsonString, "OwnerSearch");
            if (response == null)
            {
                logger.Error("wbm_api ReadFromAPI OwnerSearch return nothing");
                return false;
            }
            else
            {
                try
                {
                    List<Owner.dbRow> owners = JsonConvert.DeserializeObject<List<Owner.dbRow>>(response);
                    ListActiveOwnerID = new List<int>();
                    OwnerSourceToID = new Dictionary<string, int>();
                    DictOwner = new Dictionary<int, Owner.dbRow>();

                    foreach (Owner.dbRow dbrow in owners)
                    {
                        OwnerSourceToID.Add(dbrow.SourceOwnerID, dbrow.ID);
                        if (dbrow.Active)
                        {
                            ListActiveOwnerID.Add(dbrow.ID);
                            DictOwner.Add(dbrow.ID, dbrow);
                        }

                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Error("wbm_api ReadFromAPI OwnerSearch: " + ex);
                    return false;
                }
            }
        }

        public static Owner.dbRow GetOwner_dbRow(int OwnerID)
        {
            Owner.dbRow rtnOwner = null;
            DictOwner.TryGetValue(OwnerID, out rtnOwner);

            return rtnOwner;
        }

        #endregion

        public static bool CheckHeldData()
        {
            if (TimeSpan.FromTicks(DateTime.Now.Ticks - lastCalled.Ticks).TotalMinutes > 60)
            {
                if (!getSites())
                    return false;

                if (!getOwners())
                    return false;

                lastCalled = DateTime.Now;
                return true;
            }
            return true;
        }

    }
}
