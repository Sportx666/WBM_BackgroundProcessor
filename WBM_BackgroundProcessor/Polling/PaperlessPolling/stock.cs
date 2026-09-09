using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class stock
    {
        public class Poll
        {
            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            TRIGGER_STOCK_DW trigger_record;

            Boolean CanDeleteTriggerRecord = false;
            Boolean PreviousCanDeleteTriggerRecord = false;
            Boolean PreviousCanProcessTriggerRecord = false;

            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            int CurrentOwnerID = 0;
            string Site = "";
            string PreviousSiteName = "";
            string Owner = "";
            string PreviousOwner = "";
            string response = "";
            List<Stock.dbRow> CurrentStockRow = null;
            Stock CurrentStock = null;
            Stock.dbRow Existing_dbRow = null;
            List<string> ProductsToSkip = new List<string> { "HZLM_139", "HZLM_497", "HZLM_138", "HZLM_G100" };


            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_STOCK_DW>> rTRIGGER_STOCK_DW;

                rTRIGGER_STOCK_DW = await Paperless_DB.TRIGGER_STOCK_DW_List();

                if (rTRIGGER_STOCK_DW.IsSuccess)
                {
                    foreach (TRIGGER_STOCK_DW my_trigger_record in rTRIGGER_STOCK_DW.Value)
                    {
                        await ProcessTCDW(my_trigger_record);
                    }
                }
            }

            DateTime now = DateTime.MinValue;
            Result<List<STOCK_DW>> STOCK_DW = null;
            private async Task ProcessTCDW(TRIGGER_STOCK_DW my_trigger_record)
            {
                CanDeleteTriggerRecord = false;
                bool CanProcessTriggerRecord = true;
                now = DateTime.Now;

                trigger_record = my_trigger_record;

                if (CanProcess())
                {
                    if (await ReadRecord())
                    {
                        if (DoTranslations())
                        {
                            if (ReadFromAPI())
                            {
                                if (ProcessRecord())
                                {
                                    SaveFunction();
                                }
                            }
                        }
                    }
                }
                else
                    CanProcessTriggerRecord = false;

                if (CanDeleteTriggerRecord)
                {
                    // Delete trigger record
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_STOCK_DW_Delete(my_trigger_record);
                }
                Console.WriteLine(trigger_record.KEYID + " : " + CanProcessTriggerRecord + " - " + CanDeleteTriggerRecord);
            }

            private bool ProcessRecord()
            {
                // Are we an existing or new record ?

                CurrentStock = new Stock();

                if (CurrentStockRow != null && CurrentStockRow.Count() > 0)
                {
                    CurrentStock.dbRow_instance = CurrentStockRow[0];

                    //Need to make a copy here... for comparison later - Newtonsoft serialise de-serialise or other ?
                    Existing_dbRow = JsonConvert.DeserializeObject<Stock.dbRow>(JsonConvert.SerializeObject(CurrentStock.dbRow_instance));
                }
                else
                {
                    // Construct new db_row instance
                    //Make a call here to a function that does the work. Only needs to set bare minimum fields. Maybe not even created as API will do that ?
                    CurrentStock.dbRow_instance = makeNewStock();
                    CurrentStock.ToDo = makeToDo();
                }
                copyData();

                return true;
            }

            private Stock.dbRow makeNewStock()
            {
                Stock.dbRow dbRow = new Stock.dbRow();
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.OwnerID = CurrentOwnerID;
                dbRow.SiteID = CurrentSiteID;
                dbRow.Paperless_KeyID = trigger_record.KEYID;
                dbRow.PalletStorageTypeID = 0;
                dbRow.StockRateCategoryID = 0;
                dbRow.CategoryID = 0;
                dbRow.CreatedMethod = "WBM_CA_MCA";
                dbRow.CreatedUserID = GS.selfUserID;
                dbRow.CreatedDateTime = now;
                dbRow.LastAmendedDateTime = now;
                dbRow.LastAmendedMethod = "WBM_CA_MCA";
                dbRow.LastAmendedUserID = GS.selfUserID;
                dbRow.ID = 0;
                dbRow.DeletedFlag = false;
                return dbRow;
            }

            private bool SaveFunction()
            {
                try
                {
                    if (CurrentStock.dbRow_instance.ID == 0)
                    {
                        SaveAPIFunctionCall();
                    }
                    else if (isStockDifferent())
                    {
                        // Only update if something has changed to avoid unnecessary API calls
                        SaveAPIFunctionCall();
                    }
                    else
                    {
                        // No changes, so can delete trigger record but don't need to call API
                        CanDeleteTriggerRecord = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Stock SaveFunction " + ex);
                    return false;
                }

                return true;
            }

            private void copyData()
            {
                CurrentStock.dbRow_instance.CompanyID = CurrentCompanyID;
                CurrentStock.dbRow_instance.OwnerID = CurrentOwnerID;
                CurrentStock.dbRow_instance.SiteID = CurrentSiteID;
                CurrentStock.dbRow_instance.ProductCode = STOCK_DW.Value[0].PROD_NO ?? "";
                CurrentStock.dbRow_instance.Description = STOCK_DW.Value[0].PROD_DESC ?? "";
                CurrentStock.dbRow_instance.Length = STOCK_DW.Value[0].PROD_DEPTH ?? 0;
                CurrentStock.dbRow_instance.Width = STOCK_DW.Value[0].PROD_WIDTH ?? 0;
                CurrentStock.dbRow_instance.Height = STOCK_DW.Value[0].PROD_HEIGHT ?? 0;
                CurrentStock.dbRow_instance.Weight = STOCK_DW.Value[0].PROD_WEIGHT ?? 0;
                CurrentStock.dbRow_instance.ItemsPerPallet = (int)(STOCK_DW.Value[0].PAL_QTY ?? 0);
            }

            private bool DoTranslations()
            {
                // No trans
                return true;
            }

            private async Task<bool> ReadRecord()
            {
                STOCK_DW = await ReadStockFromDB();
                if (STOCK_DW.IsSuccess && STOCK_DW.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                return STOCK_DW.IsSuccess && STOCK_DW.Value.Count() > 0;
            }

            private void SaveAPIFunctionCall()
            {
                CurrentStock.CalledFrom = GS.selfName;
                CurrentStock.ChangedTime = now;

                if (CAPI.SaveToAPI(CurrentStock, "/api/StockSave"))
                    CanDeleteTriggerRecord = true;
                else
                    CanDeleteTriggerRecord = false; // error 
            }

            private bool isStockDifferent()
            {
                Stock.dbRow dbRow = CurrentStock.dbRow_instance;

                return dbRow.CompanyID != Existing_dbRow.CompanyID
                    || dbRow.OwnerID != Existing_dbRow.OwnerID
                    || dbRow.SiteID != Existing_dbRow.SiteID
                    || dbRow.ProductCode != Existing_dbRow.ProductCode
                    || dbRow.Description != Existing_dbRow.Description
                    || dbRow.Length != Existing_dbRow.Length
                    || dbRow.Width != Existing_dbRow.Width
                    || dbRow.Height != Existing_dbRow.Height
                    || dbRow.Weight != Existing_dbRow.Weight
                    || dbRow.ItemsPerPallet != Existing_dbRow.ItemsPerPallet;
            }

            private bool ReadFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.Stock.Search
                {
                    Paperless_KeyID = trigger_record.KEYID,
                    SearchMode = wbm_common.DataObjects.Stock.Search.SearchModeType.Paperless_KeyID
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "StockSearch");
                if (response == null)
                {
                    logger.Error("Stock ReadFromAPI KEYID returned no Stock: " + trigger_record.KEYID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentStockRow = JsonConvert.DeserializeObject<List<Stock.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Stock ReadFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<Result<List<STOCK_DW>>> ReadStockFromDB()
            {
                return await Paperless_DB.STOCK_DW_ListByKeyID(trigger_record.KEYID);
            }

            private ToDo makeToDo()
            {
                ToDo toDo = new ToDo();
                toDo.CalledFrom = GS.selfName;
                toDo.ChangedTime = now;
                ToDo.dbRow tododbrow = new ToDo.dbRow();
                tododbrow.DeletedFlag = false;
                tododbrow.CategoryID = 4;
                tododbrow.ChangeHeaderTableID = 55;
                tododbrow.CompanyID = CurrentCompanyID;
                tododbrow.CreatedDateTime = now;
                tododbrow.CreatedMethod = "WBM_St_MToDo";
                tododbrow.CreatedUserID = GS.selfUserID;
                tododbrow.Description = "New Stock " + (STOCK_DW.Value[0].PROD_DESC ?? "") + " for " + Owner;
                tododbrow.EventDateTime = now;
                tododbrow.LastAmendedDateTime = now;
                tododbrow.LastAmendedMethod = "WBM_St_MToDo";// all these rows have limited characters so truncate where you can
                tododbrow.LastAmendedUserID = GS.selfUserID;
                tododbrow.Note = "Please check Pallet Storage Type, Stock Rate Category and Category";
                tododbrow.OwnerID = CurrentOwnerID;
                tododbrow.SiteID = CurrentSiteID;
                tododbrow.SourceID = 0;
                tododbrow.StatusID = 0;
                toDo.dbRow_instance = tododbrow;
                return toDo;
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
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner + ", " + Site, EmailHelper.IssueType.Multiple, "Stock");
                            else if (SiteFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Site, EmailHelper.IssueType.Site, "Stock");
                            else if (OwnerFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner, EmailHelper.IssueType.Owner, "Stock");
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
                    else if (!ProductsToSkip.Contains(trigger_record.KEYID))
                        EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Owner, "Stock");
                }
                else
                    EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Format, "Stock");

                return false;
            }

            private wbm_api.FieldState previousSiteFS = wbm_api.FieldState.NotFound;
            private wbm_api.FieldState previousOwnerFS = wbm_api.FieldState.NotFound;

            private wbm_api.FieldState FindOwnerFS()
            {
                PreviousOwner = Owner;
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
        }
    }
}
