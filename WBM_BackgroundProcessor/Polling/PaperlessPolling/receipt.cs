using Azure;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Helpers;
using WBM_BackgroundProcessor.Models;
using WBM_BackgroundProcessor.Models.DataHolders;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class receipt
    {
        public class Poll
        {
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

            private List<string> ListIgnoreKey = new List<string>();
            public int ReceiptCount = 0;

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_PURCH_ORD_PROD_DW>> rtrigger_purchOrd_dw;

                rtrigger_purchOrd_dw = await Paperless_DB.TRIGGER_PURCH_ORD_PROD_DW_List();

                if (rtrigger_purchOrd_dw.IsSuccess)
                {
                    // because the new rows enter the database split up like
                    // BLA_06400PO-044290_1, BLA_06400PO-044290_2_1, BLA_06400PO-044290_2_2, BLA_06400PO-044290_3_1 
                    // we collect these together under BLA_06400PO-044290 and process all the parts we have. So we pass around lists of Purch Ord Prods.
                    List<string> PublicIDsUncut = rtrigger_purchOrd_dw.Value
                        .Where(x => x.KEYID.Contains("VPO-A-26506") ||
x.KEYID.Contains("VPO-A-27041") ||
x.KEYID.Contains("VPO-A-27104") ||
x.KEYID.Contains("VPO-A-27166A") ||
x.KEYID.Contains("VPO-A-27265") ||
x.KEYID.Contains("VPO-A-27274") ||
x.KEYID.Contains("VPO-A-27356") ||
x.KEYID.Contains("VPO-A-27390") ||
x.KEYID.Contains("VPO-A-27401") ||
x.KEYID.Contains("VPO-A-27457") ||
x.KEYID.Contains("VPO-A-27469") ||
x.KEYID.Contains("VPO-A-27574") ||
x.KEYID.Contains("VPO-A-27613") ||
x.KEYID.Contains("VPO-A-27624") ||
x.KEYID.Contains("VPO-A-27645") ||
x.KEYID.Contains("VPO-A-27681") ||
x.KEYID.Contains("VPO-A-27695") ||
x.KEYID.Contains("VPO-A-27709") ||
x.KEYID.Contains("VPO-A-28742")
)
                        .Where(x => x.KEYID.Contains("_"))
                        .Select(x => x.KEYID.Split('_')[0] + "_" + x.KEYID.Split('_')[1]).Distinct()
                        .Where(x => !ListIgnoreKey.Contains(x)).ToList();
                    foreach (string UnCutIDSub in PublicIDsUncut)
                    {
                        await ProcessTPODW(rtrigger_purchOrd_dw.Value.Where(x => x.KEYID.StartsWith(UnCutIDSub)).ToList(), UnCutIDSub);
                        if (ReceiptCount > 100)
                            break;
                    }
                    if (ReceiptCount > 0)
                        ProduceExcel();
                }
            }

            Result<List<PURCH_ORD_PROD_DW>> purchOrd_dw = null;
            private async Task ProcessTPODW(List<TRIGGER_PURCH_ORD_PROD_DW> my_trigger_records, string UnCutIDSub)
            {
                CanDeleteTriggerRecord = true;
                bool CanProcessTriggerRecord = true;

                UnCutID = UnCutIDSub;

                Boolean ProcessedSuccessfully = false;

                // Can Process trigger record
                if (CanProcess())
                {
                    if (await ReadEarlyWHDRecords())
                    {
                        if (true)//IsReceiptReadyToProcess())
                        {
                            if (DoTranslations())
                            {
                                if (ReadFromAPI())
                                {
                                    if (await ProcessRecord())
                                    {
                                        ProcessedSuccessfully = true;
                                        SaveFunction();
                                    }
                                }
                            }
                        }
                        else
                        {
                            CanDeleteTriggerRecord = false;
                        }
                    }
                }
                else
                    CanProcessTriggerRecord = false;

                CanDeleteTriggerRecord = false;
                if (CanDeleteTriggerRecord)
                {
                    // Delete trigger record
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_PURCH_ORD_PROD_DW_DeleteList(my_trigger_records);
                }

                Console.WriteLine(UnCutID + " : " + ProcessedSuccessfully);
            }
            private async Task<bool> ProcessRecord()
            {
                // We've read the PICK.HEAD record; need to create Receipt data holder and read in other required data
                CurrentRdh.ListPurchOrdProdDWs = purchOrd_dw.Value;

                if (await GatherData())
                    return CurrentRdh.ProcessReceipt();
                else
                    return false;
            }

            private async Task<bool> GatherData()
            {
                Boolean rtnVal = true;

                //PW Data Warehouse tables
                if (!await List_DbTrans())
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

                    if (!await List_ProcessingRule())
                        rtnVal = false;

                    if (!await List_DevanLookup())
                        rtnVal = false;

                    if (!await List_ContainerHeavyLift())
                        rtnVal = false;

                    if (!await List_ContainerLift())
                        rtnVal = false;

                    if (!await List_RateCollection())
                        rtnVal = false;

                    if (!await List_RateCode())
                        rtnVal = false;

                    if (!await List_TableRate())
                        rtnVal = false;

                    if (rtnVal)
                        LoadOwnerDataRequired = false;
                }

                CurrentRdh.ListActionRateCode = ListActionRateCode;
                CurrentRdh.ListZoneProcessing = ListZoneProcessing;
                CurrentRdh.ListStockRateCategory = ListStockRateCategory;
                CurrentRdh.ListProcessingRule = ListProcessingRule;
                CurrentRdh.ListDevanLookup = ListDevanLookup;
                CurrentRdh.ListContainerHeavyLift = ListContainerHeavyLift;
                CurrentRdh.ListContainerLift = ListContainerLift;

                return rtnVal;
            }

            private async Task<bool> List_StockMove()
            {
                Result<List<STOCK_MOVE_DW>> stockmove_dw = null;

                stockmove_dw = await Paperless_DB.STOCK_MOVE_DW_ListByInvNoStartsWith(CurrentReceiptNumber);

                if (stockmove_dw.IsSuccess)
                    CurrentRdh.ListStockMoveDW = stockmove_dw.Value;

                return stockmove_dw.IsSuccess;
            }

            private async Task<bool> List_DbTrans()
            {
                Result<List<DB_TRANS>> dbTrans = null;

                dbTrans = await Paperless_DB.DB_TRANS_ListByReference(CurrentReceiptNumber);

                if (dbTrans.IsSuccess)
                    CurrentRdh.ListDBTrans = dbTrans.Value;

                return dbTrans.IsSuccess;
            }

            string response;

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
                    logger.Error("Receipt StockSearch returned no response");
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
                            logger.Error("Receipt StockSearch missing SKUs: " + string.Join(", ", missingSKUs));
                            return false;
                        }
                        else
                        {
                            int i = 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Receipt List_WBM_Stock " + ex);
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
                    logger.Error("Receipt ActionRateCode returned no response");
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
                        logger.Error("Owner List_ActionRateCode " + ex);
                        return false;
                    }
                }
                return true;
            }
            private async Task<bool> List_RateCollection()
            {
                RateCollection.Search so = new RateCollection.Search();
                so.ListRateCollectionDefnID = new List<int> { 1 };
                so.SearchMode = RateCollection.Search.SearchModeType.SingleRateCollectionDefnID;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "RateCollectionSearch");
                if (response == null)
                {
                    logger.Error("Order RateCollection returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListRateCollection = JsonConvert.DeserializeObject<List<RateCollection.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Owner List_RateCollection " + ex);
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
                    logger.Error("Order RateCode returned no response");
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
                        logger.Error("Owner List_RateCode " + ex);
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
                    logger.Error("Order TableRate returned no response");
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
                        logger.Error("Owner TableRate " + ex);
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
                    logger.Error("Receipt ZoneProcessing returned no response");
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
                        logger.Error("List_ZoneProcessing " + ex);
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
                    logger.Error("Receipt StockRateCategory returned no response");
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
                        logger.Error("List_StockRateCategory " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_ProcessingRule()
            {
                if (CurrentOwner_dbRow != null)
                {
                    ProcessingRule.Search so = new ProcessingRule.Search();
                    so.SearchMode = ProcessingRule.Search.SearchModeType.ProcessingRuleDefnID;
                    so.ProcessingRuleDefnID = CurrentOwner_dbRow.CurrentProcessingRuleDefnID;

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "ProcessingRuleSearch");
                    if (response == null)
                    {
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

                        }
                        catch (Exception ex)
                        {
                            logger.Error("Receipt ListPriorityReceipt " + ex);
                            return false;
                        }
                    }
                    return true;
                }
                return false;
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
                        logger.Error("Receipt DevanLookupSearch returned no response");
                        return false;
                    }
                    else
                    {
                        try
                        {
                            ListDevanLookup = JsonConvert.DeserializeObject<List<DevanLookup.dbRow>>(response);

                            // Only want paperless Receipt process
                            ListDevanLookup = ListDevanLookup.Where(pr => pr.OwnerID == CurrentOwner_dbRow.ID).ToList();

                        }
                        catch (Exception ex)
                        {
                            logger.Error("Receipt List_DevanLookup " + ex);
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
                        logger.Error("Receipt ContainerHeavyLiftSearch returned no response");
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
                            logger.Error("Receipt List_ContainerHeavyLift " + ex);
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
                        logger.Error("Receipt ContainerLiftSearch returned no response");
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
                            logger.Error("Receipt List_ContainerLift " + ex);
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private bool SaveFunction()
            {
                ListTrans.Add(new WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans(CurrentRdh.TransactionHeader, CurrentRdh.ListTransactionDetail));
                ReceiptCount++;

                try
                {
                    //if (CurrentCarAdd.dbRow_instance.ID == 0)
                    //{
                    //    SaveAPIFunctionCall();
                    //}
                    //else if (isCarrierDifferent())
                    //{
                    //    // Only update if something has changed to avoid unnecessary API calls
                    //    SaveAPIFunctionCall();
                    //}
                    //else
                    //{
                    //    // No changes, so can delete trigger record but don't need to call API
                    //    CanDeleteTriggerRecord = true;
                    //}
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

            private async Task<bool> ReadEarlyWHDRecords()
            {
                purchOrd_dw = await ReadPurchOrdFromDB();
                if (purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                // this is needed earlier than orders so put it here
                CurrentRdh = new ReceiptDataHolder(CurrentCompanyID, CurrentSiteID, CurrentOwnerID);
                CurrentRdh.UnCutID = UnCutID;
                if (!await List_StockMove())
                    return false;

                return purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() > 0 && CurrentRdh.CheckInPutawayPass();
            }

            private void SaveAPIFunctionCall()
            {
                //CurrentCarAdd.CalledFrom = GS.selfName;
                //CurrentCarAdd.ChangedTime = now;

                //if (CAPI.SaveToAPI(CurrentCarAdd, "/api/CarrierSave"))
                //    CanDeleteTriggerRecord = true;
                //else
                //    CanDeleteTriggerRecord = false; // error 
            }

            private bool ReadFromAPI()
            {
                return true;
            }
            private async Task<Result<List<PURCH_ORD_PROD_DW>>> ReadPurchOrdFromDB()
            {
                return await Paperless_DB.PURCH_ORD_PROD_DW_ListByKeyID(UnCutID);
            }

            private Boolean IsReceiptReadyToProcess()
            {
                if (purchOrd_dw.Value[0].DATE_RECV != null && purchOrd_dw.Value[0].PO_CLR_LOAD_FLG == "U")
                    return true;
                else
                    return false;
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
                            return false;
                        }
                        else if (SiteFS == wbm_api.FieldState.Inactive || OwnerFS == wbm_api.FieldState.Inactive)
                        {
                            // both exist, one or more is inactive, delete and don't process
                            CanDeleteTriggerRecord = true;
                            PreviousCanDeleteTriggerRecord = true;
                            PreviousCanProcessTriggerRecord = false;
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

                WBM_BackgroundProcessor.Models.Excel.TransExcel excelReport = new Models.Excel.TransExcel(ListTrans);

                await excelReport.GenerateExcel();

                WBM_BackgroundProcessor.Helpers.general.debugWriteFile(excelReport.ms, "ReceiptExcel");


            }
        }
    }
}
