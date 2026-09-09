using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class location
    {
        public class Poll
        {
            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            TRIGGER_POSITION_DW trigger_record;

            Boolean CanDeleteTriggerRecord = false;
            Boolean PreviousCanDeleteTriggerRecord = false;
            Boolean PreviousCanProcessTriggerRecord = false;

            int CurrentSiteID = 0;
            int CurrentCompanyID = 0;
            string Site = "";
            string PreviousSiteName = "";
            Location CurrentLocAdd = null;
            Location.dbRow Existing_dbRow = null;

            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_POSITION_DW>> rTRIGGER_POSITION_DW;

                rTRIGGER_POSITION_DW = await Paperless_DB.TRIGGER_POSITION_DW_List();

                if (rTRIGGER_POSITION_DW.IsSuccess)
                {
                    foreach (TRIGGER_POSITION_DW my_trigger_record in rTRIGGER_POSITION_DW.Value)
                    {
                        await ProcessTCDW(my_trigger_record);
                    }
                }
            }

            DateTime now = DateTime.MinValue;
            Result<List<POSITION_DW>> positions_dw = null;
            private async Task ProcessTCDW(TRIGGER_POSITION_DW my_trigger_record)
            {
                CanDeleteTriggerRecord = true;
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
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_POSITION_DW_Delete(my_trigger_record);
                }
                Console.WriteLine(trigger_record.KEYID + " : " + CanProcessTriggerRecord + " - " + CanDeleteTriggerRecord);
            }
            private bool ProcessRecord()
            {
                // Are we an existing or new record ?

                CurrentLocAdd = new Location();

                if (CurrentLocations != null && CurrentLocations.Count() > 0)
                {
                    CurrentLocAdd.dbRow_instance = CurrentLocations[0];

                    //Need to make a copy here... for comparison later - Newtonsoft serialise de-serialise or other ?
                    Existing_dbRow = JsonConvert.DeserializeObject<Location.dbRow>(JsonConvert.SerializeObject(CurrentLocAdd.dbRow_instance));
                }
                else
                {
                    // Construct new db_row instance
                    //Make a call here to a function that does the work. Only needs to set bare minimum fields. Maybe not even created as API will do that ?
                    CurrentLocAdd.dbRow_instance = makeNewLocation();
                    CurrentLocAdd.ToDo = makeToDo();
                }
                copyData();

                return true;
            }
            private Location.dbRow makeNewLocation()
            {
                Location.dbRow dbRow = new Location.dbRow();
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.SiteID = CurrentSiteID;
                dbRow.PalletStorageTypeID = 0;
                dbRow.Paperless_KeyID = trigger_record.KEYID;
                dbRow.CreatedMethod = "WBM_Loc_MC";
                dbRow.CreatedUserID = GS.selfUserID;
                dbRow.CreatedDateTime = now;
                dbRow.LastAmendedDateTime = now;
                dbRow.LastAmendedMethod = "WBM_Loc_MC";
                dbRow.LastAmendedUserID = GS.selfUserID;
                dbRow.ID = 0;
                dbRow.DeletedFlag = false;
                return dbRow;
            }


            private bool SaveFunction()
            {
                try
                {
                    if (CurrentLocAdd.dbRow_instance.ID == 0)
                    {
                        SaveAPIFunctionCall();
                    }
                    else if (isLocationDifferent())
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
                    logger.Error("Location SaveFunction " + ex);
                    return false;
                }

                return true;
            }
            private void copyData()
            {
                CurrentLocAdd.dbRow_instance.CompanyID = CurrentCompanyID;
                CurrentLocAdd.dbRow_instance.SiteID = CurrentSiteID;
                CurrentLocAdd.dbRow_instance.PublicLocationID = positions_dw.Value[0].POSITION ?? "";
            }

            private bool DoTranslations()
            {
                // No trans
                return true;
            }

            private async Task<bool> ReadRecord()
            {
                positions_dw = await ReadPositionFromDB();
                if (positions_dw.IsSuccess && positions_dw.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                return positions_dw.IsSuccess && positions_dw.Value.Count() > 0;
            }

            private void SaveAPIFunctionCall()
            {
                CurrentLocAdd.CalledFrom = GS.selfName;
                CurrentLocAdd.ChangedTime = now;

                if (CAPI.SaveToAPI(CurrentLocAdd, "/api/LocationSave"))
                    CanDeleteTriggerRecord = true;
                else
                    CanDeleteTriggerRecord = false; // error 
            }

            private bool isLocationDifferent()
            {
                Location.dbRow dbRow = CurrentLocAdd.dbRow_instance;
                return dbRow.CompanyID != Existing_dbRow.CompanyID
                    || dbRow.SiteID != Existing_dbRow.SiteID
                    || dbRow.PublicLocationID != Existing_dbRow.PublicLocationID;
            }

            string response = "";
            List<Location.dbRow> CurrentLocations = null;
            private bool ReadFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.Location.Search
                {
                    Paperless_KeyID = trigger_record.KEYID,
                    SearchMode = wbm_common.DataObjects.Location.Search.SearchModeType.Paperless_KeyID
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "LocationSearch");
                if (response == null)
                {
                    logger.Error("Location ReadFromAPI KEYID returned no location: " + trigger_record.KEYID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentLocations = JsonConvert.DeserializeObject<List<Location.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Location ReadFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<Result<List<POSITION_DW>>> ReadPositionFromDB()
            {
                return await Paperless_DB.POSITION_DW_ListByKeyID(trigger_record.KEYID);
            }

            private ToDo makeToDo()
            {
                ToDo toDo = new ToDo();
                toDo.CalledFrom = GS.selfName;
                toDo.ChangedTime = now;
                ToDo.dbRow tododbrow = new ToDo.dbRow();
                tododbrow.DeletedFlag = false;
                tododbrow.CategoryID = 3;
                tododbrow.ChangeHeaderTableID = 30;
                tododbrow.CompanyID = CurrentCompanyID;
                tododbrow.CreatedDateTime = now;
                tododbrow.CreatedMethod = "WBM_Loc_MToDo";
                tododbrow.CreatedUserID = GS.selfUserID;
                tododbrow.Description = "new Location " + (positions_dw.Value[0].POSITION ?? "") + ", please check Pallet Storage Type"; 
                tododbrow.EventDateTime = now;
                tododbrow.LastAmendedDateTime = now;
                tododbrow.LastAmendedMethod = "WBM_Loc_MToDo";// all these rows have limited characters so truncate where you can
                tododbrow.LastAmendedUserID = GS.selfUserID;
                tododbrow.Note = "new location";
                tododbrow.OwnerID = 0;
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
                if (keyid_parts.Length > 0)
                {
                    Site = keyid_parts[0];

                    if (Site == PreviousSiteName)
                    {
                        // Results for this site will be same as previous, re-use
                        CanDeleteTriggerRecord = PreviousCanDeleteTriggerRecord;
                        return PreviousCanProcessTriggerRecord;
                    }

                    wbm_api.FieldState SiteFS = FindSiteFS();
                    if (SiteFS == wbm_api.FieldState.NotFound)
                    {
                        // don't delete, don't process
                        PreviousCanDeleteTriggerRecord = false;
                        PreviousCanProcessTriggerRecord = false;
                        EmailHelper.SendEmail(trigger_record.KEYID, Site, EmailHelper.IssueType.Site, "Location");
                        return false;
                    }
                    else if (SiteFS == wbm_api.FieldState.Inactive)
                    {
                        // exist, inactive, delete and don't process
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
                else
                    EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Format, "Location");

                return false;
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
