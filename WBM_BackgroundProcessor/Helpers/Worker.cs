using Microsoft.Extensions.Hosting;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.WBM_DB;
using WBM_BackgroundProcessor.Paperless_DB;
using WBM_BackgroundProcessor.Polling.PaperlessPolling;
using WBM_BackgroundProcessor.WBM_DB;
using WBM_BackgroundProcessor.Models;
using wbm_common.NonDatabaseObjects;

namespace WBM_BackgroundProcessor.Helpers
{
    public class Worker : BackgroundService
    {
        PaperlessDatabase db_Paperless;
        WBMDatabase db_wbm;
        public Worker(PaperlessDatabase indb, WBMDatabase inwbmdb)
        {
            db_Paperless = indb;
            db_wbm = inwbmdb;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            WBM_BackgroundProcessor.Polling.PaperlessPolling.zone.Poll poll_zoneCheck = new WBM_BackgroundProcessor.Polling.PaperlessPolling.zone.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.customerAddress.Poll poll_customerAddress = new WBM_BackgroundProcessor.Polling.PaperlessPolling.customerAddress.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll poll_carrier = new WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.location.Poll poll_location = new WBM_BackgroundProcessor.Polling.PaperlessPolling.location.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.stock.Poll poll_stock = new WBM_BackgroundProcessor.Polling.PaperlessPolling.stock.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.order.Poll poll_order = new WBM_BackgroundProcessor.Polling.PaperlessPolling.order.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.receipt.Poll poll_receipt = new WBM_BackgroundProcessor.Polling.PaperlessPolling.receipt.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.receiptIntegrityCheck.Poll poll_receiptIntegrityCheck = new WBM_BackgroundProcessor.Polling.PaperlessPolling.receiptIntegrityCheck.Poll(db_Paperless);
            WBM_BackgroundProcessor.Polling.PaperlessPolling.orderIntegrityCheck.Poll poll_orderIntegrityCheck = new WBM_BackgroundProcessor.Polling.PaperlessPolling.orderIntegrityCheck.Poll(db_Paperless);

            WBM_BackgroundProcessor.Polling.PaperlessPolling.pwServicerCheck pwSCheck = new pwServicerCheck();
            while (!stoppingToken.IsCancellationRequested)
            {
                Result<ActiveServices> AS = await db_wbm.ActiveServices_ReadService();
                if (AS.IsSuccess)
                {
                    if (!db_wbm.ActiveServices_UpdateService(AS.Value))
                        return;
                }
                else
                    return;

                //InvoiceGenerateHolder IGH = new InvoiceGenerateHolder();
                //IGH.CompanyID = 19;
                //IGH.SiteID = 1;
                //IGH.InvoiceCycleID = 1;
                //IGH.InvoiceDate = new DateTime(2026,6,01);
                //IGH.ListOwnerID = new List<int>() { 1 };
                //IGH.CalledFrom = "WBM";
                //CommonAPI wbm_api = new CommonAPI();
                //wbm_api.SaveToAPI(IGH, "/api/InvoiceGenerateGenerate");


                pwSCheck.Process();

                wbm_api.CheckHeldData();

                //await poll_zoneCheck.PollTriggerTable();
                //await poll_zoneCheck.PollTriggerTable();
                //await poll_customerAddress.PollTriggerTable();
                //await poll_carrier.PollTriggerTable();
                //await poll_location.PollTriggerTable();
                //await poll_stock.PollTriggerTable();
                await poll_order.PollTriggerTable(order.Poll.OrderEnum.TriggerRows);
                await poll_receipt.PollTriggerTable(receipt.Poll.ReceiptEnum.Excel);
                //await poll_receiptIntegrityCheck.PollTriggerTable();
                //await poll_orderIntegrityCheck.PollTriggerTable();

                //WBM_BackgroundProcessor.Polling.PaperlessPolling.pwServicerCheck pwSCheck = new pwServicerCheck();
                await Task.Delay(5 * 60 * 1000, stoppingToken);
            }
        }
    }
}
