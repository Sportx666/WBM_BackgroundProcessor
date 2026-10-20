using Azure;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using OfficeOpenXml.Style;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Helpers;
using WBM_BackgroundProcessor.Models;
using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.DataHolders;
using WBM_BackgroundProcessor.Models.Excel;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;
using wbm_common.NonDatabaseObjects;
using static WBM_BackgroundProcessor.Models.Excel.ExcelExtraction;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class receipt
    {
        public class Poll
        {
            public enum ReceiptEnum
            {
                Excel = 0,
                TriggerRows = 1,
                AwaitingTable = 2
            }

            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            string UnCutID;

            Boolean CanDeleteTriggerRecord = false;
            Boolean PreviousCanDeleteTriggerRecord = false;
            Boolean PreviousCanProcessTriggerRecord = false;
            Boolean LoadOwnerDataRequired = true;

            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            int CurrentOwnerID = 0;
            string Site = "";
            string PreviousSiteName = "";
            string Owner = "";
            string PreviousOwner = "";
            string CurrentReceiptNumber = "";
            Owner.dbRow CurrentOwner_dbRow = null;

            ReceiptDataHolder CurrentRdh = null;

            List<CustomerAddressDefault.dbRow> ListCustomerAddressDefault = null;
            List<ActionRateCode.dbRow> ListActionRateCode = null;
            List<ZoneProcessing.dbRow> ListZoneProcessing = null;
            List<ProcessingRule.dbRow> ListProcessingRule = null;
            List<RateCollection.dbRow> ListRateCollection = null;
            List<RateCode.dbRow> ListRateCode = null;
            List<TableRate.dbRow> ListTableRate = null;
            List<DevanLookup.dbRow> ListDevanLookup { get; set; }
            List<ContainerHeavyLift.dbRow> ListContainerHeavyLift { get; set; }
            List<ContainerLift.dbRow> ListContainerLift { get; set; }
            List<StockRateCategory.dbRow> ListStockRateCategory { get; set; }

            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public List<WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans> ListTrans = new List<WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans>();

            public int ReceiptCount = 0;
            Dictionary<string, List<EERow>> ExcelValues;
            bool genExcel = false;
            List<(string, string)> ReceiptsThatDidntComplete = new List<(string, string)>();

            public async Task PollTriggerTable(ReceiptEnum RecEnm)
            {
                ReceiptsThatDidntComplete = new List<(string, string)>();
                switch (RecEnm)
                {
                    case ReceiptEnum.Excel:
                        genExcel = true;
                        ExcelValues = ExcelExtraction.ExtractReceipt();
                        List<string> ExcelKeys = ExcelValues.Keys.ToList();
                        foreach (string KeyIDFromExcel in ExcelKeys)
                        {
                            UnCutID = KeyIDFromExcel;
                            purchOrd_dw = await ReadPurchOrdFromDB();
                            if (!purchOrd_dw.IsSuccess || purchOrd_dw.Value.Count() == 0 || !purchOrd_dw.Value[0].KEYID.Contains("_"))
                            {
                                ReceiptsThatDidntComplete.Add((KeyIDFromExcel, "Couldn't read Purch Ord Row"));
                                continue;
                            }
                            if (ExcelValues.ContainsKey(purchOrd_dw.Value[0].KEYID.Split("_")[1])) // the excel docs give us some kind of repeats
                                continue;
                            string UnCutKeyID = purchOrd_dw.Value[0].SITE_NO + "_" + purchOrd_dw.Value[0].KEYID.Split("_")[1];
                            await ProcessTPODW(new List<TRIGGER_PURCH_ORD_PROD_DW> { new TRIGGER_PURCH_ORD_PROD_DW { KEYID = UnCutKeyID } }, UnCutKeyID);
                            // since the excel doc has missing HZLM_2300 type parts we need to fix the dictionary we make in receipt but not in order
                            ExcelValues.Add(purchOrd_dw.Value[0].KEYID.Split("_")[1], ExcelValues[KeyIDFromExcel]);
                        }
                            ProduceExcel();
                        break;
                    case ReceiptEnum.TriggerRows:

                        //TRIGGER_PURCH_ORD_PROD_DW trow = new TRIGGER_PURCH_ORD_PROD_DW();
                        //trow.KEYID = "HZLM_02300VRT-A-54891";

                        //List<TRIGGER_PURCH_ORD_PROD_DW> tempList = new List<TRIGGER_PURCH_ORD_PROD_DW>();
                        //tempList.Add(trow);
                        //await ProcessTPODW(tempList, "HZLM_02300VRT-A-54891");

                        Result<List<TRIGGER_PURCH_ORD_PROD_DW>> rtrigger_purchOrd_dw = await Paperless_DB.TRIGGER_PURCH_ORD_PROD_DW_List();

                        if (rtrigger_purchOrd_dw.IsSuccess)
                        {
                            // because the new rows enter the database split up like
                            // BLA_06400PO-044290_1, BLA_06400PO-044290_2_1, BLA_06400PO-044290_2_2, BLA_06400PO-044290_3_1 
                            // we collect these together under BLA_06400PO-044290 and process all the parts we have. So we pass around lists of Purch Ord Prods.
                            foreach (string UnCutIDSub in rtrigger_purchOrd_dw.Value.Select(x => x.KEYID.Split('_')[0] + "_" + x.KEYID.Split('_')[1]).Distinct())
                            {
                                await ProcessTPODW(rtrigger_purchOrd_dw.Value.Where(x => x.KEYID.StartsWith(UnCutIDSub)).ToList(), UnCutIDSub);
                            }
                         }
                         break;
                    case ReceiptEnum.AwaitingTable:
                        if (ReadAllAwaitingRowsFromAPI())
                        {
                            foreach (AwaitingPurchOrdDW.dbRow AwtRw in AllAwaitingPurchOrdDW)
                            {
                                UnCutID = AwtRw.KEYID;
                                purchOrd_dw = await ReadPurchOrdFromDB();
                                if (!purchOrd_dw.IsSuccess || purchOrd_dw.Value.Count() == 0 || !purchOrd_dw.Value[0].KEYID.Contains("_"))
                                    continue;
                                string UnCutKeyID = purchOrd_dw.Value[0].SITE_NO + "_" + purchOrd_dw.Value[0].KEYID.Split("_")[1];
                                await ProcessTPODW(new List<TRIGGER_PURCH_ORD_PROD_DW> { new TRIGGER_PURCH_ORD_PROD_DW { KEYID = UnCutKeyID } }, UnCutKeyID);
                                // if successful delete from awaiting table?
                            }
                        }
                        break;
                }
            }

            DateTime now = DateTime.MinValue;
            List<PURCH_ORD_PROD_DW> allrecp = new List<PURCH_ORD_PROD_DW>();
            Result<List<PURCH_ORD_PROD_DW>> purchOrd_dw = null;
            List<TRIGGER_PURCH_ORD_PROD_DW> my_trigger_records = null;
            private async Task ProcessTPODW(List<TRIGGER_PURCH_ORD_PROD_DW> in_my_trigger_records, string UnCutIDSub)
            {
                my_trigger_records = in_my_trigger_records;
                CanDeleteTriggerRecord = true;
                bool CanProcessTriggerRecord = true;
                now = DateTime.Now;

                UnCutID = UnCutIDSub;

                Boolean ProcessedSuccessfully = false;

                // Can Process trigger record
                if (CanProcess())
                {
                    if (await ReadEarlyPurchRecord())
                    {
                        if (await HasNoTransactionXref())
                        {
                            if (IsReceiptReadyToProcessPurch())
                            {
                                if (await ReadEarlyStockRecord())
                                {
                                    if (IsReceiptReadyToProcessStock())
                                    {
                                        if (DoTranslations())
                                        {
                                            if (ReadFromAPI())
                                            {
                                                if (await ProcessRecord())
                                                {
                                                    //if (!genExcel)
                                                    {
                                                        if (await Cost_Receipt())
                                                        {
                                                            ProcessedSuccessfully = true;
                                                            SaveFunction();
                                                        }
                                                        else
                                                            ReceiptsThatDidntComplete.Add((UnCutIDSub, failedErrorMessage));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else if (!genExcel && ReadAwaitingRowsFromAPI())
                                    {
                                        if (PutInAwaitingDbTable())
                                            CanDeleteTriggerRecord = true;
                                        }
                                    else
                                        ReceiptsThatDidntComplete.Add((UnCutIDSub, failedErrorMessage));
                                }
                                else
                                    ReceiptsThatDidntComplete.Add((UnCutIDSub, failedErrorMessage));
                            }
                            else if (!genExcel && ReadAwaitingRowsFromAPI())
                            {
                                if (PutInAwaitingDbTable())
                                    CanDeleteTriggerRecord = true;
                                }
                            else
                                ReceiptsThatDidntComplete.Add((UnCutIDSub, failedErrorMessage));
                        }
                        else
                            // here we have already processed a transaction with this receipt so we're just going to delete it
                            CanDeleteTriggerRecord = true;
                    }
                }
                else
                {
                    ReceiptsThatDidntComplete.Add((UnCutIDSub, failedErrorMessage));
                    CanProcessTriggerRecord = false;
                }

                CanDeleteTriggerRecord = false;
                if (CanDeleteTriggerRecord && !genExcel)
                {
                    // Delete trigger record
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_PURCH_ORD_PROD_DW_DeleteList(my_trigger_records);
                }

                Console.WriteLine(UnCutID + " : " + ProcessedSuccessfully);
            }

            private bool PutInAwaitingDbTable()
            {
                AwaitingPurchOrdDW APO = new AwaitingPurchOrdDW();
                APO.CalledFrom = GS.selfName;
                APO.ChangedTime = DateTime.Now;
                if (CurrentAwaitingPurchOrdDW.Count() == 1)
                {
                    CurrentAwaitingPurchOrdDW[0].LastUpdatedDate = APO.ChangedTime;
                    APO.dbRow_instance = CurrentAwaitingPurchOrdDW[0];
                    return CAPI.SaveToAPI(APO, "/api/AwaitingPurchOrdDWSave");
                }
                else
                {
                    AwaitingPurchOrdDW.dbRow AwtPO = new AwaitingPurchOrdDW.dbRow();
                    AwtPO.ID = 0;
                    AwtPO.InsertedDate = APO.ChangedTime;
                    AwtPO.LastUpdatedDate = null;
                    AwtPO.KEYID = UnCutID;
                    APO.dbRow_instance = AwtPO;

                    return CAPI.SaveToAPI(APO, "/api/AwaitingPurchOrdDWSave");
                }
            }
            List<AwaitingPurchOrdDW.dbRow> CurrentAwaitingPurchOrdDW;
            private bool ReadAwaitingRowsFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.AwaitingPurchOrdDW.Search
                {
                    KEYID = UnCutID,
                    SearchMode = wbm_common.DataObjects.AwaitingPurchOrdDW.Search.SearchModeType.KeyID
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "AwaitingPurchOrdDWSearch");
                if (response == null)
                {

                    logger.Error("receipt ReadAwaitingRowsFromAPI KEYID returned error: " + UnCutID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentAwaitingPurchOrdDW = JsonConvert.DeserializeObject<List<AwaitingPurchOrdDW.dbRow>>(response);
                        if (CurrentAwaitingPurchOrdDW.Count() > 1)
                        {
                            logger.Error("receipt ReadAwaitingRowsFromAPI should only have one row in the database, returned "
                                + CurrentAwaitingPurchOrdDW.Count().ToString() + " rows");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("receipt ReadAwaitingRowsFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }
            List<AwaitingPurchOrdDW.dbRow> AllAwaitingPurchOrdDW;
            private bool ReadAllAwaitingRowsFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.AwaitingPurchOrdDW.Search
                {
                    SearchMode = wbm_common.DataObjects.AwaitingPurchOrdDW.Search.SearchModeType.Undefined
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "AwaitingPurchOrdDWSearch");
                if (response == null)
                {
                    logger.Error("receipt ReadAllAwaitingRowsFromAPI returned error");
                    return false;
                }
                else
                {
                    try
                    {
                        AllAwaitingPurchOrdDW = JsonConvert.DeserializeObject<List<AwaitingPurchOrdDW.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("receipt ReadAllAwaitingRowsFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> ProcessRecord()
            {
                if (await GatherData())
                {
                    if (CurrentRdh.ProcessReceipt())
                        return true;
                else
                    {
                        ReceiptsThatDidntComplete.Add((UnCutID, CurrentRdh.failedErrorMessage));
                        return false;
                    }
                }
                ReceiptsThatDidntComplete.Add((UnCutID, "Couldn't read rest of database rows from API, " + failedErrorMessage));
                    return false;
            }
            private string failedErrorMessage = "";

            private async Task<bool> GatherData()
            {
                Boolean rtnVal = true;

                //PW Data Warehouse tables
                if (!await List_DbTrans())
                    rtnVal = false;

                if (!await List_Conts())
                    rtnVal = false;

                if (!await List_Pallet_AtInsert())
                    rtnVal = false;

                // WBM Tables
                if (!await List_WBM_Stock())
                    rtnVal = false;

                if (LoadOwnerDataRequired)
                {
                    CurrentOwner_dbRow = wbm_api.GetOwner_dbRow(CurrentOwnerID);

                    if (!await List_ActionRateCode())
                        rtnVal = false;

                    if (!await List_ZoneProcessing())
                        rtnVal = false;

                    if (!await List_StockRateCategory())
                        rtnVal = false;

                    if (!await List_DevanLookup())
                        rtnVal = false;

                    if (!await List_ContainerHeavyLift())
                        rtnVal = false;

                    if (!await List_ContainerLift())
                        rtnVal = false;

                    if (!await List_RateCode())
                        rtnVal = false;

                    if (!await List_TableRate())
                        rtnVal = false;

                    if (rtnVal)
                        LoadOwnerDataRequired = false;
                }

                if (!await List_ProcessingRule())
                    rtnVal = false;

                if (!await List_RateCollection())
                    rtnVal = false;

                CurrentRdh.ListActionRateCode = ListActionRateCode;
                CurrentRdh.ListZoneProcessing = ListZoneProcessing;
                CurrentRdh.ListStockRateCategory = ListStockRateCategory;
                CurrentRdh.ListProcessingRule = ListProcessingRule;
                CurrentRdh.ListDevanLookup = ListDevanLookup;
                CurrentRdh.ListContainerHeavyLift = ListContainerHeavyLift;
                CurrentRdh.ListContainerLift = ListContainerLift;
                CurrentRdh.ListRateCollection = ListRateCollection;

                return rtnVal;
            }

            private async Task<bool> List_StockMove()
            {
                Result<List<STOCK_MOVE_DW>> stockmove_dw = await Paperless_DB.STOCK_MOVE_DW_ListByInvNoOrOrdNo(CurrentReceiptNumber);

                if (stockmove_dw.IsSuccess)
                    CurrentRdh.ListStockMoveDW = stockmove_dw.Value;

                return stockmove_dw.IsSuccess;
            }

            Result<List<PALLET_DW>> pallet_dw = null;
            private async Task<bool> List_Pallet()
            {
                pallet_dw = await Paperless_DB.PALLET_DW_ListBySuppOrdNo(CurrentReceiptNumber);
                return pallet_dw.IsSuccess;
            }

            Result<List<WBM_PALLET_DW_ASATINSERT>> pallet_dw_atinserts = null;
            private async Task<bool> List_Pallet_AtInsert()
            {
                pallet_dw_atinserts = await Paperless_DB.WBM_PALLET_DW_ASASINSERT_ListByContNum(CurrentRdh.ListConts);

                if (pallet_dw_atinserts.IsSuccess)
                    CurrentRdh.ListPalletAsInsert = pallet_dw_atinserts.Value;

                return pallet_dw_atinserts.IsSuccess;
            }

            private async Task<bool> List_DbTrans()
            {
                Result<List<DB_TRANS>> dbTrans = null;

                dbTrans = await Paperless_DB.DB_TRANS_ListByReference(CurrentReceiptNumber);

                if (dbTrans.IsSuccess)
                    CurrentRdh.ListDBTrans = dbTrans.Value;

                return dbTrans.IsSuccess;
            }
            private async Task<bool> List_Conts()
            {
                Result<List<CONTAINER_DW>> dbConts = null;
                CurrentRdh.ListConts = CurrentRdh.ListPurchOrdProdDWs.Where(x => x.CONTAINER_NO != null && x.CONTAINER_NO != "")
                    .Select(x => x.CONTAINER_NO).Distinct().ToList();

                dbConts = await Paperless_DB.CONTAINER_DW_ListByListContainer(CurrentRdh.ListConts);

                if (dbConts.IsSuccess)
                    CurrentRdh.ListDBConts = dbConts.Value;

                return dbConts.IsSuccess;
            }

            string response;

            private async Task<bool> HasNoTransactionXref()
            {
                PaperlessTransactionXref.dbRow PaperlessTransactionXrefRow = null;
                PaperlessTransactionXref.Search so = new PaperlessTransactionXref.Search();
                so.SearchMode = PaperlessTransactionXref.Search.SearchModeType.PaperlessKeyID;
                so.PaperlessKeyID = UnCutID;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "PaperlessTransactionXrefSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt PaperlessTransactionXref returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        List<PaperlessTransactionXref.dbRow> ListPaperlessTransactionXref = JsonConvert.DeserializeObject<List<PaperlessTransactionXref.dbRow>>(response);

                        if (ListPaperlessTransactionXref != null && ListPaperlessTransactionXref.Count > 0)
                            return false;
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt Get_PaperlessTransactionXref " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_WBM_Stock()
            {
                Stock.Search so = new Stock.Search();
                so.SearchMode = Stock.Search.SearchModeType.Owner_ListProductCode;
                so.ListProductCode = CurrentRdh.ListSKU;
                so.ListOwnerID = new List<int>() { CurrentOwnerID };
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt Stocksearch returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        List<Stock.dbRow> stockList = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);

                        CurrentRdh.ListStock = stockList;

                        // Check that we have all required stock records
                        if (CurrentRdh.ListSKU.Count() != CurrentRdh.ListSKU.Count())
                        {
                            var missingSKUs = CurrentRdh.ListSKU.Except(CurrentRdh.ListStock.Select(x => x.ProductCode)).ToList();

                            failedErrorMessage = "Receipt StockSearch missing SKUs: " + string.Join(", ", missingSKUs);
                            logger.Error(failedErrorMessage);
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt List_WBM_Stock " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_ActionRateCode()
            {
                ActionRateCode.Search so = new ActionRateCode.Search();
                so.ListOwnerID = new List<int> { CurrentOwnerID };
                so.SearchMode = ActionRateCode.Search.SearchModeType.SingleOwner;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ActionRateCodeSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt ActionRateCode returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListActionRateCode = JsonConvert.DeserializeObject<List<ActionRateCode.dbRow>>(response);
                        ListActionRateCode = ListActionRateCode.OrderBy(arc => arc.OwnerID).ThenBy(arc => arc.SiteID).ToList();
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListActionRateCode " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }
            private int lastRtCllDfnID = 0;
            private async Task<bool> List_RateCollection()
            {
                if (CurrentOwner_dbRow == null)
                    return false;
                RateCollection.Search so = new RateCollection.Search();
                so.ListRateCollectionDefnID = new List<int> { CurrentOwner_dbRow.CurrentRateCollectionDefnID };

                // Check for a change in RateCollectionDefnID based on dates.

                if (purchOrd_dw.Value.Any(x => x.DATE_DEL != null) && CurrentOwner_dbRow.DateOfChange != null && purchOrd_dw.Value.First(x => x.DATE_DEL != null).DATE_DEL > CurrentOwner_dbRow.DateOfChange)
                    so.ListRateCollectionDefnID = new List<int> { CurrentOwner_dbRow.NewRateCollectionDefnID };
                so.SearchMode = RateCollection.Search.SearchModeType.SingleRateCollectionDefnID;

                // If the colection we already have are the same no need to fetch again

                if (lastRtCllDfnID == so.ListRateCollectionDefnID[0])
                    return true;
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "RateCollectionSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt RateCollection returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListRateCollection = JsonConvert.DeserializeObject<List<RateCollection.dbRow>>(response);
                        lastRtCllDfnID = so.ListRateCollectionDefnID[0];
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListRateCollection " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_RateCode()
            {
                RateCode.Search so = new RateCode.Search();
                so.SearchMode = RateCode.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "RateCodeSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt RateCode returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListRateCode = JsonConvert.DeserializeObject<List<RateCode.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListRateCode" + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_TableRate()
            {
                TableRate.Search so = new TableRate.Search();
                so.SearchMode = TableRate.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "TableRateSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt TableRate returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListTableRate = JsonConvert.DeserializeObject<List<TableRate.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListTableRate " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_ZoneProcessing()
            {
                ZoneProcessing.Search so = new ZoneProcessing.Search();
                so.SearchMode = ZoneProcessing.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ZoneProcessingSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt ZoneProcessing returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListZoneProcessing = JsonConvert.DeserializeObject<List<ZoneProcessing.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListZoneProcessing " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_StockRateCategory()
            {
                StockRateCategory.Search so = new StockRateCategory.Search();
                so.SearchMode = StockRateCategory.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockRateCategorySearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt StockRateCategory returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        ListStockRateCategory = JsonConvert.DeserializeObject<List<StockRateCategory.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt ListStockRateCategory " + ex;
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private int lastPrDfnID = 0;
            private async Task<bool> List_ProcessingRule()
            {
                if (CurrentOwner_dbRow == null)
                    return false;
                ProcessingRule.Search so = new ProcessingRule.Search();
                so.SearchMode = ProcessingRule.Search.SearchModeType.ProcessingRuleDefnID;
                so.ProcessingRuleDefnID = CurrentOwner_dbRow.CurrentProcessingRuleDefnID;
                if (purchOrd_dw.Value.Any(x => x.DATE_DEL != null) && CurrentOwner_dbRow.DateOfChange != null && purchOrd_dw.Value.First(x => x.DATE_DEL != null).DATE_DEL > CurrentOwner_dbRow.DateOfChange)
                    so.ProcessingRuleDefnID = CurrentOwner_dbRow.NewProcessingRuleDefnID;
                if (lastPrDfnID == so.ProcessingRuleDefnID)
                    return true;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ProcessingRuleSearch");
                if (response == null)
                {
                    failedErrorMessage = "Receipt ProcessingRuleSearch returned no response";
                    logger.Error("Receipt ProcessingRuleSearch returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListProcessingRule = JsonConvert.DeserializeObject<List<ProcessingRule.dbRow>>(response);

                        // Only want paperless Receipt process
                        ListProcessingRule = ListProcessingRule.Where(pr => pr.ProcessCategoryID == 2).ToList();
                        lastPrDfnID = so.ProcessingRuleDefnID;
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt List_ProcessingRule " + ex;
                        logger.Error("Receipt List_ProcessingRule " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_DevanLookup()
            {
                if (CurrentOwner_dbRow != null)
                {
                    DevanLookup.Search so = new DevanLookup.Search();
                    so.SearchMode = DevanLookup.Search.SearchModeType.AllRecords;

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "DevanLookupSearch");
                    if (response == null)
                    {
                        failedErrorMessage = "Receipt DevanLookupSearch returned no response";
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                    else
                    {
                        try
                        {
                            ListDevanLookup = JsonConvert.DeserializeObject<List<DevanLookup.dbRow>>(response);

                            // Only want paperless Receipt process
                            ListDevanLookup = ListDevanLookup.Where(pr => pr.OwnerID == CurrentOwner_dbRow.ID).ToList();
                            if (ListDevanLookup.Count() == 0)
                                return false;
                        }
                        catch (Exception ex)
                        {
                            failedErrorMessage = "Receipt List_DevanLookup" + ex;
                            logger.Error(failedErrorMessage);

                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private async Task<bool> List_ContainerHeavyLift()
            {
                if (CurrentOwner_dbRow != null)
                {
                    ContainerHeavyLift.Search so = new ContainerHeavyLift.Search();
                    so.SearchMode = ContainerHeavyLift.Search.SearchModeType.AllRecords;
                    //so.SearchMode = ContainerHeavyLift.Search.SearchModeType.SingleOwnerID;
                    //so.ListOwnerID = new List<int> { CurrentOwner_dbRow.ID };

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "ContainerHeavyLiftSearch");
                    if (response == null)
                    {
                        failedErrorMessage = "Receipt ContainerHeavyLiftSearch returned no response";
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                    else
                    {
                        try
                        {
                            ListContainerHeavyLift = JsonConvert.DeserializeObject<List<ContainerHeavyLift.dbRow>>(response);
                        }
                        catch (Exception ex)
                        {
                            failedErrorMessage = "Receipt List_ContainerHeavyLift" + ex;
                            logger.Error(failedErrorMessage);

                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private async Task<bool> List_ContainerLift()
            {
                if (CurrentOwner_dbRow != null)
                {
                    ContainerLift.Search so = new ContainerLift.Search();
                    so.SearchMode = ContainerLift.Search.SearchModeType.AllRecords;
                    //so.SearchMode = ContainerLift.Search.SearchModeType.SingleOwnerID;
                    //so.ListOwnerID = new List<int> { CurrentOwner_dbRow.ID };

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "ContainerLiftSearch");
                    if (response == null)
                    {
                        failedErrorMessage = "Receipt ContainerLiftSearch returned no response";
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                    else
                    {
                        try
                        {
                            ListContainerLift = JsonConvert.DeserializeObject<List<ContainerLift.dbRow>>(response);
                        }
                        catch (Exception ex)
                        {
                            failedErrorMessage = "Receipt List_ContainerLift" + ex;
                            logger.Error(failedErrorMessage);

                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }
            private async Task<bool> Cost_Receipt()
            {
                Transaction_Costing tc = new Transaction_Costing();
                tc.thRow = CurrentRdh.TransactionHolder.TransactionHeader_Row;
                tc.ListTransactionDetail_Costing = new List<Transaction_Costing.TransactionDetail_Costing>();

                Transaction_Costing.TransactionDetail_Costing tdc;
                foreach (TransactionDetail.dbRow td in CurrentRdh.TransactionHolder.ListTransactionDetail)
                {
                    tdc = new Transaction_Costing.TransactionDetail_Costing();
                    tdc.tdRow = td;
                    tc.ListTransactionDetail_Costing.Add(tdc);
                }

                Transaction_CostingHolder sendtch = new Transaction_CostingHolder(tc);
                Transaction_CostingHolder returntch = null;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(sendtch);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "TransactionCostingCost");
                if (response == null)
                {
                    failedErrorMessage = "Receipt TransactionCostingCost returned no response";
                    logger.Error(failedErrorMessage);

                    return false;
                }
                else
                {
                    try
                    {
                        returntch = JsonConvert.DeserializeObject<Transaction_CostingHolder>(response);
                    }
                    catch (Exception ex)
                    {
                        failedErrorMessage = "Receipt TransactionCostingCost deserialise " + ex;
                        logger.Error(failedErrorMessage);
                        return false;
                    }

                    // only here if successful deserialise
                    if (returntch.CostedOK == true)
                    {
                        if (returntch.ListTransactionCosting[0].CostedOK == false)
                        {
                            failedErrorMessage = "Receipt TransactionCostingCost returned CostedOK = false";
                            logger.Error(failedErrorMessage);

                            return false;
                        }
                        else
                        {
                            ProcessReturnCosting(returntch.ListTransactionCosting[0]);
                        }
                    }
                    else
                    {
                        failedErrorMessage = "Receipt TransactionCostingCost returned no costing records";
                        logger.Error(failedErrorMessage);

                        return false;
                    }
                }
                return true;
            }

            private void ProcessReturnCosting(wbm_common.NonDatabaseObjects.Transaction_Costing tc)
            {
                CurrentRdh.TransactionHolder.TransactionHeader_Row = tc.thRow;
                CurrentRdh.TransactionHolder.ListTransactionDetail.Clear();
                foreach (Transaction_Costing.TransactionDetail_Costing tdc in tc.ListTransactionDetail_Costing)
                {
                    TransactionDetail.dbRow td = tdc.tdRow;
                    CurrentRdh.TransactionHolder.ListTransactionDetail.Add(td);
                }
            }

            private bool SaveFunction()
            {
                allrecp.AddRange(CurrentRdh.ListPurchOrdProdDWs);
                ListTrans.Add(new WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans(CurrentRdh.TransactionHolder.TransactionHeader_Row, CurrentRdh.TransactionHolder.ListTransactionDetail));
                ReceiptCount++;

                try
                {
                    SaveAPIFunctionCall();
                }
                catch (Exception ex)
                {
                    logger.Error("Carrier SaveFunction " + ex);
                    return false;
                }

                return true;
            }

            private bool DoTranslations()
            {
                // No trans
                return true;
            }

            private async Task<bool> ReadEarlyPurchRecord()
            {
                purchOrd_dw = await ReadPurchOrdFromDB();
                if (purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                // this is needed earlier so put it here
                CurrentRdh = new ReceiptDataHolder(CurrentCompanyID, CurrentSiteID, CurrentOwnerID);
                if (purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() > 0)
                {
                    CurrentRdh.ListPurchOrdProdDWs = purchOrd_dw.Value;
                    CurrentRdh.setPurchOrdProdDWWithDate();
                }
                CurrentRdh.UnCutID = UnCutID;
                CurrentRdh.addDebug = genExcel;

                return purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() > 0;
            }

            private async Task<bool> ReadEarlyStockRecord()
            {
                if (!await List_StockMove())
                    return false;

                if (!await List_Pallet())
                    return false;

                if (LoadOwnerDataRequired && !await List_ZoneProcessing())
                    return false;
                CurrentRdh.ListZoneProcessing = ListZoneProcessing;

                return true;
            }

            private void SaveAPIFunctionCall()
            {
                CurrentRdh.TransactionHolder.CalledFrom = GS.selfName;
                CurrentRdh.TransactionHolder.ChangedTime = now;

                if (CAPI.SaveToAPI(CurrentRdh.TransactionHolder, "/api/TransactionHolderSave"))
                    CanDeleteTriggerRecord = true;
                else
                    CanDeleteTriggerRecord = false; // error 
            }

            private bool ReadFromAPI()
            {
                return true;
            }
            private async Task<Result<List<PURCH_ORD_PROD_DW>>> ReadPurchOrdFromDB()
            {
                return await Paperless_DB.PURCH_ORD_PROD_DW_ListByKeyID(UnCutID);
            }

            private Boolean IsReceiptReadyToProcessPurch()
            {
                // this is also required for it to be complete
                if (purchOrd_dw.Value.Any(x => x.DATE_RECV != null && x.PO_CLR_LOAD_FLG == "U"))
                    return true;
                else
                    return false;
            }

            private Boolean IsReceiptReadyToProcessStock()
            {
                // This routine will check that the quanity of check.in matches with the quantity of put.away and full.rep. 
                // This is done at item level
                if (CurrentRdh.CheckInPutawayPass())
                {
                    // If all items are away, then we can proceed

                    #region OldCode
                    /*
                    // The receipt information comes in parts so we test if it's all in if all the put aways, checkins and full replenishes in recieving zones matches the pallet table
                    List<string> Relaventpalnosinstockmoves =
                        CurrentRdh.ListStockMoveDW.Where(x => x.MOVE_TYPE == "PUT.AWAY" || x.MOVE_TYPE == "CHECK.IN"
                        || (x.MOVE_TYPE == "FULL.REP"
                        // this line just checks that we can find the site so we won't get a null reference, since there are a lot of VALID ones won't be found by our search
                        && ListZoneProcessing.Any(y => y.ZoneNumber == x.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(x.SITE_NO).Item1)
                        // now check it's receiving
                        && ListZoneProcessing.First(y => y.ZoneNumber == x.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(x.SITE_NO).Item1).IsReceiving)
                        ).Select(x => x.PAL_NO).Distinct().ToList();
                    List<string> justPalNo = pallet_dw.Value.Select(x => x.PAL_NO).Distinct().ToList();
                    // check the same pallet numbers are present in both lists
                    if (Relaventpalnosinstockmoves.Where(x => !justPalNo.Contains(x)).Count() != 0
                        || justPalNo.Where(x => !Relaventpalnosinstockmoves.Contains(x)).Count() != 0)
                    {
                        failedErrorMessage = "IsReceiptReadyToProcess failed pallet count check";
                        return false;
                    }
                    */
                    #endregion

                    return true;
                }
                else
                {
                    failedErrorMessage = "CheckPutawayPass failed - " + CurrentRdh.TotalCheckIns + " vs " + CurrentRdh.TotalPutawaysAndReplns;
                    return false;
                }
            }

            private Boolean CanProcess()
            {
                // First part of KEYID is the site; need to see if the site is enabled before proceeding further
                String[] keyid_parts = UnCutID.Split('_');
                CurrentReceiptNumber = UnCutID.Split('_')[1];
                if (keyid_parts.Length > 0) // we can combine these but also could just show when key or owner is missing
                {
                    if (keyid_parts.Length > 1 && keyid_parts[1].Length >= 5)
                    {
                        Site = keyid_parts[0];
                        Owner = keyid_parts[1].Substring(0, 5);
                        wbm_api.FieldState OwnerFS = wbm_api.FieldState.NotFound;
                        wbm_api.FieldState SiteFS = wbm_api.FieldState.NotFound;
                        if (Owner == PreviousOwner && Site == PreviousSiteName)
                        {
                            // Results for this site will be same as previous, re-use
                            CanDeleteTriggerRecord = PreviousCanDeleteTriggerRecord;
                            return PreviousCanProcessTriggerRecord;
                        }
                        else if (Owner == PreviousOwner)
                        {
                            // if the previous loop had the same owner we don't look up again
                            OwnerFS = previousOwnerFS;
                            SiteFS = FindSiteFS();
                            previousSiteFS = SiteFS;
                        }
                        else if (Site == PreviousSiteName)
                        {
                            // if the previous loop had the same site we don't look up again
                            SiteFS = previousSiteFS;
                            OwnerFS = FindOwnerFS();
                            previousOwnerFS = OwnerFS;
                        }
                        else
                        {
                            SiteFS = FindSiteFS();
                            previousSiteFS = SiteFS;
                            OwnerFS = FindOwnerFS();
                            previousOwnerFS = OwnerFS;
                        }


                        if (SiteFS == wbm_api.FieldState.NotFound || OwnerFS == wbm_api.FieldState.NotFound)
                        {
                            // don't delete, don't process
                            PreviousCanDeleteTriggerRecord = false;
                            PreviousCanProcessTriggerRecord = false;
                            if (SiteFS == wbm_api.FieldState.NotFound && OwnerFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(UnCutID, Owner + ", " + Site, EmailHelper.IssueType.Multiple, "Receipt");
                            else if (SiteFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(UnCutID, Site, EmailHelper.IssueType.Site, "Receipt");
                            else if (OwnerFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(UnCutID, Owner, EmailHelper.IssueType.Owner, "Receipt");

                            failedErrorMessage = "site or owner not found";
                            return false;
                        }
                        else if (SiteFS == wbm_api.FieldState.Inactive || OwnerFS == wbm_api.FieldState.Inactive)
                        {
                            // both exist, one or more is inactive, delete and don't process
                            CanDeleteTriggerRecord = true;
                            PreviousCanDeleteTriggerRecord = true;
                            PreviousCanProcessTriggerRecord = false;

                            failedErrorMessage = "site or owner inactive";
                            return false;
                        }
                        else
                        {
                            // both exist and are active, proceed to process, but whether it can be deleted gets deteremined later on
                            PreviousCanDeleteTriggerRecord = false;
                            PreviousCanProcessTriggerRecord = true;
                            return true;
                        }
                    }
                }
                else
                    EmailHelper.SendEmail(UnCutID, "", EmailHelper.IssueType.Format, "Receipt");

                return false;
            }

            private wbm_api.FieldState previousSiteFS = wbm_api.FieldState.NotFound;
            private wbm_api.FieldState previousOwnerFS = wbm_api.FieldState.NotFound;


            private wbm_api.FieldState FindOwnerFS()
            {
                PreviousOwner = Owner;
                LoadOwnerDataRequired = true;
                CurrentOwnerID = wbm_api.GetOwnerID(Owner);
                if (CurrentOwnerID == 0) // site doesn't exist
                    return wbm_api.FieldState.NotFound;
                else if (!wbm_api.IsOwnerActive(CurrentOwnerID)) // non 0 owner id
                    return wbm_api.FieldState.Inactive;
                return wbm_api.FieldState.Active;
            }

            private wbm_api.FieldState FindSiteFS()
            {
                PreviousSiteName = Site;
                (int, int) SiteIDCompanyID = wbm_api.GetSiteNumberCompanyID(Site);
                if (SiteIDCompanyID == (0, 0)) // site doesn't exist
                    return wbm_api.FieldState.NotFound;
                CurrentSiteID = SiteIDCompanyID.Item1;
                CurrentCompanyID = SiteIDCompanyID.Item2;
                if (CurrentSiteID != 0 && !wbm_api.IsSiteActive(CurrentSiteID))
                    return wbm_api.FieldState.Inactive;
                return wbm_api.FieldState.Active;
            }

            public async Task ProduceExcel()
            {
                // First cost these jobs

                tempCosting tc = new tempCosting();
                tc.ListTableRate = ListTableRate;
                tc.ListRateCollection = ListRateCollection;
                tc.ListRateCode = ListRateCode;
                tc.ProcessListTrans(ref ListTrans);

                WBM_BackgroundProcessor.Models.Excel.TransExcel excelReport = new Models.Excel.TransExcel();
                excelReport.ListTrans = ListTrans;
                excelReport.ExcelValues = ExcelValues;
                excelReport.ValuesThatDidntComplete = ReceiptsThatDidntComplete;

                await excelReport.GenerateExcel();

                WBM_BackgroundProcessor.Helpers.general.debugWriteFile(excelReport.ms, "ReceiptExcel");
            }
        }
    }
}
