using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models;
using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;
using static WBM_BackgroundProcessor.Models.Excel.ProductQtyExcelReport;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class receiptIntegrityCheck
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
            int CurrentOwnerID = 0;
            string Site = "";
            string PreviousSiteName = "";
            string Owner = "";
            string PreviousOwner = "";
            string CurrentReceiptNumber = "";

            List<ZoneProcessing.dbRow> ListZoneProcessing = null;

            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public int ReceiptCount = 0;
            public List<ReceiptAndListsObject> ReceiptAndLists = new List<ReceiptAndListsObject>();

            public async Task PollTriggerTable()
            {
                Result<List<PURCH_ORD_PROD_DW>> rtrigger_purchOrd_dw;

                DateTime FromDate = new DateTime(2026, 07, 06);
                DateTime ToDate = new DateTime(2026, 07, 23);

                rtrigger_purchOrd_dw = await Paperless_DB.PURCH_ORD_PROD_DW_ListLoadWithUFlagBetweenDates(FromDate, ToDate);

                if (rtrigger_purchOrd_dw.IsSuccess)
                {
                    // because the new rows enter the database split up like
                    // BLA_06400PO-044290_1, BLA_06400PO-044290_2_1, BLA_06400PO-044290_2_2, BLA_06400PO-044290_3_1 
                    // we collect these together under BLA_06400PO-044290 and process all the parts we have. So we pass around lists of Purch Ord Prods.
                    List<string> PublicIDsUncut = rtrigger_purchOrd_dw.Value
                        .Where(x => x.KEYID.Contains("_"))
                        .Select(x => x.KEYID.Split('_')[0] + "_" + x.KEYID.Split('_')[1]).Distinct().ToList();

                    foreach (string UnCutIDSub in PublicIDsUncut)
                    {
                        await ProcessTPODW(rtrigger_purchOrd_dw.Value.Where(x => x.KEYID.StartsWith(UnCutIDSub)).ToList(), UnCutIDSub);
                    }
                    if (ReceiptCount > 0)
                        ProduceExcel();
                }
            }

            Result<List<PURCH_ORD_PROD_DW>> purchOrd_dw = null;
            private async Task ProcessTPODW(List<PURCH_ORD_PROD_DW> my_trigger_records, string UnCutIDSub)
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
                    //Result<int> deleted_trigger = await Paperless_DB.TRIGGER_PURCH_ORD_PROD_DW_DeleteList(my_trigger_records);
                }

                Console.WriteLine(UnCutID + " : " + ProcessedSuccessfully);
            }
            private async Task<bool> ProcessRecord()
            {
                if (await GatherData())
                    return true;
                else
                    return false;
            }

            private async Task<bool> GatherData()
            {
                Boolean rtnVal = true;

                if (LoadOwnerDataRequired)
                {
                    if (!await List_ZoneProcessing())
                        rtnVal = false;

                    if (rtnVal)
                        LoadOwnerDataRequired = false;
                }

                ReceiptAndLists.Add(new ReceiptAndListsObject
                {
                    purchs = purchOrd_dw.Value,
                    stockmoves = stockmove_dw.Value,
                    zones = ListZoneProcessing.Where(x => x.IsPickFace || x.IsReceiving).ToList(),
                    pallets = pallet_dw.Value,
                });

                return rtnVal;
            }

            Result<List<STOCK_MOVE_DW>> stockmove_dw = null;
            private async Task<bool> List_StockMove()
            {
                stockmove_dw = await Paperless_DB.STOCK_MOVE_DW_ListByInvNoOrOrdNo(CurrentReceiptNumber);
                return stockmove_dw.IsSuccess;
            }

            Result<List<PALLET_DW>> pallet_dw = null;
            private async Task<bool> List_Pallet()
            {
                pallet_dw = await Paperless_DB.PALLET_DW_ListBySuppOrdNo(CurrentReceiptNumber);
                return pallet_dw.IsSuccess;
            }
            string response;

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

            private bool SaveFunction()
            {
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
                if (!await List_StockMove())
                    return false;

                if (!await List_Pallet())
                    return false;

                return purchOrd_dw.IsSuccess && purchOrd_dw.Value.Count() > 0;
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
                            //if (SiteFS == wbm_api.FieldState.NotFound && OwnerFS == wbm_api.FieldState.NotFound)
                            //    EmailHelper.SendEmail(UnCutID, Owner + ", " + Site, EmailHelper.IssueType.Multiple, "Receipt");
                            //else if (SiteFS == wbm_api.FieldState.NotFound)
                            //    EmailHelper.SendEmail(UnCutID, Site, EmailHelper.IssueType.Site, "Receipt");
                            //else if (OwnerFS == wbm_api.FieldState.NotFound)
                            //    EmailHelper.SendEmail(UnCutID, Owner, EmailHelper.IssueType.Owner, "Receipt");
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
                //else
                //    EmailHelper.SendEmail(UnCutID, "", EmailHelper.IssueType.Format, "Receipt");

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
                if (CurrentSiteID != 0 && !wbm_api.IsSiteActive(CurrentSiteID))
                    return wbm_api.FieldState.Inactive;
                return wbm_api.FieldState.Active;
            }

            public async Task ProduceExcel()
            {
                WBM_BackgroundProcessor.Models.Excel.ProductQtyExcelReport excelReport = new Models.Excel.ProductQtyExcelReport(ReceiptAndLists);

                await excelReport.GenerateExcel();

                WBM_BackgroundProcessor.Helpers.general.debugWriteFile(excelReport.ms, "ReceiptCheckExcel");
            }
        }
    }
}
