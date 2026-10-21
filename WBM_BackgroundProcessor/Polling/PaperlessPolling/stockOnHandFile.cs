using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Globalization;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Excel;
using WBM_BackgroundProcessor.Polling;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling
{
    public class stockOnHandFile
    {
        public class Poll
        {
            // Set from appsettings.json StockOnHandImport section in Program.cs
            public static string ImportFolder = "";
            public static int BatchSize = 1000;

            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            private CommonAPI CAPI = new CommonAPI();

            DateTime now = DateTime.MinValue;
            string CurrentFileName = "";
            DateTime CurrentStockOnHandDate = DateTime.MinValue;
            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            int CurrentOwnerID = 0;

            // Set when something failed that needs looking at; the file then goes to Error instead of Processed
            Boolean FileHasErrors = false;

            List<StockOnHandCsvRow> ListCsvRow = null;
            Dictionary<string, int> CurrentLocations = null;    // PublicLocationID -> LocationID for the current site
            Dictionary<string, int> CurrentStocks = null;       // ProductCode -> StockID for the current owner and site
            HashSet<(int, int)> ExistingStockOnHand = null;     // (LocationID, StockID) already saved for this owner, site and date

            public void PollFolder()
            {
                if (string.IsNullOrEmpty(ImportFolder) || !Directory.Exists(ImportFolder))
                {
                    logger.Error("stockOnHandFile PollFolder import folder not found: " + ImportFolder);
                    return;
                }
            }
        }

    }

}

