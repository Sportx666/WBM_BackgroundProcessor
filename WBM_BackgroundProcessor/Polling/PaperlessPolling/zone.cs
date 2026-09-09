using Azure;
using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Helpers;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.DataHolders;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;
using static WBM_BackgroundProcessor.Models.Excel.ProductQtyExcelReport;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class zone
    {
        public class Poll
        {
            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            TRIGGER_ZONE_DW trigger_record;

            Boolean CanDeleteTriggerRecord = false;
            Boolean PreviousCanDeleteTriggerRecord = false;
            Boolean PreviousCanProcessTriggerRecord = false;

            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            string Site = "";
            string PreviousSiteName = "";
            ZoneProcessing CurrentZone = null;
            ZoneProcessing.dbRow Existing_dbRow = null;

            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_ZONE_DW>> rTRIGGER_ZONE_DW;

                rTRIGGER_ZONE_DW = await Paperless_DB.TRIGGER_ZONE_DW_List();

                if (rTRIGGER_ZONE_DW.IsSuccess)
                {
                    foreach (TRIGGER_ZONE_DW my_trigger_record in rTRIGGER_ZONE_DW.Value)
                    {
                        await ProcessTCDW(my_trigger_record);
                    }
                }
            }

            DateTime now = DateTime.MinValue;
            Result<List<ZONE_DW>> ZONE_DW = null;
            private async Task ProcessTCDW(TRIGGER_ZONE_DW my_trigger_record)
            {
                CanDeleteTriggerRecord = true;
                bool CanProcessTriggerRecord = true;
                now = DateTime.Now;

                trigger_record = my_trigger_record;

                if (await ReadRecord())
                {
                    if (ReadFromAPI())
                    {
                        if (ProcessRecord())
                        {
                            SaveFunction();
                        }
                    }
                }

                if (CanDeleteTriggerRecord)
                {
                    // Delete trigger record
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_ZONE_DW_Delete(my_trigger_record);
                }
                Console.WriteLine(trigger_record.KEYID + " : " + CanProcessTriggerRecord + " - " + CanDeleteTriggerRecord);
            }
            private bool ProcessRecord()
            {
                // Are we an existing or new record ?

                CurrentZone = new ZoneProcessing();

                if (CurrentZoneProcessings != null && CurrentZoneProcessings.Count() > 0)
                {
                    CurrentZone.dbRow_instance = CurrentZoneProcessings[0];

                    //Need to make a copy here... for comparison later - Newtonsoft serialise de-serialise or other ?
                    Existing_dbRow = JsonConvert.DeserializeObject<ZoneProcessing.dbRow>(JsonConvert.SerializeObject(CurrentZone.dbRow_instance));
                }
                else
                {
                    // Construct new db_row instance
                    //Make a call here to a function that does the work. Only needs to set bare minimum fields. Maybe not even created as API will do that ?
                    CurrentZone.dbRow_instance = makeNewZoneProcessing();
                    CurrentZone.ToDo = makeToDo();
                }
                copyData();

                return true;
            }
            private ZoneProcessing.dbRow makeNewZoneProcessing()
            {
                ZoneProcessing.dbRow dbRow = new ZoneProcessing.dbRow();
                dbRow.ZoneNumber = ZONE_DW.Value[0].ZONE ?? "";
                dbRow.SiteID = CurrentSiteID;
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.Description = ZONE_DW.Value[0].ZONE_DESC ?? "";
                dbRow.IsCartonConsolidation = false;
                dbRow.IsPickFace = false;
                dbRow.IsReceiving = false;
                dbRow.IsReserve = false;
                dbRow.CreatedMethod = "WBM_Zon_MC";
                dbRow.CreatedUserID = GS.selfUserID;
                dbRow.CreatedDateTime = now;
                dbRow.LastAmendedDateTime = now;
                dbRow.LastAmendedMethod = "WBM_Zon_MC";
                dbRow.LastAmendedUserID = GS.selfUserID;
                dbRow.ID = 0;
                dbRow.DeletedFlag = false;
                return dbRow;
            }


            private bool SaveFunction()
            {
                try
                {
                    if (CurrentZone.dbRow_instance.ID == 0)
                    {
                        SaveAPIFunctionCall();
                    }
                    else if (isZoneProcessingDifferent())
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
                    logger.Error("ZoneProcessing SaveFunction " + ex);
                    return false;
                }

                return true;
            }
            private void copyData()
            {
                CurrentZone.dbRow_instance.ZoneNumber = ZONE_DW.Value[0].ZONE ?? "";
                CurrentZone.dbRow_instance.SiteID = CurrentSiteID;
            }

            private async Task<bool> ReadRecord()
            {
                ZONE_DW = await ReadZoneProcessingFromDB();
                if (ZONE_DW.IsSuccess && ZONE_DW.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                return ZONE_DW.IsSuccess && ZONE_DW.Value.Count() > 0;
            }

            private void SaveAPIFunctionCall()
            {
                CurrentZone.CalledFrom = GS.selfName;
                CurrentZone.ChangedTime = now;

                if (CAPI.SaveToAPI(CurrentZone, "/api/ZoneProcessingSave"))
                    CanDeleteTriggerRecord = true;
                else
                    CanDeleteTriggerRecord = false; // error 
            }

            private bool isZoneProcessingDifferent()
            {
                ZoneProcessing.dbRow dbRow = CurrentZone.dbRow_instance;
                return dbRow.IsReserve != Existing_dbRow.IsReserve
                    || dbRow.IsCartonConsolidation != Existing_dbRow.IsCartonConsolidation
                    || dbRow.IsReceiving != Existing_dbRow.IsReceiving
                    || dbRow.IsPickFace != Existing_dbRow.IsPickFace
                    || dbRow.Description != Existing_dbRow.Description;
            }

            string response = "";
            List<ZoneProcessing.dbRow> CurrentZoneProcessings = null;
            private bool ReadFromAPI()
            {
                (int, int) SiteIDCompanyID = wbm_api.GetSiteNumberCompanyID(ZONE_DW.Value[0].SITE_NO);
                if (SiteIDCompanyID.Item1 == 0 || SiteIDCompanyID.Item2 == 0)
                    return false;
                CurrentSiteID = SiteIDCompanyID.Item1;
                CurrentCompanyID = SiteIDCompanyID.Item2;
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.ZoneProcessing.Search
                {
                    SearchMode = wbm_common.DataObjects.ZoneProcessing.Search.SearchModeType.SiteZoneNumber,
                    ListSiteID = new List<int> { CurrentSiteID },
                    ZoneNumber = ZONE_DW.Value[0].ZONE
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "ZoneProcessingSearch");
                if (response == null)
                {
                    logger.Error("ZoneProcessing ReadFromAPI KEYID returned no ZoneProcessing: " + trigger_record.KEYID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentZoneProcessings = JsonConvert.DeserializeObject<List<ZoneProcessing.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("ZoneProcessing ReadFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<Result<List<ZONE_DW>>> ReadZoneProcessingFromDB()
            {
                return await Paperless_DB.ZONE_DW_ListByKeyID(trigger_record.KEYID);
            }

            private ToDo makeToDo()
            {
                ToDo toDo = new ToDo();
                toDo.CalledFrom = GS.selfName;
                toDo.ChangedTime = now;
                ToDo.dbRow tododbrow = new ToDo.dbRow();
                tododbrow.DeletedFlag = false;
                tododbrow.CategoryID = 1;
                tododbrow.ChangeHeaderTableID = 13;
                tododbrow.CompanyID = CurrentCompanyID;
                tododbrow.CreatedDateTime = now;
                tododbrow.CreatedMethod = "WBM_Zon_MToDo";
                tododbrow.CreatedUserID = GS.selfUserID;
                tododbrow.Description = "new ZoneProcessing " + (ZONE_DW.Value[0].ZONE ?? "") + ", please check IsX true or falses and set";
                tododbrow.EventDateTime = now;
                tododbrow.LastAmendedDateTime = now;
                tododbrow.LastAmendedMethod = "WBM_Zon_MToDo";// all these rows have limited characters so truncate where you can
                tododbrow.LastAmendedUserID = GS.selfUserID;
                tododbrow.Note = "new ZoneProcessing";
                tododbrow.OwnerID = 0;
                tododbrow.SiteID = CurrentSiteID;
                tododbrow.SourceID = 0;
                tododbrow.StatusID = 0;
                toDo.dbRow_instance = tododbrow;
                return toDo;
            }
        }
    }
}
