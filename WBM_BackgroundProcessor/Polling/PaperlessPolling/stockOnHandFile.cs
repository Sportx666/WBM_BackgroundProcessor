using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Excel;
using WBM_BackgroundProcessor.Polling;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling
{
    // Imports stock on hand CSV files (RP_<Site>_CSR2_..._<ddMMyyHHmmss>.csv) from a local folder.
    // No partial loads: every row is checked first and the file is saved only if all of them are valid,
    // in one API call and one database transaction.
    //   Left out without failing the file: rows with no bin location (not stock on hand), rows of owners that exist
    //   in WBM but are inactive, and rows already saved for this site and date (the file was dropped in again).
    //   Saved with ID 0 and a warning: zones and pallet types blank or not in WBM.
    //   Anything else (owner, bin or product not in WBM, bad quantity, use-by date or pallet value) fails the file.
    // Folders, all inside the import folder (StockOnHandImport:Folder, eg C:\WBM\StockOnHand, where the CSVs are dropped):
    //   Processed\ : every valid row saved
    //   Errors\    : nothing saved. Fix the cause and drop the file in again
    //   ErrorLogs\ : <file name>.log for each failed file, a short summary of why
    // The main log (see Nlog.config) has the details, each line starting with "stockOnHandFile [<file name>]",
    // with long lists cut short, and each file ends with one summary line.
    public class stockOnHandFile
    {
        public class Poll
        {
            // Set from appsettings.json StockOnHandImport section in Program.cs
            public static string ImportFolder = "";

            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            private CommonAPI CAPI = new CommonAPI();

            // Lists longer than this are cut short in the main log
            private const int LogListLimit = 20;

            // Use-by dates come as eg "28 DEC 26"; the other usual layouts are accepted too. Month names match in any case, "d" takes 1 or 2 digits
            private static readonly string[] UseByDateFormats = { "d MMM yy", "d-MMM-yy", "d MMM yyyy", "d-MMM-yyyy", "d/M/yy", "d/M/yyyy" };

            DateTime now = DateTime.MinValue;
            string CurrentFileName = "";
            string CurrentSite = "";
            DateTime CurrentStockOnHandDate = DateTime.MinValue;
            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            int CurrentOwnerID = 0;

            // Why the file failed, one short reason per entry; empty means it can be saved. Any failure sends the file to Errors with nothing saved
            List<string> FileErrors = null;

            List<StockOnHandCsvRow> ListCsvRow = null;
            Dictionary<string, int> CurrentLocations = null;    // PublicLocationID -> LocationID for the current site
            Dictionary<string, int> CurrentStocks = null;       // Stock.ProductCode (owner code + product code) -> StockID for the current owner and site
            Dictionary<string, int> CurrentZones = null;        // ZoneNumber -> ZoneProcessingID for the current site
            Dictionary<string, int> CurrentPalletTypes = null;  // PublicID -> PalletTypeID for the current site
            // (OwnerID, LocationID, StockID, PalletNumber, ZoneProcessingID, PalletTypeID) already saved for this site and date
            HashSet<(int, int, int, string, int, int)> ExistingStockOnHand = null;

            List<StockOnHand.dbRow> ListSaveRow = null;

            // Counts for the summary
            int EmptyBinCount = 0;
            int OwnerCount = 0;
            List<string> SkippedOwners = null;
            int UnmatchedZoneLines = 0;
            int UnmatchedPalletTypeLines = 0;
            int AlreadySavedCount = 0;
            int SavedCount = 0;

            private string LogPrefix
            {
                get { return "stockOnHandFile [" + CurrentFileName + "] "; }
            }

            #region Logging
            private void LogInfo(string Message)
            {
                logger.Info(LogPrefix + Message);
            }

            private void LogWarn(string Message)
            {
                logger.Warn(LogPrefix + Message);
            }

            // Records a reason the file can't be saved
            private void Fail(string Message)
            {
                FileErrors.Add(Message);
                logger.Error(LogPrefix + Message);
            }

            // A heading and its items on one line of the main log, cut short after LogListLimit items
            private void LogList(LogLevel Level, string Heading, List<string> Items)
            {
                StringBuilder Line = new StringBuilder(LogPrefix).Append(Heading).Append(": ").Append(string.Join(", ", Items.Take(LogListLimit)));
                if (Items.Count() > LogListLimit)
                    Line.Append(" ... and ").Append(Items.Count() - LogListLimit).Append(" more");
                logger.Log(Level, Line.ToString());
            }

            // A reason the file can't be saved: the heading goes in the error summary, the items (where in the file) only in the main log
            private void FailList(string Heading, List<string> Items)
            {
                FileErrors.Add(Heading);
                LogList(LogLevel.Error, Heading, Items);
            }
            #endregion

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
                FileErrors = new List<string>();
                ListCsvRow = new List<StockOnHandCsvRow>();
                ListSaveRow = new List<StockOnHand.dbRow>();
                EmptyBinCount = 0;
                OwnerCount = 0;
                SkippedOwners = new List<string>();
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
                LogInfo("started");

                if (ReadFileName() && ReadCsv(filePath) && ReadLocationsFromAPI() && ReadZonesFromAPI() && ReadPalletTypesFromAPI() && ReadExistingStockOnHandFromAPI())
                {
                    // The file holds several owners, each owner's rows are checked separately then all are saved together, only if every row is valid.
                    // Every owner is checked even after a problem is found, so one run lists every problem; only an API failure stops early
                    foreach (IGrouping<string, StockOnHandCsvRow> OwnerRows in ListCsvRow.GroupBy(x => x.Owner ?? ""))
                    {
                        if (!ProcessOwner(OwnerRows.Key, OwnerRows.ToList()))
                            break;
                    }

                    if (FileErrors.Count() == 0)
                    {
                        if (ListSaveRow.Count() > 0)
                            SaveAPIFunctionCall();
                        else
                            LogWarn("no new rows to save");
                    }
                }

                string Result = FileErrors.Count() == 0 ? "Processed" : "Error";
                LogSummary(Result, timer.Elapsed);
                // The error file is written to ErrorLogs\ before the CSV is moved, so it is there even if the move fails
                if (Result == "Error")
                {
                    string ErrorLogFolder = Path.Combine(ImportFolder, "ErrorLogs");
                    string ErrorLogPath = Path.Combine(ErrorLogFolder, Path.GetFileNameWithoutExtension(CurrentFileName) + ".log");
                    // Same rule as MoveFile: don't overwrite the log of an earlier failure of the same file
                    if (File.Exists(ErrorLogPath))
                        ErrorLogPath = Path.Combine(ErrorLogFolder, Path.GetFileNameWithoutExtension(CurrentFileName) + "_" + now.ToString("yyyyMMddHHmmss") + ".log");
                    WriteErrorSummary(ErrorLogPath, timer.Elapsed);
                }
                MoveFile(filePath, Result == "Processed" ? "Processed" : "Errors");
                Console.WriteLine(CurrentFileName + " : " + Result);
            }

            private bool ReadFileName()
            {
                // RP_HZLM_CSR2_12345_1_201026223845.csv : 2nd part is the site, last part is the stock on hand date and time (ddMMyyHHmmss)
                string[] FileNameParts = Path.GetFileNameWithoutExtension(CurrentFileName).Split('_');
                if (FileNameParts.Length < 3)
                {
                    Fail("file name not in expected format RP_<Site>_..._<ddMMyyHHmmss>.csv");
                    return false;
                }

                if (!DateTime.TryParseExact(FileNameParts[FileNameParts.Length - 1], "ddMMyyHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out CurrentStockOnHandDate))
                {
                    Fail("file name date '" + FileNameParts[FileNameParts.Length - 1] + "' is not in ddMMyyHHmmss format");
                    return false;
                }

                CurrentSite = FileNameParts[1];
                (int, int) SiteIDCompanyID = wbm_api.GetSiteNumberCompanyID(CurrentSite);
                if (SiteIDCompanyID.Item1 == 0 || SiteIDCompanyID.Item2 == 0)
                {
                    Fail("site '" + CurrentSite + "' from the file name was not found in WBM");
                    return false;
                }
                CurrentSiteID = SiteIDCompanyID.Item1;
                CurrentCompanyID = SiteIDCompanyID.Item2;

                if (!wbm_api.IsSiteActive(CurrentSiteID))
                {
                    Fail("site '" + CurrentSite + "' is not active in WBM");
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
                    Fail("CSV could not be read after " + ListCsvRow.Count() + " rows: " + ex.Message);
                    return false;
                }

                // Rows with no bin location are not stock on hand and are left out; all statuses go in
                List<StockOnHandCsvRow> EmptyBinRows = ListCsvRow.Where(x => string.IsNullOrEmpty(x.BinLocation)).ToList();
                EmptyBinCount = EmptyBinRows.Count();
                if (EmptyBinCount > 0)
                {
                    ListCsvRow.RemoveAll(x => string.IsNullOrEmpty(x.BinLocation));
                    LogList(LogLevel.Warn, "left out " + EmptyBinCount + " rows with no bin location, lines", EmptyBinRows.Select(x => x.LineNumber.ToString()).ToList());
                }

                LogInfo("read " + (ListCsvRow.Count() + EmptyBinCount) + " rows, site " + CurrentSite + ", stock on hand date " + CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"));
                return true;
            }

            // Returns false only when the API failed; problems are recorded in FileErrors and the next owner is still checked
            private bool ProcessOwner(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                OwnerCount++;
                CurrentOwnerID = wbm_api.GetOwnerID(OwnerCode);
                if (CurrentOwnerID == 0)
                {
                    FailList("owner '" + OwnerCode + "' not found in WBM (" + OwnerRows.Count() + " rows)", OwnerRows.Select(x => "line " + x.LineNumber).ToList());
                    return true;
                }
                // An owner set up in WBM but inactive is left out without failing the file
                if (!wbm_api.IsOwnerActive(CurrentOwnerID))
                {
                    SkippedOwners.Add(OwnerCode + " inactive");
                    LogWarn("owner '" + OwnerCode + "' is not active in WBM, skipped " + OwnerRows.Count() + " rows");
                    return true;
                }

                if (!ReadStockFromAPI(OwnerCode, OwnerRows))
                    return false;

                BuildRows(OwnerCode, OwnerRows);
                return true;
            }

            private void BuildRows(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                List<string> MissingBins = new List<string>();
                List<string> MissingProducts = new List<string>();
                List<string> MissingZones = new List<string>();
                List<string> MissingPalletTypes = new List<string>();
                // Bad data, kept by kind so each gets its own heading
                List<string> InvalidQuantities = new List<string>();
                List<string> InvalidUseByDates = new List<string>();
                List<string> InvalidPalletValues = new List<string>();
                int OwnerAlreadySaved = 0;
                int OwnerRowsBuilt = 0;

                // Lines for the same pallet, product, bin, zone and pallet type are added together into one row
                foreach (var PalletLines in OwnerRows.GroupBy(x => new { x.PalletNumber, x.ProductCode, x.BinLocation, x.PositionZone, x.PalletType }))
                {
                    string Lines = string.Join(",", PalletLines.Select(x => x.LineNumber));
                    if (!CurrentLocations.TryGetValue(PalletLines.Key.BinLocation, out int LocationID))
                    {
                        MissingBins.Add(PalletLines.Key.BinLocation);
                        continue;
                    }
                    // Stock.ProductCode holds the owner code in front of the product code
                    if (string.IsNullOrEmpty(PalletLines.Key.ProductCode) || !CurrentStocks.TryGetValue(OwnerCode + PalletLines.Key.ProductCode, out int StockID))
                    {
                        MissingProducts.Add(ShowValue(PalletLines.Key.ProductCode));
                        continue;
                    }

                    // Zone and pallet type not set up in WBM are saved as 0 and logged
                    if (string.IsNullOrEmpty(PalletLines.Key.PositionZone) || !CurrentZones.TryGetValue(PalletLines.Key.PositionZone, out int ZoneProcessingID))
                    {
                        ZoneProcessingID = 0;
                        MissingZones.Add(ShowValue(PalletLines.Key.PositionZone));
                        UnmatchedZoneLines += PalletLines.Count();
                    }
                    if (string.IsNullOrEmpty(PalletLines.Key.PalletType) || !CurrentPalletTypes.TryGetValue(PalletLines.Key.PalletType, out int PalletTypeID))
                    {
                        PalletTypeID = 0;
                        MissingPalletTypes.Add(ShowValue(PalletLines.Key.PalletType));
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
                        InvalidQuantities.Add("line " + Lines + ": '" + string.Join("+", PalletLines.Select(x => x.Quantity)) + "'");
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
                        if (!TryParseUseByDate(csvRow.UseByDate, out DateTime RowUseByDate))
                        {
                            InvalidUseByDates.Add("line " + csvRow.LineNumber + ": '" + csvRow.UseByDate + "'");
                            UseByOK = false;
                            continue;
                        }
                        if (UseByDate == null || RowUseByDate < UseByDate)
                        {
                            UseByDate = RowUseByDate;
                            LotNumber = csvRow.LotNumber ?? "";
                        }
                    }

                    // Status, weight and sizes are the same for every line of a pallet, so the first line is used
                    StockOnHandCsvRow FirstLine = PalletLines.First();
                    Boolean PalletOK = TryParsePalletDecimal(FirstLine.PalletWeight, out decimal PalletWeight)
                        & TryParsePalletInt(FirstLine.PalletWidth, out int PalletWidth)
                        & TryParsePalletInt(FirstLine.PalletDepth, out int PalletDepth)
                        & TryParsePalletInt(FirstLine.PalletHeight, out int PalletHeight);
                    if (!PalletOK)
                        InvalidPalletValues.Add("line " + FirstLine.LineNumber + ": weight '" + FirstLine.PalletWeight + "', width '" + FirstLine.PalletWidth
                            + "', depth '" + FirstLine.PalletDepth + "', height '" + FirstLine.PalletHeight + "'");
                    if (!UseByOK || !PalletOK)
                        continue;

                    StockOnHand.dbRow dbRow = makeNewStockOnHand(LocationID, StockID, Quantity);
                    dbRow.PalletNumber = PalletNumber;
                    dbRow.ZoneProcessingID = ZoneProcessingID;
                    dbRow.Status = FirstLine.Status ?? "";
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

                string Owner = "owner " + OwnerCode + ": ";
                if (MissingZones.Count() > 0)
                    LogList(LogLevel.Warn, Owner + "saved with ZoneProcessingID 0, " + MissingZones.Distinct().Count() + " zones not found for site " + CurrentSite, MissingZones.Distinct().ToList());
                if (MissingPalletTypes.Count() > 0)
                    LogList(LogLevel.Warn, Owner + "saved with PalletTypeID 0, " + MissingPalletTypes.Distinct().Count() + " pallet types not found for site " + CurrentSite, MissingPalletTypes.Distinct().ToList());
                if (OwnerAlreadySaved > 0)
                    LogWarn(Owner + "skipped " + OwnerAlreadySaved + " rows already saved for " + CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"));

                // Bins and products not in WBM, and bad data, fail the whole file so it can be corrected and dropped in again
                if (MissingBins.Count() > 0)
                    FailList(Owner + MissingBins.Distinct().Count() + " bins not found in Location for site " + CurrentSite, MissingBins.Distinct().ToList());
                if (MissingProducts.Count() > 0)
                    FailList(Owner + MissingProducts.Distinct().Count() + " products not found in Stock for site " + CurrentSite, MissingProducts.Distinct().ToList());
                if (InvalidQuantities.Count() > 0)
                    FailList(Owner + InvalidQuantities.Count() + " invalid quantities (must be a whole number of 1 or more)", InvalidQuantities);
                if (InvalidUseByDates.Count() > 0)
                    FailList(Owner + InvalidUseByDates.Count() + " invalid use-by dates (expected eg 27 OCT 26)", InvalidUseByDates);
                if (InvalidPalletValues.Count() > 0)
                    FailList(Owner + InvalidPalletValues.Count() + " invalid pallet weights/sizes (expected numbers eg 179.28 and 1,170)", InvalidPalletValues);

                AlreadySavedCount += OwnerAlreadySaved;
                LogInfo(Owner + OwnerRows.Count() + " CSV rows -> " + OwnerRowsBuilt + " rows to save");
            }

            // Empty values would read as nothing in a list, so they are shown as (blank)
            private static string ShowValue(string Value)
            {
                return string.IsNullOrEmpty(Value) ? "(blank)" : Value;
            }

            private bool TryParseUseByDate(string Value, out DateTime Result)
            {
                // Collapse doubled spaces, eg "28  DEC 26"
                string Cleaned = string.Join(" ", Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                return DateTime.TryParseExact(Cleaned, UseByDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out Result);
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

                LogInfo("sending " + ListSaveRow.Count() + " rows to StockOnHandSaveList");
                Stopwatch timer = Stopwatch.StartNew();

                // FetchFromAPIWithKeyID returns null on any failure, including the login; SaveToAPI returns true if the login fails
                string jsonString = JsonConvert.SerializeObject(SOH);
                if (CAPI.FetchFromAPIWithKeyID(jsonString, "StockOnHandSaveList") == null)
                {
                    // The API logs which row it rejected ("Row N of M (OwnerID, LocationID, StockID): reason")
                    Fail("StockOnHandSaveList failed after " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s, nothing saved; the reason is in the CommonAPI error in the main log and in the API log."
                        + " If this took about 100s the call timed out and the API may still finish the save; dropping the file in again skips any rows already saved");
                    return;
                }

                SavedCount = ListSaveRow.Count();
                LogInfo("saved " + SavedCount + " rows in " + timer.Elapsed.TotalSeconds.ToString("0.0") + "s");
            }

            // One line per file in the main log with everything needed to see what happened
            private void LogSummary(string Result, TimeSpan Elapsed)
            {
                StringBuilder Summary = new StringBuilder(LogPrefix).Append(Result).Append(" in ").Append(Elapsed.TotalSeconds.ToString("0.0")).Append("s")
                    .Append(" | site ").Append(CurrentSite == "" ? "?" : CurrentSite)
                    .Append(", date ").Append(CurrentStockOnHandDate == DateTime.MinValue ? "?" : CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss"))
                    .Append(" | CSV rows ").Append(ListCsvRow.Count() + EmptyBinCount).Append(", no bin ").Append(EmptyBinCount)
                    .Append(" | owners ").Append(OwnerCount).Append(SkippedOwners.Count() > 0 ? " (skipped " + string.Join(", ", SkippedOwners) + ")" : "")
                    .Append(" | CSV rows with zone not in WBM (saved as 0) ").Append(UnmatchedZoneLines).Append(", pallet type not in WBM (saved as 0) ").Append(UnmatchedPalletTypeLines)
                    .Append(" | rows already saved ").Append(AlreadySavedCount).Append(", rows saved ").Append(SavedCount);

                if (Result == "Processed")
                    logger.Info(Summary.ToString());
                else
                {
                    // The reasons are short headings; where in the file each one is was logged above
                    Summary.Append(" | moved to Errors, nothing saved | ").Append(FileErrors.Count()).Append(FileErrors.Count() == 1 ? " reason: " : " reasons: ").Append(string.Join("; ", FileErrors));
                    logger.Error(Summary.ToString());
                }
            }

            // ErrorLogs\<file name>.log for a file that failed: a short summary of why, no row data (that is in the main log)
            private void WriteErrorSummary(string SummaryPath, TimeSpan Elapsed)
            {
                StringBuilder Text = new StringBuilder();
                Text.AppendLine("Stock on hand import: " + CurrentFileName);
                Text.AppendLine("Run at:              " + now.ToString("dd/MM/yyyy HH:mm:ss") + ", took " + Elapsed.TotalSeconds.ToString("0.0") + "s");
                Text.AppendLine("Result:              Error, nothing saved");
                Text.AppendLine("Site:                " + (CurrentSite == "" ? "?" : CurrentSite));
                Text.AppendLine("Stock on hand date:  " + (CurrentStockOnHandDate == DateTime.MinValue ? "?" : CurrentStockOnHandDate.ToString("dd/MM/yyyy HH:mm:ss")));
                Text.AppendLine("CSV rows:            " + (ListCsvRow.Count() + EmptyBinCount) + " (" + EmptyBinCount + " with no bin, left out)");
                Text.AppendLine("Owners:              " + OwnerCount + (SkippedOwners.Count() > 0 ? " (left out " + string.Join(", ", SkippedOwners) + ")" : ""));
                Text.AppendLine();
                Text.AppendLine("Why it failed:");
                foreach (string Reason in FileErrors)
                    Text.AppendLine("    " + Reason);
                Text.AppendLine();
                Text.AppendLine("Which lines and values are affected is in the main log (Nlog.config), lines starting \"stockOnHandFile [" + CurrentFileName + "]\".");

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
                    File.WriteAllText(SummaryPath, Text.ToString());
                    logger.Info(LogPrefix + "error summary written to " + SummaryPath);
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "error summary could not be written to " + SummaryPath + ": " + ex);
                }
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
                    Fail("LocationSearch for site " + CurrentSite + " failed, see the CommonAPI error in the main log");
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
                    Fail("LocationSearch response for site " + CurrentSite + " could not be read: " + ex.Message);
                    return false;
                }
                LogInfo("loaded " + CurrentLocations.Count() + " locations for site " + CurrentSite);
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
                    Fail("ZoneProcessingSearch for site " + CurrentSite + " failed, see the CommonAPI error in the main log");
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
                    Fail("ZoneProcessingSearch response for site " + CurrentSite + " could not be read: " + ex.Message);
                    return false;
                }
                LogInfo("loaded " + CurrentZones.Count() + " zones for site " + CurrentSite);
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
                    Fail("PalletTypeSearch failed, see the CommonAPI error in the main log");
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
                    Fail("PalletTypeSearch response could not be read: " + ex.Message);
                    return false;
                }
                LogInfo("loaded " + CurrentPalletTypes.Count() + " pallet types for site " + CurrentSite);
                return true;
            }

            private bool ReadStockFromAPI(string OwnerCode, List<StockOnHandCsvRow> OwnerRows)
            {
                string jsonString = JsonConvert.SerializeObject(new wbm_common.DataObjects.Stock.Search
                {
                    SearchMode = wbm_common.DataObjects.Stock.Search.SearchModeType.Owner_ListProductCode,
                    ListOwnerID = new List<int> { CurrentOwnerID },
                    // The API's Owner_ListProductCode search ignores ListSiteID; the site is filtered below
                    ListSiteID = new List<int> { CurrentSiteID, 0 },
                    // Stock.ProductCode holds the owner code in front of the product, eg 02300 + 108842 = 02300108842
                    ListProductCode = OwnerRows.Select(x => x.ProductCode).Where(x => !string.IsNullOrEmpty(x)).Distinct().Select(x => OwnerCode + x).ToList()
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockSearch");
                if (response == null)
                {
                    Fail("StockSearch for owner " + OwnerCode + " failed, see the CommonAPI error in the main log");
                    return false;
                }
                try
                {
                    List<Stock.dbRow> ListStock = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);
                    // SiteID 0 means the stock applies to all sites (as in the API's site searches); prefer the file's own site
                    CurrentStocks = ListStock.Where(x => !x.DeletedFlag && (x.SiteID == CurrentSiteID || x.SiteID == 0) && !string.IsNullOrEmpty(x.ProductCode))
                        .GroupBy(x => x.ProductCode).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SiteID == CurrentSiteID).First().ID);

                    List<string> CsvProducts = OwnerRows.Select(x => x.ProductCode).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
                    LogInfo("owner " + OwnerCode + ": " + CurrentStocks.Count() + " products in Stock for site " + CurrentSite + ", "
                        + CsvProducts.Count(x => CurrentStocks.ContainsKey(OwnerCode + x)) + " of the file's " + CsvProducts.Count() + " products found");
                }
                catch (Exception ex)
                {
                    logger.Error(LogPrefix + "ReadStockFromAPI " + ex);
                    Fail("StockSearch response for owner " + OwnerCode + " could not be read: " + ex.Message);
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
                    Fail("StockOnHandSearch for existing rows failed, see the CommonAPI error in the main log");
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
                    Fail("StockOnHandSearch response could not be read: " + ex.Message);
                    return false;
                }
                if (ExistingStockOnHand.Count() > 0)
                    LogInfo(ExistingStockOnHand.Count() + " rows already in WBM for site " + CurrentSite + " and this date, these will be skipped");
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
                    LogInfo("moved to " + DestinationPath);
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
