using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Globalization;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Csv;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling.FilePolling
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

                // Sites and owners must be loaded to match anything, otherwise leave the files for the next poll
                if (!wbm_api.CheckHeldData())
                    return;

                foreach (string filePath in Directory.GetFiles(ImportFolder, "*.csv"))
                {
                    // Skip files still being copied in, they will be picked up on the next poll
                    if (!IsFileReady(filePath))
                        continue;

                    ProcessFile(filePath);
                }
            }

            private bool IsFileReady(string filePath)
            {
                // A file still being written is locked by the writer, so an exclusive open fails
                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            private void ProcessFile(string filePath)
            {
                now = DateTime.Now;
                CurrentFileName = Path.GetFileName(filePath);
                FileHasErrors = false;

                if (ReadFileName() && ReadCsv(filePath) && ReadLocationsFromAPI())
                {
                    // The file holds several owners, each owner is matched and saved separately
                    foreach (IGrouping<string, StockOnHandCsvRow> OwnerRows in ListCsvRow.GroupBy(x => x.Owner ?? ""))
                        ProcessOwner(OwnerRows.Key, OwnerRows.ToList());
                }
                else
                    FileHasErrors = true;

                MoveFile(filePath, FileHasErrors ? "Error" : "Processed");
                Console.WriteLine(CurrentFileName + " : " + (FileHasErrors ? "Error" : "Processed"));
            }

            private bool ReadFileName()
            {
                // RP_HZLM_CSR2_12345_1_201026223845.csv : 2nd part is the site, last part is the stock on hand date and time (ddMMyyHHmmss)
                string[] FileNameParts = Path.GetFileNameWithoutExtension(CurrentFileName).Split('_');
                if (FileNameParts.Length < 3)
                {
                    logger.Error("stockOnHandFile file name not in expected format: " + CurrentFileName);
                    return false;
                }

                if (!DateTime.TryParseExact(FileNameParts[FileNameParts.Length - 1], "ddMMyyHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out CurrentStockOnHandDate))
                {
                    logger.Error("stockOnHandFile file name date not in ddMMyyHHmmss format: " + CurrentFileName);
                    return false;
                }

                (int, int) SiteIDCompanyID = wbm_api.GetSiteNumberCompanyID(FileNameParts[1]);
                if (SiteIDCompanyID.Item1 == 0 || SiteIDCompanyID.Item2 == 0)
                {
                    logger.Error("stockOnHandFile site " + FileNameParts[1] + " not found: " + CurrentFileName);
                    return false;
                }
                CurrentSiteID = SiteIDCompanyID.Item1;
                CurrentCompanyID = SiteIDCompanyID.Item2;

                if (!wbm_api.IsSiteActive(CurrentSiteID))
                {
                    logger.Error("stockOnHandFile site " + FileNameParts[1] + " is not active: " + CurrentFileName);
                    return false;
                }

                return true;
            }

            private bool ReadCsv(string filePath)
            {
                try
                {
                    CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        // Strip spaces round values, including inside the quotes eg "Reserved "
                        TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes
                    };
                    using (StreamReader reader = new StreamReader(filePath))
                    using (CsvReader csv = new CsvReader(reader, csvConfig))
                    {
                        ListCsvRow = csv.GetRecords<StockOnHandCsvRow>().ToList();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("stockOnHandFile ReadCsv " + CurrentFileName + ": " + ex);
                    return false;
                }

                // Rows with no bin location are the only rows skipped, all statuses go in
                int EmptyBinCount = ListCsvRow.RemoveAll(x => string.IsNullOrEmpty(x.BinLocation));
                if (EmptyBinCount > 0)
                    logger.Info("stockOnHandFile " + CurrentFileName + ": skipped " + EmptyBinCount + " rows with no bin location");

                return true;
            }

            private void ProcessOwner(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                CurrentOwnerID = wbm_api.GetOwnerID(OwnerCode);
                if (CurrentOwnerID == 0)
                {
                    logger.Error("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " not found, skipped " + OwnerRows.Count() + " rows");
                    return;
                }
                if (!wbm_api.IsOwnerActive(CurrentOwnerID))
                {
                    logger.Error("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " is not active, skipped " + OwnerRows.Count() + " rows");
                    return;
                }

                if (ReadStockFromAPI(OwnerRows) && ReadExistingStockOnHandFromAPI())
                {
                    List<StockOnHand.dbRow> ListSaveRow = BuildRows(OwnerCode, OwnerRows);
                    if (ListSaveRow.Count() > 0)
                        SaveFunction(ListSaveRow);
                }
                else
                    FileHasErrors = true;
            }

            private List<StockOnHand.dbRow> BuildRows(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                List<StockOnHand.dbRow> ListSaveRow = new List<StockOnHand.dbRow>();
                List<string> MissingLocations = new List<string>();
                List<string> MissingStocks = new List<string>();
                int AlreadySavedCount = 0;

                // Pallets with exactly the same bin and product are added together into one row
                foreach (var BinProduct in OwnerRows.GroupBy(x => new { x.BinLocation, x.ProductCode }))
                {
                    if (!CurrentLocations.TryGetValue(BinProduct.Key.BinLocation, out int LocationID))
                    {
                        MissingLocations.Add(BinProduct.Key.BinLocation);
                        continue;
                    }
                    if (string.IsNullOrEmpty(BinProduct.Key.ProductCode) || !CurrentStocks.TryGetValue(BinProduct.Key.ProductCode, out int StockID))
                    {
                        MissingStocks.Add(BinProduct.Key.ProductCode);
                        continue;
                    }
                    if (ExistingStockOnHand.Contains((LocationID, StockID)))
                    {
                        AlreadySavedCount++;
                        continue;
                    }

                    int Quantity = 0;
                    Boolean QuantityOK = true;
                    foreach (StockOnHandCsvRow csvRow in BinProduct)
                    {
                        if (int.TryParse(csvRow.Quantity, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out int RowQuantity))
                            Quantity += RowQuantity;
                        else
                            QuantityOK = false;
                    }
                    if (!QuantityOK || Quantity < 1)
                    {
                        logger.Error("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " bin " + BinProduct.Key.BinLocation
                            + " product " + BinProduct.Key.ProductCode + " has an invalid quantity, skipped");
                        FileHasErrors = true;
                        continue;
                    }

                    ListSaveRow.Add(makeNewStockOnHand(LocationID, StockID, Quantity));
                }

                // Rows not found in WBM are skipped and logged; the file still counts as processed
                if (MissingLocations.Count() > 0)
                    logger.Error("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " skipped bins not found in Location: " + string.Join(", ", MissingLocations.Distinct()));
                if (MissingStocks.Count() > 0)
                    logger.Error("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " skipped products not found in Stock: " + string.Join(", ", MissingStocks.Distinct()));
                if (AlreadySavedCount > 0)
                    logger.Info("stockOnHandFile " + CurrentFileName + ": owner " + OwnerCode + " skipped " + AlreadySavedCount + " rows already saved for " + CurrentStockOnHandDate);

                return ListSaveRow;
            }

            private StockOnHand.dbRow makeNewStockOnHand(int LocationID, int StockID, int Quantity)
            {
                StockOnHand.dbRow dbRow = new StockOnHand.dbRow();
                dbRow.ID = 0;
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.OwnerID = CurrentOwnerID;
                dbRow.SiteID = CurrentSiteID;
                dbRow.StockOnHandDate = CurrentStockOnHandDate;
                dbRow.LocationID = LocationID;
                dbRow.StockID = StockID;
                dbRow.Quantity = Quantity;
                // TODO: work out the storage rate code for each row; 1 is a placeholder until the rules are agreed
                dbRow.RateCodeID = 1;
                dbRow.StorageRateCodeFromID = 1;
                dbRow.CreatedMethod = "WBM_SOH_CSV";
                dbRow.CreatedUserID = GS.selfUserID;
                dbRow.CreatedDateTime = now;
                dbRow.LastAmendedDateTime = now;
                dbRow.LastAmendedMethod = "WBM_SOH_CSV";
                dbRow.LastAmendedUserID = GS.selfUserID;
                dbRow.DeletedFlag = false;
                return dbRow;
            }

            private void SaveFunction(List<StockOnHand.dbRow> ListSaveRow)
            {
                // Send in batches so one call doesn't run past the API timeout. Each batch is saved all or nothing
                for (int i = 0; i < ListSaveRow.Count(); i += BatchSize)
                {
                    if (!SaveAPIFunctionCall(ListSaveRow.Skip(i).Take(BatchSize).ToList()))
                    {
                        FileHasErrors = true;
                        return;
                    }
                }
            }

            private bool SaveAPIFunctionCall(List<StockOnHand.dbRow> ListBatchRow)
            {
                StockOnHand SOH = new StockOnHand();
                SOH.CalledFrom = GS.selfName;
                SOH.ChangedTime = now;
                SOH.ListdbRow_instance = ListBatchRow;

                // FetchFromAPIWithKeyID returns null on any failure, including the login; SaveToAPI returns true if the login fails
                string jsonString = JsonConvert.SerializeObject(SOH);
                if (CAPI.FetchFromAPIWithKeyID(jsonString, "StockOnHandSaveList") == null)
                {
                    logger.Error("stockOnHandFile " + CurrentFileName + ": StockOnHandSaveList failed for owner ID " + CurrentOwnerID + ", " + ListBatchRow.Count() + " rows not saved");
                    return false;
                }
                return true;
            }

            string response = "";
            private bool ReadLocationsFromAPI()
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.Location.Search
                {
                    SearchMode = wbm_common.DataObjects.Location.Search.SearchModeType.SingleSite,
                    ListSiteID = new List<int> { CurrentSiteID }
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "LocationSearch");
                if (response == null)
                {
                    logger.Error("stockOnHandFile ReadLocationsFromAPI returned nothing for site ID " + CurrentSiteID + ": " + CurrentFileName);
                    return false;
                }
                try
                {
                    List<Location.dbRow> ListLocation = JsonConvert.DeserializeObject<List<Location.dbRow>>(response);
                    CurrentLocations = ListLocation.Where(x => !x.DeletedFlag && !string.IsNullOrEmpty(x.PublicLocationID))
                        .GroupBy(x => x.PublicLocationID).ToDictionary(x => x.Key, x => x.First().ID);
                }
                catch (Exception ex)
                {
                    logger.Error("stockOnHandFile ReadLocationsFromAPI " + ex);
                    return false;
                }
                return true;
            }

            private bool ReadStockFromAPI(List<StockOnHandCsvRow> OwnerRows)
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.Stock.Search
                {
                    SearchMode = wbm_common.DataObjects.Stock.Search.SearchModeType.Owner_ListProductCode,
                    ListOwnerID = new List<int> { CurrentOwnerID },
                    ListProductCode = OwnerRows.Select(x => x.ProductCode).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList()
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockSearch");
                if (response == null)
                {
                    logger.Error("stockOnHandFile ReadStockFromAPI returned nothing for owner ID " + CurrentOwnerID + ": " + CurrentFileName);
                    return false;
                }
                try
                {
                    List<Stock.dbRow> ListStock = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);
                    CurrentStocks = ListStock.Where(x => !x.DeletedFlag && x.SiteID == CurrentSiteID && !string.IsNullOrEmpty(x.ProductCode))
                        .GroupBy(x => x.ProductCode).ToDictionary(x => x.Key, x => x.First().ID);
                }
                catch (Exception ex)
                {
                    logger.Error("stockOnHandFile ReadStockFromAPI " + ex);
                    return false;
                }
                return true;
            }

            // Lets the same file be dropped in again (eg after an Error) without saving rows twice
            private bool ReadExistingStockOnHandFromAPI()
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.StockOnHand.Search
                {
                    SearchMode = wbm_common.DataObjects.StockOnHand.Search.SearchModeType.Undefined,
                    ListOwnerID = new List<int> { CurrentOwnerID },
                    ListSiteID = new List<int> { CurrentSiteID },
                    StockOnHandDate_from = CurrentStockOnHandDate,
                    StockOnHandDate_to = CurrentStockOnHandDate
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockOnHandSearch");
                if (response == null)
                {
                    logger.Error("stockOnHandFile ReadExistingStockOnHandFromAPI returned nothing for owner ID " + CurrentOwnerID + ": " + CurrentFileName);
                    return false;
                }
                try
                {
                    List<StockOnHand.dbRow> ListExisting = JsonConvert.DeserializeObject<List<StockOnHand.dbRow>>(response);
                    ExistingStockOnHand = ListExisting.Select(x => (x.LocationID, x.StockID)).ToHashSet();
                }
                catch (Exception ex)
                {
                    logger.Error("stockOnHandFile ReadExistingStockOnHandFromAPI " + ex);
                    return false;
                }
                return true;
            }

            private void MoveFile(string filePath, string SubFolder)
            {
                try
                {
                    string DestinationFolder = Path.Combine(ImportFolder, SubFolder);
                    Directory.CreateDirectory(DestinationFolder);
                    string DestinationPath = Path.Combine(DestinationFolder, CurrentFileName);
                    // Don't overwrite a file of the same name moved there earlier
                    if (File.Exists(DestinationPath))
                        DestinationPath = Path.Combine(DestinationFolder, Path.GetFileNameWithoutExtension(CurrentFileName) + "_" + now.ToString("yyyyMMddHHmmss") + Path.GetExtension(CurrentFileName));
                    File.Move(filePath, DestinationPath);
                }
                catch (Exception ex)
                {
                    logger.Error("stockOnHandFile MoveFile " + CurrentFileName + " to " + SubFolder + ": " + ex);
                }
            }
        }
    }
}
