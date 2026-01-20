namespace WBM_BackgroundProcessor.DataLookups
{
    public static class wbm_api
    {
        static wbm_api()
        {
            SiteNameToNumber.Add("HZLM", 1);
        }

        #region Site
        public static List<int> ListActiveSiteID = new List<int> { 1 };

        static wbm_common.DataObjects.Carrier.dbRow dbCarrier = new wbm_common.DataObjects.Carrier.dbRow();

        public static Dictionary<string, int> SiteNameToNumber = new Dictionary<string, int>();

        public static Boolean IsSiteActive(int SiteID)
        {
            if (ListActiveSiteID.Contains(SiteID))
                return true;
            else return false;
        }

        public static int GetSiteNumber(string SiteName)
        {
            int rtnSiteID = 0;

            SiteNameToNumber.TryGetValue(SiteName, out rtnSiteID);

            return rtnSiteID;
        }


        #endregion
    }
}
