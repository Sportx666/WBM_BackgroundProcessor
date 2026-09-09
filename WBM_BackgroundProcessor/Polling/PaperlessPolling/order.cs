using Newtonsoft.Json;
using NLog;
using NLog.Web;
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
    public class order
    {
        public class Poll
        {
            public enum OrderEnum
            {
                Excel = 0,
                TriggerRows = 1,
                AwaitingTable = 2
            }
            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            TRIGGER_PICK_HEAD_DW trigger_record;

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
            Owner.dbRow CurrentOwner_dbRow = null;

            OrderDataHolder CurrentOdh = null;
            string CurrentCustomerAddressCustomerNumber = "";

            List<ActionRateCode.dbRow> ListActionRateCode = null;
            List<CustomerAddressDefault.dbRow> ListCustomerAddressDefault = null;
            List<ConnoteFee.dbRow> ListConnoteFee = null;
            List<PriorityOrder.dbRow> ListPriorityOrder = null;
            Dictionary<string, ZoneProcessing.dbRow> DictZoneProcessing = new Dictionary<string, ZoneProcessing.dbRow>();
            List<ProcessingRule.dbRow> ListProcessingRule = null;
            List<RateCollection.dbRow> ListRateCollection = null;
            List<RateCode.dbRow> ListRateCode = null;
            List<TableRate.dbRow> ListTableRate = null;
            List<Carrier.dbRow> ListCarrier = null;


            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public List<WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans> ListTrans = new List<WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans>();

            public int OrderCount = 0;
            WBM_BackgroundProcessor.Models.Excel.TransExcel excelReport;
            Dictionary<string, List<EERow>> ValuesFromExcel;
            bool genExcel = false;
            List<(string, string)> OrdersThatDidntComplete = new List<(string, string)>();

            public async Task PollTriggerTable(OrderEnum ordEnm)
            {
                switch (ordEnm)
                {
                    case OrderEnum.Excel:
                        genExcel = true;
                        excelReport = new Models.Excel.TransExcel();
                        ValuesFromExcel = ExcelExtraction.ExtractOrder();
                        foreach (string ckeyid in ValuesFromExcel.Keys)
                        {
                            trigger_record = new TRIGGER_PICK_HEAD_DW { KEYID = ckeyid };
                            pickhead_dw = await ReadPickHeadFromDB();
                            if (!pickhead_dw.IsSuccess || pickhead_dw.Value.Count() == 0)
                            {
                                OrdersThatDidntComplete.Add((ckeyid, "Unable to read Pick Head"));
                                continue;
                            }
                            trigger_record.KEYID = pickhead_dw.Value[0].KEYID;
                            await ProcessTPHDW(trigger_record);
                        }
                        if (OrderCount > 0)
                            ProduceExcel();
                        break;
                    case OrderEnum.TriggerRows:
                        Result<List<TRIGGER_PICK_HEAD_DW>> rtrigger_pickhead_dw = await Paperless_DB.TRIGGER_PICK_HEAD_DW_List();
                        if (rtrigger_pickhead_dw.IsSuccess)
                        {
                            foreach (TRIGGER_PICK_HEAD_DW my_trigger_record in rtrigger_pickhead_dw.Value)
                            {
                                await ProcessTPHDW(my_trigger_record);
                            }
                        }
                        break;
                    case OrderEnum.AwaitingTable:
                        if (ReadAllAwaitingRowsFromAPI())
                        {
                            foreach (AwaitingPickHeadDW.dbRow AwtRw in AllAwaitingPickHeadDW)
                            {
                                trigger_record = new TRIGGER_PICK_HEAD_DW { KEYID = AwtRw.KEYID };
                                pickhead_dw = await ReadPickHeadFromDB();
                                if (!pickhead_dw.IsSuccess || pickhead_dw.Value.Count() == 0)
                                    continue;
                                trigger_record.KEYID = pickhead_dw.Value[0].KEYID;
                                await ProcessTPHDW(trigger_record);
                                // if successful delete from awaiting table?
                            }
                        }
                        break;
                }
            }

            DateTime now = DateTime.MinValue;
            Result<List<PICK_HEAD_DW>> pickhead_dw = null;
            private async Task ProcessTPHDW(TRIGGER_PICK_HEAD_DW my_trigger_record)
            {
                CanDeleteTriggerRecord = true;
                bool CanProcessTriggerRecord = true;
                now = DateTime.Now;

                trigger_record = my_trigger_record;

                Boolean ProcessedSuccessfully = false;

                // Can Process trigger record
                if (CanProcess())
                {
                    if (await ReadRecord())
                    {
                        if (await HasNoTransactionXref())
                        {
                            if (IsOrderReadyToProcess())
                            {
                                if (DoTranslations())
                                {
                                    if (ReadFromAPI())
                                    {
                                        if (await ProcessRecord())
                                        {
                                            if (!genExcel)
                                            {
                                                if (await Cost_Order())
                                                {
                                                    ProcessedSuccessfully = true;
                                                    SaveFunction();
                                                }
                                                else
                                                    OrdersThatDidntComplete.Add((trigger_record.KEYID, "Order failed to cost"));
                                            }
                                        }
                                        else
                                            OrdersThatDidntComplete.Add((trigger_record.KEYID, "Order database rows failed to read from API"));
                                    }
                                }
                            }
                            else if (!genExcel && ReadAwaitingRowsFromAPI())
                            {
                                if (PutInAwaitingDbTable())
                                {
                                    CanDeleteTriggerRecord = true;
                                }
                            }
                            else
                                OrdersThatDidntComplete.Add((trigger_record.KEYID, "Order not ready to process"));
                        }
                        else
                            // here we have already processed a transaction with this order so we're just going to delete it
                            CanDeleteTriggerRecord = true;
                    }
                    else
                        OrdersThatDidntComplete.Add((trigger_record.KEYID, "Stock Move, dbtrans or pick detail failed to read"));
                }
                else
                {
                    OrdersThatDidntComplete.Add((trigger_record.KEYID, "Site or Owner Error"));
                    CanProcessTriggerRecord = false;
                }

                CanDeleteTriggerRecord = false;
                if (CanDeleteTriggerRecord && !genExcel)
                {
                    // Delete trigger record
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_PICK_HEAD_DW_Delete(my_trigger_record);
                }

                Console.WriteLine(trigger_record.KEYID + " : " + ProcessedSuccessfully);
            }
            private bool PutInAwaitingDbTable()
            {
                AwaitingPickHeadDW APO = new AwaitingPickHeadDW();
                APO.CalledFrom = GS.selfName;
                APO.ChangedTime = DateTime.Now;
                if (CurrentAwaitingPickHeadDW.Count() == 1)
                {
                    CurrentAwaitingPickHeadDW[0].LastUpdatedDate = APO.ChangedTime;
                    APO.dbRow_instance = CurrentAwaitingPickHeadDW[0];
                    return CAPI.SaveToAPI(APO, "/api/AwaitingPickHeadDWSave");
                }
                else
                {
                    AwaitingPickHeadDW.dbRow AwtPO = new AwaitingPickHeadDW.dbRow();
                    AwtPO.ID = 0;
                    AwtPO.InsertedDate = APO.ChangedTime;
                    AwtPO.LastUpdatedDate = null;
                    AwtPO.KEYID = trigger_record.KEYID;
                    APO.dbRow_instance = AwtPO;

                    return CAPI.SaveToAPI(APO, "/api/AwaitingPickHeadDWSave");
                }
            }
            List<AwaitingPickHeadDW.dbRow> CurrentAwaitingPickHeadDW;
            private bool ReadAwaitingRowsFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.AwaitingPickHeadDW.Search
                {
                    KEYID = trigger_record.KEYID,
                    SearchMode = wbm_common.DataObjects.AwaitingPickHeadDW.Search.SearchModeType.KeyID
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "AwaitingPickHeadDWSearch");
                if (response == null)
                {
                    logger.Error("order ReadAwaitingRowsFromAPI KEYID returned error: " + trigger_record.KEYID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentAwaitingPickHeadDW = JsonConvert.DeserializeObject<List<AwaitingPickHeadDW.dbRow>>(response);
                        if (CurrentAwaitingPickHeadDW.Count() > 1)
                        {
                            logger.Error("order ReadAwaitingRowsFromAPI should only have one row in the database, returned "
                                + CurrentAwaitingPickHeadDW.Count().ToString() + " rows");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("order ReadAwaitingRowsFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }
            List<AwaitingPickHeadDW.dbRow> AllAwaitingPickHeadDW;
            private bool ReadAllAwaitingRowsFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.AwaitingPickHeadDW.Search
                {
                    SearchMode = wbm_common.DataObjects.AwaitingPickHeadDW.Search.SearchModeType.Undefined
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "AwaitingPickHeadDWSearch");
                if (response == null)
                {
                    logger.Error("order ReadAllAwaitingRowsFromAPI returned error");
                    return false;
                }
                else
                {
                    try
                    {
                        AllAwaitingPickHeadDW = JsonConvert.DeserializeObject<List<AwaitingPickHeadDW.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("order ReadAllAwaitingRowsFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }
            private async Task<bool> ProcessRecord()
            {
                // We've read the PICK.HEAD record; need to create order data holder and read in other required data
                CurrentOdh = new OrderDataHolder(CurrentCompanyID, CurrentSiteID, CurrentOwnerID);
                CurrentOdh.addDebug = genExcel;
                CurrentOdh.PickHeadDW = pickhead_dw.Value[0];

                if (await GatherData())
                {
                    // Check and see if we have a customer address record and if not, create one
                    if (CurrentOdh.CustomerAddress == null && CurrentCustomerAddressCustomerNumber != null)
                    {
                        // We won't use the new address in order processing; this is being created so that staff can then populate
                        // with any processing flags needed, and will be used for future orders.
                        CreateNewCustomerAddress();
                    }

                    return CurrentOdh.ProcessOrder();

                }
                else
                    return false;
            }

            private async Task<bool> GatherData()
            {
                Boolean rtnVal = true;

                //PW Data Warehouse tables
                if (!await List_PickDetail())
                    rtnVal = false;

                if (!await List_StockMove())
                    rtnVal = false;

                if (!await List_DbTrans())
                    rtnVal = false;

                if (!await List_Truck_Load())
                    rtnVal = false;

                // WBM Tables
                if (!await List_WBM_Stock())
                    rtnVal = false;

                if (LoadOwnerDataRequired)
                {
                    CurrentOwner_dbRow = wbm_api.GetOwner_dbRow(CurrentOwnerID);

                    if (!await List_ActionRateCode())
                        rtnVal = false;

                    if (!await List_ProcessingRule())
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

                if (!await Get_ZoneProcessing())
                    rtnVal = false;

                if (!await Get_CustomerAddress())
                    rtnVal = false;

                CurrentOdh.ListActionRateCode = ListActionRateCode;
                CurrentOdh.ListProcessingRule = ListProcessingRule;
                CurrentOdh.ListRateCollection = ListRateCollection;

                // Static data
                if (ListCustomerAddressDefault == null)
                    if (!await List_CustomerAddressDefault())
                        rtnVal = false;

                if (ListPriorityOrder == null)
                    if (!await List_PriorityOrder())
                        rtnVal = false;

                if (ListConnoteFee == null)
                    if (!await List_ConnoteFee())
                        rtnVal = false;

                if (ListCarrier == null)
                    if (!await List_Carrier())
                        rtnVal = false;

                CurrentOdh.ListCustomerAddressDefault = ListCustomerAddressDefault;
                CurrentOdh.ListPriorityOrder = ListPriorityOrder;
                CurrentOdh.ListConnoteFee = ListConnoteFee;
                CurrentOdh.ListCarrier = ListCarrier;

                return rtnVal;
            }

            Result<List<PICK_DETAIL_DW>> pickdetail_dw = null;
            private async Task<bool> List_PickDetail()
            {

                pickdetail_dw = await Paperless_DB.PICK_DETAIL_DW_ListByKeyIDStartsWith(trigger_record.KEYID);

                if (pickdetail_dw.IsSuccess && CurrentOdh != null)
                {
                    CurrentOdh.ListPickDetailDW = pickdetail_dw.Value;
                }

                return pickdetail_dw.IsSuccess;
            }

            Result<List<STOCK_MOVE_DW>> stockmove_dw = null;
            private async Task<bool> List_StockMove()
            {

                stockmove_dw = await Paperless_DB.STOCK_MOVE_DW_ListByInvNoOrOrdNo(pickhead_dw.Value[0].PHEAD_ID + "#");

                if (stockmove_dw.IsSuccess && CurrentOdh != null)
                {
                    CurrentOdh.ListStockMoveDW = stockmove_dw.Value;
                }

                return stockmove_dw.IsSuccess;
            }

            Result<List<DB_TRANS>> dbTrans = null;
            private async Task<bool> List_DbTrans()
            {

                dbTrans = await Paperless_DB.DB_TRANS_ListByReference(pickhead_dw.Value[0].PHEAD_ID);

                if (dbTrans.IsSuccess && CurrentOdh != null)
                {
                    CurrentOdh.ListDBTrans = dbTrans.Value;
                }

                return dbTrans.IsSuccess;
            }

            private async Task<bool> List_Truck_Load()
            {
                Result<List<TRUCK_LOAD_DW>> truck_load_dw = null;

                truck_load_dw = await Paperless_DB.TRUCK_LOAD_DW_ListByLoad_No(CurrentOdh.PickHeadDW.LOAD_NO);

                if (truck_load_dw.IsSuccess)
                {
                    CurrentOdh.ListTruckLoadDW = truck_load_dw.Value;
                }

                return truck_load_dw.IsSuccess;
            }

            string response;

            private async Task<bool> List_WBM_Stock()
            {
                Stock.Search so = new Stock.Search();
                so.SearchMode = Stock.Search.SearchModeType.Owner_ListProductCode;
                so.ListProductCode = CurrentOdh.ListSKU;
                so.ListOwnerID = new List<int>() { CurrentOwnerID };
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockSearch");
                if (response == null)
                {
                    logger.Error("Order StockSearch returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        List<Stock.dbRow> stockList = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);

                        CurrentOdh.ListStock = stockList;

                        // Check that we have all required stock records
                        if (CurrentOdh.ListSKU.Count() != CurrentOdh.ListSKU.Count())
                        {
                            var missingSKUs = CurrentOdh.ListSKU.Except(CurrentOdh.ListStock.Select(x => x.ProductCode)).ToList();
                            logger.Error("Order StockSearch missing SKUs: " + string.Join(", ", missingSKUs));
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Order List_WBM_Stock " + ex);
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
                    logger.Error("Order ActionRateCode returned no response");
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

            private async Task<bool> List_CustomerAddressDefault()
            {
                CustomerAddressDefault.Search so = new CustomerAddressDefault.Search();
                so.SearchMode = CustomerAddressDefault.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "CustomerAddressDefaultSearch");
                if (response == null)
                {
                    logger.Error("Order CustomerAddressDefault returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListCustomerAddressDefault = JsonConvert.DeserializeObject<List<CustomerAddressDefault.dbRow>>(response);

                        ListCustomerAddressDefault = ListCustomerAddressDefault.OrderBy(cad => cad.OwnerID).ThenBy(cad => cad.SiteID).ToList();

                    }
                    catch (Exception ex)
                    {
                        logger.Error("Order ListCustomerAddressDefault " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_PriorityOrder()
            {
                PriorityOrder.Search so = new PriorityOrder.Search();
                so.SearchMode = PriorityOrder.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "PriorityOrderSearch");
                if (response == null)
                {
                    logger.Error("Order PriorityOrder returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListPriorityOrder = JsonConvert.DeserializeObject<List<PriorityOrder.dbRow>>(response);
                        ListPriorityOrder = ListPriorityOrder.OrderBy(po => po.OwnerID).ThenBy(po => po.SiteID).ToList();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Order ListPriorityOrder " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> List_ConnoteFee()
            {
                ConnoteFee.Search so = new ConnoteFee.Search();
                so.SearchMode = ConnoteFee.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ConnoteFeeSearch");
                if (response == null)
                {
                    logger.Error("Order ConnoteFee returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListConnoteFee = JsonConvert.DeserializeObject<List<ConnoteFee.dbRow>>(response);

                        ListConnoteFee = ListConnoteFee.OrderBy(cf => cf.OwnerID).ThenBy(cf => cf.SiteID).ThenBy(cf => cf.CustomerAddressID).ThenBy(cf => cf.CarrierID).ToList();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Order ListPriorityOrder " + ex);
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

            private async Task<bool> HasNoTransactionXref()
            {
                PaperlessTransactionXref.dbRow PaperlessTransactionXrefRow = null;
                PaperlessTransactionXref.Search so = new PaperlessTransactionXref.Search();
                so.SearchMode = PaperlessTransactionXref.Search.SearchModeType.PaperlessKeyID;
                so.PaperlessKeyID = trigger_record.KEYID;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "PaperlessTransactionXrefSearch");
                if (response == null)
                {
                    logger.Error("Order PaperlessTransactionXref returned no response");
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
                        logger.Error("Order Get_PaperlessTransactionXref " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> Get_ZoneProcessing()
            {
                ZoneProcessing.dbRow ZoneProcessingRow = null;
                string zkey = CurrentSiteID + "-" + CurrentOdh.PickHeadDW.OUT_ZONE;
                if (DictZoneProcessing.TryGetValue(zkey, out ZoneProcessingRow))
                {
                    CurrentOdh.ZoneProcessing = ZoneProcessingRow;
                    return true;
                }
                else
                {
                    ZoneProcessing.Search so = new ZoneProcessing.Search();
                    so.SearchMode = ZoneProcessing.Search.SearchModeType.SiteZoneNumber;
                    so.ZoneNumber = CurrentOdh.PickHeadDW.OUT_ZONE;
                    so.ListSiteID = new List<int>() { CurrentSiteID };

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "ZoneProcessingSearch");
                    if (response == null)
                    {
                        logger.Error("Order ZoneProcessing returned no response");
                        return false;
                    }
                    else
                    {
                        try
                        {
                            List<ZoneProcessing.dbRow> ListZoneProcessing = JsonConvert.DeserializeObject<List<ZoneProcessing.dbRow>>(response);

                            if (ListZoneProcessing != null && ListZoneProcessing.Count > 0)
                            {
                                CurrentOdh.ZoneProcessing = ListZoneProcessing[0];
                                DictZoneProcessing.Add(zkey, ListZoneProcessing[0]);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Order Get_ZoneProcessing " + ex);
                            return false;
                        }
                    }
                    return true;
                }
            }

            private async Task<bool> Get_CustomerAddress()
            {
                // Check if we have a valid non null customernumber. Only trim if not null.

                CurrentCustomerAddressCustomerNumber = CurrentOdh.PickHeadDW.CUST_NO;
                if (CurrentCustomerAddressCustomerNumber != null)
                {
                    CurrentCustomerAddressCustomerNumber = CurrentCustomerAddressCustomerNumber.Trim();
                }

                if (string.IsNullOrEmpty(CurrentCustomerAddressCustomerNumber))
                {
                    CurrentCustomerAddressCustomerNumber = null;
                    return true;
                }
                else
                {
                    CustomerAddress.Search so = new CustomerAddress.Search();
                    so.SearchMode = CustomerAddress.Search.SearchModeType.OwnerCustomerID;
                    so.ListOwnerID = new List<int>() { CurrentOwnerID };
                    so.CustomerID = CurrentOdh.PickHeadDW.CUST_NO;

                    string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                    response = CAPI.FetchFromAPIWithKeyID(jsonString, "CustomerAddressSearch");
                    if (response == null)
                    {
                        logger.Error("Order CustomerAddress returned sno response");
                        return false;
                    }
                    else
                    {
                        try
                        {
                            List<CustomerAddress.dbRow> ListCustomerAddress = JsonConvert.DeserializeObject<List<CustomerAddress.dbRow>>(response);

                            if (ListCustomerAddress != null && ListCustomerAddress.Count > 0)
                                CurrentOdh.CustomerAddress = ListCustomerAddress[0];
                        }
                        catch (Exception ex)
                        {
                            logger.Error("Order ListCustomerAddress " + ex);
                            return false;
                        }
                    }
                    return true;
                }
            }

            private bool CreateNewCustomerAddress()
            {
                // this is so it doesn't infinitely trigger
                CustomerAddress CurrentCustAdd = new CustomerAddress();
                CustomerAddress.dbRow dbRow = new CustomerAddress.dbRow();
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.OwnerID = CurrentOwnerID;
                dbRow.SiteID = CurrentSiteID;
                dbRow.Paperless_KeyID = trigger_record.KEYID.Split("_")[0] + "_" + CurrentOdh.PickHeadDW.CUST_NO ?? "";
                dbRow.PalletHireDelayDays = null;
                dbRow.CreatedMethod = "WBM_Or_NCA";
                dbRow.CreatedUserID = GS.selfUserID;
                dbRow.CreatedDateTime = now;
                dbRow.LastAmendedDateTime = now;
                dbRow.LastAmendedMethod = "WBM_Or_NCA";
                dbRow.LastAmendedUserID = GS.selfUserID;
                dbRow.ID = 0;
                dbRow.DeletedFlag = false;
                dbRow.CustomerID = CurrentOdh.PickHeadDW.CUST_NO ?? "";
                dbRow.Name = CurrentOdh.PickHeadDW.CUST_NAME ?? "";
                dbRow.AddressLine1 = CurrentOdh.PickHeadDW.CUST_ST1 ?? "";
                dbRow.AddressLine2 = CurrentOdh.PickHeadDW.CUST_ST2 ?? "";
                dbRow.AddressLine3 = CurrentOdh.PickHeadDW.CUST_ST3 ?? "";
                dbRow.AddressLine4 = "";
                dbRow.Suburb = CurrentOdh.PickHeadDW.CUST_SUB ?? "";
                dbRow.State = CurrentOdh.PickHeadDW.STATE_CODE ?? "";
                dbRow.PostCode = CurrentOdh.PickHeadDW.CUST_PCODE ?? "";
                dbRow.Connote_RateCollectionRateChargeCode = "";
                CurrentCustAdd.dbRow_instance = dbRow;

                ToDo toDo = new ToDo();
                toDo.CalledFrom = GS.selfName;
                toDo.ChangedTime = now;
                ToDo.dbRow tododbrow = new ToDo.dbRow();
                tododbrow.DeletedFlag = false;
                tododbrow.CategoryID = 2;
                tododbrow.ChangeHeaderTableID = 23;
                tododbrow.CompanyID = CurrentCompanyID;
                tododbrow.CreatedDateTime = now;
                tododbrow.CreatedMethod = "WBM_Or_NCA";
                tododbrow.CreatedUserID = GS.selfUserID;
                tododbrow.Description = "New Customer " + (CurrentOdh.PickHeadDW.CUST_NAME ?? "") + " for " + Owner;
                tododbrow.EventDateTime = now;
                tododbrow.LastAmendedDateTime = now;
                tododbrow.LastAmendedMethod = "WBM_Or_NCA";// all these rows have limited characters so truncate where you can
                tododbrow.LastAmendedUserID = GS.selfUserID;
                tododbrow.Note = "Created From details on an Order, please check fields";
                tododbrow.OwnerID = CurrentOwnerID;
                tododbrow.SiteID = CurrentSiteID;
                tododbrow.SourceID = 0;
                tododbrow.StatusID = 0;
                toDo.dbRow_instance = tododbrow;
                CurrentCustAdd.ToDo = toDo;

                CurrentCustAdd.CalledFrom = GS.selfName;
                CurrentCustAdd.ChangedTime = now;

                return CAPI.SaveToAPI(CurrentCustAdd, "/api/CustomerAddressSave");
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
                        logger.Error("Order ProcessingRuleSearch returned no response");
                        return false;
                    }
                    else
                    {
                        try
                        {
                            ListProcessingRule = JsonConvert.DeserializeObject<List<ProcessingRule.dbRow>>(response);

                            // Only want paperless order process
                            ListProcessingRule = ListProcessingRule.Where(pr => pr.ProcessCategoryID == 3).OrderBy(pr => pr.SortOrder).ToList();

                        }
                        catch (Exception ex)
                        {
                            logger.Error("Order ListPriorityOrder " + ex);
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            }

            private async Task<bool> List_Carrier()
            {
                Carrier.Search so = new Carrier.Search();
                so.SearchMode = Carrier.Search.SearchModeType.AllRecords;

                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(so);

                response = CAPI.FetchFromAPIWithKeyID(jsonString, "CarrierSearch");
                if (response == null)
                {
                    logger.Error("Order Carrier returned no response");
                    return false;
                }
                else
                {
                    try
                    {
                        ListCarrier = JsonConvert.DeserializeObject<List<Carrier.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Order ListCarrier" + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<bool> Cost_Order()
            {
                Transaction_Costing tc = new Transaction_Costing();
                tc.thRow = CurrentOdh.TransactionHeader;
                tc.ListTransactionDetail_Costing = new List<Transaction_Costing.TransactionDetail_Costing>();

                Transaction_Costing.TransactionDetail_Costing tdc;
                foreach (TransactionDetail.dbRow td in CurrentOdh.ListTransactionDetail)
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
                    logger.Error("TransactionCostingCost returned no response");
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
                        logger.Error("TransactionCostingCost deserialise " + ex);
                        return false;
                    }

                    // only here if successful deserialise
                    if (returntch.CostedOK == true)
                    {
                        if (returntch.ListTransactionCosting[0].CostedOK == false)
                        {
                            logger.Error("TransactionCostingCost returned CostedOK = false");
                            return false;
                        }
                        else
                        {
                            ProcessReturnCosting(returntch.ListTransactionCosting[0]);
                        }
                    }
                    else
                    {
                        logger.Error("TransactionCostingCost returned no costing records");
                        return false;
                    }
                }
                return true;
            }

            private void ProcessReturnCosting(wbm_common.NonDatabaseObjects.Transaction_Costing tc)
            {
                CurrentOdh.TransactionHeader = tc.thRow;
                CurrentOdh.ListTransactionDetail.Clear();
                foreach (Transaction_Costing.TransactionDetail_Costing tdc in tc.ListTransactionDetail_Costing)
                {
                    TransactionDetail.dbRow td = tdc.tdRow;
                    CurrentOdh.ListTransactionDetail.Add(td);
                }
            }

            private bool SaveFunction()
            {
                ListTrans.Add(new WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans(CurrentOdh.TransactionHeader, CurrentOdh.ListTransactionDetail));
                OrderCount++;

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

            private async Task<bool> ReadRecord()
            {
                pickhead_dw = await ReadPickHeadFromDB();
                if (pickhead_dw.IsSuccess && pickhead_dw.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                if (!await List_PickDetail())
                    return false;

                if (!await List_StockMove())
                    return false;

                if (!await List_DbTrans())
                    return false;

                return pickhead_dw.IsSuccess && pickhead_dw.Value.Count() > 0;
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

            private async Task<Result<List<PICK_HEAD_DW>>> ReadPickHeadFromDB()
            {
                return await Paperless_DB.PICK_HEAD_DW_ListByKeyID(trigger_record.KEYID);
            }

            private Boolean IsOrderReadyToProcess()
            {
                // check if order is in a status that should be processed, or if we need to wait for more updates to come in before processing
                if (pickhead_dw.Value[0].PHEAD_STATUS == "10")
                    return true;
                else
                    return false;
            }

            private Boolean CanProcess()
            {
                // First part of KEYID is the site; need to see if the site is enabled before proceeding further
                String[] keyid_parts = trigger_record.KEYID.Split('_');
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
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner + ", " + Site, EmailHelper.IssueType.Multiple, "Order");
                            else if (SiteFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Site, EmailHelper.IssueType.Site, "Order");
                            else if (OwnerFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner, EmailHelper.IssueType.Owner, "Order");
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
                    EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Format, "Order");

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

                excelReport.ListTrans = ListTrans;
                excelReport.ExcelValues = ValuesFromExcel;
                excelReport.ValuesThatDidntComplete = OrdersThatDidntComplete;

                await excelReport.GenerateExcel();

                WBM_BackgroundProcessor.Helpers.general.debugWriteFile(excelReport.ms, "OrderExcel");
            }
        }
    }
}
