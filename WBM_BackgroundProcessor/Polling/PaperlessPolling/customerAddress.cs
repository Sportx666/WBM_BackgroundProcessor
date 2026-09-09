using Newtonsoft.Json;
using NLog;
using NLog.Web;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class customerAddress
    {
        public class Poll
        {
            private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            TRIGGER_CUSTOMER_DW trigger_record;

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
            List<CustomerAddress.dbRow> CurrentCustomerAddress = null;
            CustomerAddress CurrentCustAdd = null;
            CustomerAddress.dbRow Existing_dbRow = null;


            public Paperless_DB.PaperlessDatabase Paperless_DB;
            private CommonAPI CAPI = new CommonAPI();

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_CUSTOMER_DW>> rTRIGGER_CUSTOMER_DW;

                rTRIGGER_CUSTOMER_DW = await Paperless_DB.TRIGGER_CUSTOMER_DW_List();

                if (rTRIGGER_CUSTOMER_DW.IsSuccess)
                {
                    foreach (TRIGGER_CUSTOMER_DW my_trigger_record in rTRIGGER_CUSTOMER_DW.Value)
                    {
                        await ProcessTCDW(my_trigger_record);
                    }
                }
            }

            DateTime now = DateTime.MinValue;
            Result<List<CUSTOMER_DW>> CUSTOMER_DW = null;
            private async Task ProcessTCDW(TRIGGER_CUSTOMER_DW my_trigger_record)
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
                    Result<int> deleted_trigger = await Paperless_DB.TRIGGER_CUSTOMER_DW_Delete(my_trigger_record);
                }
                Console.WriteLine(trigger_record.KEYID + " : " + CanProcessTriggerRecord + " - " + CanDeleteTriggerRecord);
            }

            private bool ProcessRecord()
            {
                // Are we an existing or new record ?

                CurrentCustAdd = new CustomerAddress();

                if (CurrentCustomerAddress != null && CurrentCustomerAddress.Count() > 0)
                {
                    CurrentCustAdd.dbRow_instance = CurrentCustomerAddress[0];

                    //Need to make a copy here... for comparison later - Newtonsoft serialise de-serialise or other ?
                    Existing_dbRow = JsonConvert.DeserializeObject<CustomerAddress.dbRow>(JsonConvert.SerializeObject(CurrentCustAdd.dbRow_instance));
                }
                else
                {
                    // Construct new db_row instance
                    //Make a call here to a function that does the work. Only needs to set bare minimum fields. Maybe not even created as API will do that ?
                    CurrentCustAdd.dbRow_instance = makeNewCustomerAddress();
                    CurrentCustAdd.ToDo = makeToDo();
                }
                copyData();

                return true;
            }

            private CustomerAddress.dbRow makeNewCustomerAddress()
            {
                CustomerAddress.dbRow dbRow = new CustomerAddress.dbRow();
                dbRow.CompanyID = CurrentCompanyID;
                dbRow.OwnerID = CurrentOwnerID;
                dbRow.SiteID = CurrentSiteID;
                dbRow.Paperless_KeyID = trigger_record.KEYID;
                dbRow.PalletHireDelayDays = null;
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
                    if (CurrentCustAdd.dbRow_instance.ID == 0)
                    {
                        SaveAPIFunctionCall();
                    }
                    else if (isCustomerAddressDifferent())
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
                    logger.Error("CustomerAddress SaveFunction " + ex);
                    return false;
                }

                return true;
            }

            private void copyData()
            {
                CurrentCustAdd.dbRow_instance.CompanyID = CurrentCompanyID;
                CurrentCustAdd.dbRow_instance.OwnerID = CurrentOwnerID;
                CurrentCustAdd.dbRow_instance.SiteID = CurrentSiteID;
                CurrentCustAdd.dbRow_instance.CustomerID = CUSTOMER_DW.Value[0].CUST_NO ?? ""; // this is the only field that is required out of these null checks
                CurrentCustAdd.dbRow_instance.Name = CUSTOMER_DW.Value[0].CUST_NAME ?? "";
                CurrentCustAdd.dbRow_instance.AddressLine1 = CUSTOMER_DW.Value[0].CUST_ST1 ?? "";
                CurrentCustAdd.dbRow_instance.AddressLine2 = CUSTOMER_DW.Value[0].CUST_ST2 ?? "";
                CurrentCustAdd.dbRow_instance.AddressLine3 = CUSTOMER_DW.Value[0].CUST_ST3 ?? "";
                CurrentCustAdd.dbRow_instance.AddressLine4 = "";
                CurrentCustAdd.dbRow_instance.Suburb = CUSTOMER_DW.Value[0].CUST_SUB ?? "";
                CurrentCustAdd.dbRow_instance.State = CUSTOMER_DW.Value[0].STATE ?? "";
                CurrentCustAdd.dbRow_instance.PostCode = CUSTOMER_DW.Value[0].CUST_PCODE ?? "";
                CurrentCustAdd.dbRow_instance.Connote_RateCollectionRateChargeCode = "";
            }

            private bool DoTranslations()
            {
                // No trans
                return true;
            }

            private async Task<bool> ReadRecord()
            {
                CUSTOMER_DW = await ReadCustomerAddressFromDB();
                if (CUSTOMER_DW.IsSuccess && CUSTOMER_DW.Value.Count() == 0)
                {
                    CanDeleteTriggerRecord = true;
                    return false;
                }
                return CUSTOMER_DW.IsSuccess && CUSTOMER_DW.Value.Count() > 0;
            }

            private void SaveAPIFunctionCall()
            {
                CurrentCustAdd.CalledFrom = GS.selfName;
                CurrentCustAdd.ChangedTime = now;

                if (CAPI.SaveToAPI(CurrentCustAdd, "/api/CustomerAddressSave"))
                    CanDeleteTriggerRecord = true;
                else
                    CanDeleteTriggerRecord = false; // error 
            }

            private bool isCustomerAddressDifferent()
            {
                CustomerAddress.dbRow dbRow = CurrentCustAdd.dbRow_instance;

                return dbRow.CompanyID != Existing_dbRow.CompanyID
                    || dbRow.OwnerID != Existing_dbRow.OwnerID
                    || dbRow.SiteID != Existing_dbRow.SiteID
                    || dbRow.CustomerID != Existing_dbRow.CustomerID
                    || dbRow.Name != Existing_dbRow.Name
                    || dbRow.AddressLine1 != Existing_dbRow.AddressLine1
                    || dbRow.AddressLine2 != Existing_dbRow.AddressLine2
                    || dbRow.AddressLine3 != Existing_dbRow.AddressLine3
                    || dbRow.AddressLine4 != Existing_dbRow.AddressLine4
                    || dbRow.Suburb != Existing_dbRow.Suburb
                    || dbRow.State != Existing_dbRow.State
                    || dbRow.PostCode != Existing_dbRow.PostCode;
            }

            private bool ReadFromAPI()
            {
                string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(new wbm_common.DataObjects.CustomerAddress.Search
                {
                    Paperless_KeyID = trigger_record.KEYID,
                    SearchMode = wbm_common.DataObjects.CustomerAddress.Search.SearchModeType.Paperless_KeyID
                });
                response = CAPI.FetchFromAPIWithKeyID(jsonString, "CustomerAddressSearch");
                if (response == null)
                {
                    logger.Error("CustomerAddress ReadFromAPI KEYID returned no CustomerAddress: " + trigger_record.KEYID);
                    return false;
                }
                else
                {
                    try
                    {
                        CurrentCustomerAddress = JsonConvert.DeserializeObject<List<CustomerAddress.dbRow>>(response);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("CustomerAddress ReadFromAPI " + ex);
                        return false;
                    }
                }
                return true;
            }

            private async Task<Result<List<CUSTOMER_DW>>> ReadCustomerAddressFromDB()
            {
                return await Paperless_DB.CUSTOMER_DW_ListByKeyID(trigger_record.KEYID);
            }

            private ToDo makeToDo()
            {
                ToDo toDo = new ToDo();
                toDo.CalledFrom = GS.selfName;
                toDo.ChangedTime = now;
                ToDo.dbRow tododbrow = new ToDo.dbRow();
                tododbrow.DeletedFlag = false;
                tododbrow.CategoryID = 2;
                tododbrow.ChangeHeaderTableID = 23;
                tododbrow.CompanyID = CurrentCompanyID;
                tododbrow.CreatedDateTime = now;
                tododbrow.CreatedMethod = "WBM_CA_MToDo";
                tododbrow.CreatedUserID = GS.selfUserID;
                tododbrow.Description = "New Customer " + (CUSTOMER_DW.Value[0].CUST_NAME ?? "") + " for " + Owner;
                tododbrow.EventDateTime = now;
                tododbrow.LastAmendedDateTime = now;
                tododbrow.LastAmendedMethod = "WBM_CA_MToDo";// all these rows have limited characters so truncate where you can
                tododbrow.LastAmendedUserID = GS.selfUserID;
                tododbrow.Note = "Please check Pallet Hire Days, Invoice Enclosed";
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
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner + ", " + Site, EmailHelper.IssueType.Multiple, "Customer Address");
                            else if (SiteFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Site, EmailHelper.IssueType.Site, "Customer Address");
                            else if (OwnerFS == wbm_api.FieldState.NotFound)
                                EmailHelper.SendEmail(trigger_record.KEYID, Owner, EmailHelper.IssueType.Owner, "Customer Address");
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
                    else
                        EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Owner, "Customer Address");
                }
                else
                    EmailHelper.SendEmail(trigger_record.KEYID, "", EmailHelper.IssueType.Format, "Customer Address");

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
