using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Diagnostics;
using System.Globalization;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Excel;
using WBM_BackgroundProcessor.Polling;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling
{
    // Imports stock on hand CSV files (RP_<Site>_CSR2_..._<ddMMyyHHmmss>.csv) from a local folder.
    // Each file is saved in one API call and one database transaction: all of its rows are saved or none are.
    //   Processed\ : rows saved; any skipped rows (empty bin, owner/bin/product not in WBM) are in the log,
    //                 as are zones and pallet types not in WBM (those rows are saved with ID 0)
    //   Error\     : nothing saved; the log says why. Fix the cause and drop the file in again.
    // Every log line starts with "stockOnHandFile [<file name>]" and each file ends with one summary line.
    public class stockOnHandFile
    {
        public class Poll
        {
            // Set from appsettings.json StockOnHandImport section in Program.cs
            public static string ImportFolder = "";

            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            private CommonAPI CAPI = new CommonAPI();

            DateTime now = DateTime.MinValue;
            string CurrentFileName = "";
            string CurrentSite = "";
            DateTime CurrentStockOnHandDate = DateTime.MinValue;
            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            int CurrentOwnerID = 0;

            // Why the file failed; empty means it can be saved. Any failure sends the file to Error with nothing saved
            string failedErrorMessage = "";

            List<StockOnHandCsvRow> ListCsvRow = null;
            Dictionary<string, int> CurrentLocations = null;    // PublicLocationID -> LocationID for the current site
            Dictionary<string, int> CurrentStocks = null;       // ProductCode -> StockID for the current owner and site
            Dictionary<string, int> CurrentZones = null;        // ZoneNumber -> ZoneProcessingID for the current site
            Dictionary<string, int> CurrentPalletTypes = null;  // PublicID -> PalletTypeID for the current site
            // (OwnerID, LocationID, StockID, PalletNumber, ZoneProcessingID, PalletTypeID) already saved for this site and date
            HashSet<(int, int, int, string, int, int)> ExistingStockOnHand = null;

            List<StockOnHand.dbRow> ListSaveRow = null;

            // Counts for the summary line
            int EmptyBinCount = 0;
            int OwnerCount = 0;
            List<string> SkippedOwners = null;
            int UnmatchedBinLines = 0;
            int UnmatchedProductLines = 0;
            int UnmatchedZoneLines = 0;
            int UnmatchedPalletTypeLines = 0;
            int AlreadySavedCount = 0;
            int SavedCount = 0;

            private string LogPrefix
            {
                get { return "stockOnHandFile [" + CurrentFileName + "] "; }
            }

            public void PollFolder()
            {
                if (string.IsNullOrEmpty(ImportFolder) || !Directory.Exists(ImportFolder))
                {
                    logger.Error("stockOnHandFile PollFolder import folder not found: '" + ImportFolder + "' (check StockOnHandImport:Folder in appsettings.json)");
                    return;
                }

                string[] Files = Directory.GetFiles(ImportFolder, "*.csv");
                if (Files.Length == 0)
                    return;

                // Sites and owners must be loaded to match anything, otherwise leave the files for the next run
                if (!wbm_api.CheckHeldData())
                {
                    logger.Error("stockOnHandFile PollFolder could not load sites and owners from the API, " + Files.Length + " file(s) left in " + ImportFolder + " for the next run");
                    return;
                }

                logger.Info("stockOnHandFile PollFolder found " + Files.Length + " file(s) in " + ImportFolder);
                foreach (string filePath in Files)
                {
                    // Skip files still being copied in, they will be picked up on the next run
                    if (!IsFileReady(filePath))
                    {
                        logger.Warn("stockOnHandFile [" + Path.GetFileName(filePath) + "] is in use by another program, left for the next run");
                        continue;
                    }

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

            private void ResetFileState()
            {
                CurrentSite = "";
                CurrentStockOnHandDate = DateTime.MinValue;
                CurrentSiteID = 0;
                CurrentCompanyID = 0;
                CurrentOwnerID = 0;
                failedErrorMessage = "";
                ListCsvRow = new List<StockOnHandCsvRow>();
                ListSaveRow = new List<StockOnHand.dbRow>();
                EmptyBinCount = 0;
                OwnerCount = 0;
                SkippedOwners = new List<string>();
                UnmatchedBinLines = 0;
                UnmatchedProductLines = 0;
                UnmatchedZoneLines = 0;
                UnmatchedPalletTypeLines = 0;
                AlreadySavedCount = 0;
                SavedCount = 0;
            }

            private void ProcessFile(string filePath)
            {
                Stopwatch timer = Stopwatch.StartNew();
                now = DateTime.Now;
                CurrentFileName = Path.GetFileName(filePath);
                ResetFileState();
                logger.Info(LogPrefix + "started");

                if (ReadFileName() && ReadCsv(filePath) && ReadLocationsFromAPI() && ReadZonesFromAPI() && ReadPalletTypesFromAPI() && ReadExistingStockOnHandFromAPI())
                {
                    // The file holds several owners, each owner's rows are matched separately then all are saved together
                    foreach (IGrouping<string, StockOnHandCsvRow> OwnerRows in ListCsvRow.GroupBy(x => x.Owner ?? ""))
                    {
                        if (!ProcessOwner(OwnerRows.Key, OwnerRows.ToList()))
                            break;
                    }

                    if (failedErrorMessage == "")
                    {
                        if (ListSaveRow.Count() > 0)
                            SaveAPIFunctionCall();
                        else
                            logger.Warn(LogPrefix + "no new rows to save");
                    }
                }

                string Result = failedErrorMessage == "" ? "Processed" : "Error";
                MoveFile(filePath, Result);
                LogSummary(Result, timer.Elapsed);
                Console.WriteLine(CurrentFileName + " : " + Result);
            }

            private bool ReadFileName()
            {
                // RP_HZLM_CSR2_12345_1_201026223845.csv : 2nd part is the site, last part is the stock on hand date and time (ddMMyyHHmmss)
                string[] FileNameParts = Path.GetFileNameWithoutExtension(CurrentFileName).Split('_');
                if (FileNameParts.Length < 3)
                {
                    failedErrorMessage = "file name not in expected format RP_<Site>_..._<ddMMyyHHmmss>.csv";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }

                if (!DateTime.TryParseExact(FileNameParts[FileNameParts.Length - 1], "ddMMyyHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out CurrentStockOnHandDate))
                {
                    failedErrorMessage = "file name date '" + FileNameParts[FileNameParts.Length - 1] + "' is not in ddMMyyHHmmss format";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }

                CurrentSite = FileNameParts[1];
                (int, int) SiteIDCompanyID = wbm_api.GetSiteNumberCompanyID(CurrentSite);
                if (SiteIDCompanyID.Item1 == 0 || SiteIDCompanyID.Item2 == 0)
                {
                    failedErrorMessage = "site '" + CurrentSite + "' from the file name was not found in WBM";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                CurrentSiteID = SiteIDCompanyID.Item1;
                CurrentCompanyID = SiteIDCompanyID.Item2;

                if (!wbm_api.IsSiteActive(CurrentSiteID))
                {
                    failedErrorMessage = "site '" + CurrentSite + "' is not active in WBM";
                    logger.Error(LogPrefix + failedErrorMessage);
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
                        csv.Read();
                        csv.ReadHeader();
                        while (csv.Read())
                        {
                            StockOnHandCsvRow csvRow = csv.GetRecord<StockOnHandCsvRow>();
                            csvRow.LineNumber = csv.Parser.RawRow;
                            ListCsvRow.Add(csvRow);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Includes missing columns, eg a renamed header
                    logger.Error(LogPrefix + "ReadCsv " + ex);
                    failedErrorMessage = "CSV could not be read after " + ListCsvRow.Count() + " rows: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }

                // Rows with no bin location are the only rows skipped here, all statuses go in
                List<StockOnHandCsvRow> EmptyBinRows = ListCsvRow.Where(x => string.IsNullOrEmpty(x.BinLocation)).ToList();
                EmptyBinCount = EmptyBinRows.Count();
                if (EmptyBinCount > 0)
                {
                    ListCsvRow.RemoveAll(x => string.IsNullOrEmpty(x.BinLocation));
                    logger.Warn(LogPrefix + "skipped " + EmptyBinCount + " rows with no bin location, lines " + string.Join(", ", EmptyBinRows.Select(x => x.LineNumber.ToString())));
                }

                logger.Info(LogPrefix + "read " + (ListCsvRow.Count() + EmptyBinCount) + " rows, site " + CurrentSite + ", stock on hand date " + CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"));
                return true;
            }

            // Returns false when the file can't be saved (API failed or bad data); skipped rows still return true
            private bool ProcessOwner(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                OwnerCount++;
                CurrentOwnerID = wbm_api.GetOwnerID(OwnerCode);
                if (CurrentOwnerID == 0)
                {
                    SkippedOwners.Add(OwnerCode + " not found");
                    logger.Warn(LogPrefix + "owner '" + OwnerCode + "' not found in WBM, skipped " + OwnerRows.Count() + " rows, lines " + string.Join(", ", OwnerRows.Select(x => x.LineNumber.ToString())));
                    return true;
                }
                if (!wbm_api.IsOwnerActive(CurrentOwnerID))
                {
                    SkippedOwners.Add(OwnerCode + " inactive");
                    logger.Warn(LogPrefix + "owner '" + OwnerCode + "' is not active in WBM, skipped " + OwnerRows.Count() + " rows");
                    return true;
                }

                if (!ReadStockFromAPI(OwnerCode, OwnerRows))
                    return false;

                BuildRows(OwnerCode, OwnerRows);
                return failedErrorMessage == "";
            }

            private void BuildRows(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                List<string> MissingBins = new List<string>();
                List<string> MissingProducts = new List<string>();
                List<string> MissingZones = new List<string>();
                List<string> MissingPalletTypes = new List<string>();
                List<string> InvalidValues = new List<string>();
                int OwnerAlreadySaved = 0;
                int OwnerRowsBuilt = 0;

                // Lines for the same pallet, product, bin, zone and pallet type are added together into one row
                foreach (var PalletLines in OwnerRows.GroupBy(x => new { x.PalletNumber, x.ProductCode, x.BinLocation, x.PositionZone, x.PalletType }))
                {
                    string Lines = string.Join(",", PalletLines.Select(x => x.LineNumber));
                    if (!CurrentLocations.TryGetValue(PalletLines.Key.BinLocation, out int LocationID))
                    {
                        MissingBins.Add(PalletLines.Key.BinLocation);
                        UnmatchedBinLines += PalletLines.Count();
                        continue;
                    }
                    if (string.IsNullOrEmpty(PalletLines.Key.ProductCode) || !CurrentStocks.TryGetValue(PalletLines.Key.ProductCode, out int StockID))
                    {
                        MissingProducts.Add(PalletLines.Key.ProductCode ?? "");
                        UnmatchedProductLines += PalletLines.Count();
                        continue;
                    }

                    // Zone and pallet type not set up in WBM are saved as 0 and logged
                    if (string.IsNullOrEmpty(PalletLines.Key.PositionZone) || !CurrentZones.TryGetValue(PalletLines.Key.PositionZone, out int ZoneProcessingID))
                    {
                        ZoneProcessingID = 0;
                        MissingZones.Add(PalletLines.Key.PositionZone ?? "");
                        UnmatchedZoneLines += PalletLines.Count();
                    }
                    if (string.IsNullOrEmpty(PalletLines.Key.PalletType) || !CurrentPalletTypes.TryGetValue(PalletLines.Key.PalletType, out int PalletTypeID))
                    {
                        PalletTypeID = 0;
                        MissingPalletTypes.Add(PalletLines.Key.PalletType ?? "");
                        UnmatchedPalletTypeLines += PalletLines.Count();
                    }

                    string PalletNumber = PalletLines.Key.PalletNumber ?? "";
                    if (ExistingStockOnHand.Contains((CurrentOwnerID, LocationID, StockID, PalletNumber, ZoneProcessingID, PalletTypeID)))
                    {
                        OwnerAlreadySaved++;
                        continue;
                    }

                    int Quantity = 0;
                    Boolean QuantityOK = true;
                    foreach (StockOnHandCsvRow csvRow in PalletLines)
                    {
                        if (int.TryParse(csvRow.Quantity, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out int RowQuantity))
                            Quantity += RowQuantity;
                        else
                            QuantityOK = false;
                    }
                    if (!QuantityOK || Quantity < 1)
                    {
                        InvalidValues.Add("line " + Lines + " quantity '" + string.Join("+", PalletLines.Select(x => x.Quantity)) + "'");
                        continue;
                    }

                    // Earliest use-by date and the lot it belongs to; lines without a use-by date only count if none has one
                    DateTime? UseByDate = null;
                    string LotNumber = PalletLines.First().LotNumber ?? "";
                    Boolean UseByOK = true;
                    foreach (StockOnHandCsvRow csvRow in PalletLines)
                    {
                        if (string.IsNullOrEmpty(csvRow.UseByDate))
                            continue;
                        if (!DateTime.TryParseExact(csvRow.UseByDate, "dd MMM yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime RowUseByDate))
                        {
                            InvalidValues.Add("line " + csvRow.LineNumber + " use-by date '" + csvRow.UseByDate + "' (expected eg 27 OCT 26)");
                            UseByOK = false;
                            continue;
                        }
                        if (UseByDate == null || RowUseByDate < UseByDate)
                        {
                            UseByDate = RowUseByDate;
                            LotNumber = csvRow.LotNumber ?? "";
                        }
                    }

                    // Status, conversion code, weight and sizes are the same for every line of a pallet, so the first line is used
                    StockOnHandCsvRow FirstLine = PalletLines.First();
                    Boolean PalletOK = TryParsePalletDecimal(FirstLine.PalletWeight, out decimal PalletWeight)
                        & TryParsePalletInt(FirstLine.PalletWidth, out int PalletWidth)
                        & TryParsePalletInt(FirstLine.PalletDepth, out int PalletDepth)
                        & TryParsePalletInt(FirstLine.PalletHeight, out int PalletHeight);
                    if (!PalletOK)
                        InvalidValues.Add("line " + FirstLine.LineNumber + " pallet weight/size '" + FirstLine.PalletWeight + "', '" + FirstLine.PalletWidth
                            + "', '" + FirstLine.PalletDepth + "', '" + FirstLine.PalletHeight + "'");
                    if (!UseByOK || !PalletOK)
                        continue;

                    StockOnHand.dbRow dbRow = makeNewStockOnHand(LocationID, StockID, Quantity);
                    dbRow.PalletNumber = PalletNumber;
                    dbRow.ZoneProcessingID = ZoneProcessingID;
                    dbRow.Status = FirstLine.Status ?? "";
                    dbRow.ConversionCode = FirstLine.ConversionCode ?? "";
                    dbRow.UseByDate = UseByDate;
                    dbRow.LotNumber = LotNumber;
                    dbRow.PalletWeight = PalletWeight;
                    dbRow.PalletWidth = PalletWidth;
                    dbRow.PalletDepth = PalletDepth;
                    dbRow.PalletHeight = PalletHeight;
                    dbRow.PalletTypeID = PalletTypeID;
                    ListSaveRow.Add(dbRow);
                    OwnerRowsBuilt++;
                }

                // Rows not found in WBM are skipped and logged; the file still counts as processed
                if (MissingBins.Count() > 0)
                    logger.Warn(LogPrefix + "owner " + OwnerCode + ": skipped " + MissingBins.Distinct().Count() + " bins not found in Location for site " + CurrentSite + ": " + string.Join(", ", MissingBins.Distinct()));
                if (MissingProducts.Count() > 0)
                    logger.Warn(LogPrefix + "owner " + OwnerCode + ": skipped " + MissingProducts.Distinct().Count() + " products not found in Stock for site " + CurrentSite + ": " + string.Join(", ", MissingProducts.Distinct()));
                if (MissingZones.Count() > 0)
                    logger.Warn(LogPrefix + "owner " + OwnerCode + ": saved with ZoneProcessingID 0, zones not found for site " + CurrentSite + ": " + string.Join(", ", MissingZones.Distinct()));
                if (MissingPalletTypes.Count() > 0)
                    logger.Warn(LogPrefix + "owner " + OwnerCode + ": saved with PalletTypeID 0, pallet types not found for site " + CurrentSite + ": " + string.Join(", ", MissingPalletTypes.Distinct()));
                if (OwnerAlreadySaved > 0)
                    logger.Warn(LogPrefix + "owner " + OwnerCode + ": skipped " + OwnerAlreadySaved + " rows already saved for " + CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"));
                // Bad data in the file fails the whole file so it can be corrected and dropped in again
                if (InvalidValues.Count() > 0)
                {
                    failedErrorMessage = "owner " + OwnerCode + ": " + InvalidValues.Count() + " invalid values (quantity must be a whole number of 1 or more): " + string.Join(", ", InvalidValues);
                    logger.Error(LogPrefix + failedErrorMessage);
                }

                AlreadySavedCount += OwnerAlreadySaved;
                logger.Info(LogPrefix + "owner " + OwnerCode + ": " + OwnerRows.Count() + " CSV rows -> " + OwnerRowsBuilt + " rows to save");
            }

            // Pallet weight and sizes, eg "179.28" and "1,170"; empty means 0
            private bool TryParsePalletDecimal(string Value, out decimal Result)
            {
                Result = 0;
                return string.IsNullOrEmpty(Value) || decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out Result);
            }

            private bool TryParsePalletInt(string Value, out int Result)
            {
                Result = 0;
                return string.IsNullOrEmpty(Value) || int.TryParse(Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out Result);
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

            private void SaveAPIFunctionCall()
            {
                StockOnHand SOH = new StockOnHand();
                SOH.CalledFrom = GS.selfName;
                SOH.ChangedTime = now;
                SOH.ListdbRow_instance = ListSaveRow;

                logger.Info(LogPrefix + "sending " + ListSaveRow.Count() + " rows to StockOnHandSaveList");
                Stopwatch timer = Stopwatch.StartNew();

                // FetchFromAPIWithKeyID returns null on any failure, including the login; SaveToAPI returns true if the login fails
                string jsonString = JsonConvert.SerializeObject(SOH);
                if (CAPI.FetchFromAPIWithKeyID(jsonString, "StockOnHandSaveList") == null)
                {
                    // The API logs which row it rejected ("Row N of M (OwnerID, LocationID, StockID): reason")
                    failedErrorMessage = "StockOnHandSaveList failed after " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s, nothing saved; the reason is in the CommonAPI error above and in the API log."
                        + " If this took about 100s the call timed out and the API may still finish the save; dropping the file in again skips any rows already saved";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return;
                }

                SavedCount = ListSaveRow.Count();
                logger.Info(LogPrefix + "saved " + SavedCount + " rows in " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s");
            }

            // One line per file with everything needed to see what happened
            private void LogSummary(string Result, TimeSpan Elapsed)
            {
                string Summary = LogPrefix + Result + " in " + Elapsed.TotalSeconds.ToString("0.0") + "s"
                    + " | site " + (CurrentSite == "" ? "?" : CurrentSite)
                    + ", date " + (CurrentStockOnHandDate == DateTime.MinValue ? "?" : CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"))
                    + " | CSV rows " + (ListCsvRow.Count() + EmptyBinCount) + ", no bin " + EmptyBinCount
                    + " | owners " + OwnerCount + (SkippedOwners.Count() > 0 ? " (skipped " + string.Join(", ", SkippedOwners) + ")" : "")
                    + " | CSV rows with bin not in WBM " + UnmatchedBinLines + ", product not in WBM " + UnmatchedProductLines
                    + ", zone not in WBM (saved as 0) " + UnmatchedZoneLines + ", pallet type not in WBM (saved as 0) " + UnmatchedPalletTypeLines
                    + " | rows already saved " + AlreadySavedCount + ", rows saved " + SavedCount;

                if (Result == "Processed")
                    logger.Info(Summary);
                else
                    logger.Error(Summary + " | moved to Error, nothing saved | reason: " + failedErrorMessage);
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
                    failedErrorMessage = "LocationSearch for site " + CurrentSite + " failed, see the CommonAPI error above";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                try
                {
                    List<Location.dbRow> ListLocation = JsonConvert.DeserializeObject<List<Location.dbRow>>(response);
                    // The API also returns SiteID 0 (all sites) locations; prefer the file's own site
                    CurrentLocations = ListLocation.Where(x => !x.DeletedFlag && !string.IsNullOrEmpty(x.PublicLocationID))
                        .GroupBy(x => x.PublicLocationID).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SiteID == CurrentSiteID).First().ID);
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadLocationsFromAPI " + ex);
                    failedErrorMessage = "LocationSearch response for site " + CurrentSite + " could not be read: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                logger.Info(LogPrefix + "loaded " + CurrentLocations.Count() + " locations for site " + CurrentSite);
                return true;
            }

            private bool ReadZonesFromAPI()
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.ZoneProcessing.Search
                {
                    SearchMode = wbm_common.DataObjects.ZoneProcessing.Search.SearchModeType.SingleSiteID,
                    ListSiteID = new List<int> { CurrentSiteID }
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ZoneProcessingSearch");
                if (response == null)
                {
                    failedErrorMessage = "ZoneProcessingSearch for site " + CurrentSite + " failed, see the CommonAPI error above";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                try
                {
                    List<ZoneProcessing.dbRow> ListZone = JsonConvert.DeserializeObject<List<ZoneProcessing.dbRow>>(response);
                    // The API also returns SiteID 0 (all sites) zones; prefer the file's own site
                    CurrentZones = ListZone.Where(x => !x.DeletedFlag && !string.IsNullOrEmpty(x.ZoneNumber))
                        .GroupBy(x => x.ZoneNumber).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SiteID == CurrentSiteID).First().ID);
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadZonesFromAPI " + ex);
                    failedErrorMessage = "ZoneProcessingSearch response for site " + CurrentSite + " could not be read: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                logger.Info(LogPrefix + "loaded " + CurrentZones.Count() + " zones for site " + CurrentSite);
                return true;
            }

            private bool ReadPalletTypesFromAPI()
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.PalletType.Search
                {
                    SearchMode = wbm_common.DataObjects.PalletType.Search.SearchModeType.AllRecords
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "PalletTypeSearch");
                if (response == null)
                {
                    failedErrorMessage = "PalletTypeSearch failed, see the CommonAPI error above";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                try
                {
                    List<PalletType.dbRow> ListPalletType = JsonConvert.DeserializeObject<List<PalletType.dbRow>>(response);
                    // Pallet types for the file's site or SiteID 0 (all sites); prefer the file's own site
                    CurrentPalletTypes = ListPalletType.Where(x => !x.DeletedFlag && (x.SiteID == CurrentSiteID || x.SiteID == 0) && !string.IsNullOrEmpty(x.PublicID))
                        .GroupBy(x => x.PublicID).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SiteID == CurrentSiteID).First().ID);
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadPalletTypesFromAPI " + ex);
                    failedErrorMessage = "PalletTypeSearch response could not be read: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                logger.Info(LogPrefix + "loaded " + CurrentPalletTypes.Count() + " pallet types for site " + CurrentSite);
                return true;
            }

            private bool ReadStockFromAPI(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
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
                    failedErrorMessage = "StockSearch for owner " + OwnerCode + " failed, see the CommonAPI error above";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                try
                {
                    List<Stock.dbRow> ListStock = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);
                    // SiteID 0 means the stock applies to all sites (as in the API's site searches); prefer the file's own site
                    CurrentStocks = ListStock.Where(x => !x.DeletedFlag && (x.SiteID == CurrentSiteID || x.SiteID == 0) && !string.IsNullOrEmpty(x.ProductCode))
                        .GroupBy(x => x.ProductCode).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SiteID == CurrentSiteID).First().ID);

                    int OtherSiteCount = ListStock.Where(x => !x.DeletedFlag && x.SiteID != CurrentSiteID && x.SiteID != 0).Select(x => x.ProductCode).Distinct().Count();
                    logger.Info(LogPrefix + "owner " + OwnerCode + ": " + CurrentStocks.Count() + " of " + OwnerRows.Select(x => x.ProductCode).Distinct().Count()
                        + " products found in Stock for site " + CurrentSite + (OtherSiteCount > 0 ? ", " + OtherSiteCount + " more only set up for another site" : ""));
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadStockFromAPI " + ex);
                    failedErrorMessage = "StockSearch response for owner " + OwnerCode + " could not be read: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                return true;
            }

            // Lets the same file be dropped in again without saving rows twice
            private bool ReadExistingStockOnHandFromAPI()
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.StockOnHand.Search
                {
                    SearchMode = wbm_common.DataObjects.StockOnHand.Search.SearchModeType.Undefined,
                    ListSiteID = new List<int> { CurrentSiteID },
                    StockOnHandDate_from = CurrentStockOnHandDate,
                    StockOnHandDate_to = CurrentStockOnHandDate
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockOnHandSearch");
                if (response == null)
                {
                    failedErrorMessage = "StockOnHandSearch for existing rows failed, see the CommonAPI error above";
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                try
                {
                    List<StockOnHand.dbRow> ListExisting = JsonConvert.DeserializeObject<List<StockOnHand.dbRow>>(response);
                    ExistingStockOnHand = ListExisting.Select(x => (x.OwnerID, x.LocationID, x.StockID, x.PalletNumber ?? "", x.ZoneProcessingID, x.PalletTypeID)).ToHashSet();
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadExistingStockOnHandFromAPI " + ex);
                    failedErrorMessage = "StockOnHandSearch response could not be read: " + ex.Message;
                    logger.Error(LogPrefix + failedErrorMessage);
                    return false;
                }
                if (ExistingStockOnHand.Count() > 0)
                    logger.Info(LogPrefix + ExistingStockOnHand.Count() + " rows already in WBM for site " + CurrentSite + " and this date, these will be skipped");
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
                    logger.Info(LogPrefix + "moved to " + DestinationPath);
                }
                catch (Exception ex)
                {
                    // The file stays in the import folder and is tried again next run; rows already saved are skipped then
                    logger.Error(LogPrefix + "could not be moved to " + SubFolder + ", it will be picked up again next run: " + ex);
                }
            }
        }
    }
}
