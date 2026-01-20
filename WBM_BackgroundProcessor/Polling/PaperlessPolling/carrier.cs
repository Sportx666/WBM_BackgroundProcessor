using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class carrier
    {
        public class Poll
        {
            TRIGGER_CARRIERS_DW trigger_record;

            Boolean CanDeleteTriggerRecord = false;
            Boolean CanProcessTriggerRecord = false;
            Boolean PreviousCanDeleteTriggerRecord = false;
            Boolean PreviousCanProcessTriggerRecord = false;

            int CurrentSiteID = 0;

            string PreviousSiteName = "";

            public Paperless_DB.PaperlessDatabase Paperless_DB;

            public Poll(Paperless_DB.PaperlessDatabase paperless_DB)
            {
                Paperless_DB = paperless_DB;
            }

            public async Task PollTriggerTable()
            {
                Result<List<TRIGGER_CARRIERS_DW>> rtrigger_carriers_dw;

                rtrigger_carriers_dw = await Paperless_DB.TRIGGER_CARRIERS_DW_List();

                if (rtrigger_carriers_dw.IsSuccess)
                {
                    foreach (TRIGGER_CARRIERS_DW my_trigger_record in rtrigger_carriers_dw.Value)
                    {
                        CanDeleteTriggerRecord = false;
                        CanProcessTriggerRecord = false;

                        trigger_record = my_trigger_record;

                        if (CanProcess())
                        {
                            // Read Source Record
                            // Do Translations
                            // Read API record
                            // Save API record
                            // if no errors, can delete
                            CanDeleteTriggerRecord = true;
                        }
                        if (CanDeleteTriggerRecord)
                        {
                            // Delete trigger record
                        }
                        Console.WriteLine(trigger_record.KEYID + " : " + CanProcessTriggerRecord + " - " + CanDeleteTriggerRecord);
                    }
                }
            }

            private Boolean CanProcess()
            {
                Boolean canProcess = false;

                // First part of KEYID is the site; need to see if the site is enabled before proceeding further
                String[] keyid_parts = trigger_record.KEYID.Split('_');
                if (keyid_parts.Length > 0)
                {
                    string Site = keyid_parts[0];

                    if (Site == PreviousSiteName)
                    {
                        // Results for this site will be same as previous, re-use
                        CanProcessTriggerRecord = PreviousCanProcessTriggerRecord;
                        CanDeleteTriggerRecord = PreviousCanDeleteTriggerRecord;
                    }
                    else
                    {
                        CurrentSiteID = wbm_api.GetSiteNumber(Site);

                        if (CurrentSiteID > 0)
                        {
                            if (wbm_api.IsSiteActive(CurrentSiteID))
                            {
                                // Can process
                                canProcess = true;
                                CanProcessTriggerRecord = true;
                            }
                            else
                            {
                                // Not active, do not need the trigger record
                                CanDeleteTriggerRecord = true;
                            }
                        }
                        PreviousCanDeleteTriggerRecord = CanDeleteTriggerRecord;
                        PreviousCanProcessTriggerRecord = CanProcessTriggerRecord;
                    }
                }

                return canProcess;
            }

        }

    }
}
