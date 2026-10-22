using Microsoft.Extensions.Configuration;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Paperless_DB;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

paperlessdbcontext.ConnectionString = config.GetConnectionString("paperlessdbConnection");

paperlessdbcontext pp_dbc = new paperlessdbcontext();

PaperlessDatabase db_Paperless = new PaperlessDatabase(pp_dbc);

//WBM_BackgroundProcessor.Polling.PaperlessPolling.customerAddress.Poll poll_customerAddress = new WBM_BackgroundProcessor.Polling.PaperlessPolling.customerAddress.Poll(db_Paperless);
//WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll poll_carrier = new WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll(db_Paperless);
//WBM_BackgroundProcessor.Polling.PaperlessPolling.location.Poll poll_location = new WBM_BackgroundProcessor.Polling.PaperlessPolling.location.Poll(db_Paperless);
//WBM_BackgroundProcessor.Polling.PaperlessPolling.stock.Poll poll_stock = new WBM_BackgroundProcessor.Polling.PaperlessPolling.stock.Poll(db_Paperless);
//WBM_BackgroundProcessor.Polling.PaperlessPolling.order.Poll poll_order = new WBM_BackgroundProcessor.Polling.PaperlessPolling.order.Poll(db_Paperless);
//WBM_BackgroundProcessor.Polling.PaperlessPolling.receipt.Poll poll_receipt = new WBM_BackgroundProcessor.Polling.PaperlessPolling.receipt.Poll(db_Paperless);
WBM_BackgroundProcessor.Polling.stockOnHandFile.Poll.ImportFolder = config["StockOnHandImport:Folder"] ?? "";

WBM_BackgroundProcessor.Polling.stockOnHandFile.Poll poll_stockOnHandFile = new WBM_BackgroundProcessor.Polling.stockOnHandFile.Poll();

wbm_api.CheckHeldData();

poll_stockOnHandFile.PollFolder();

//await poll_customerAddress.PollTriggerTable();
//await poll_carrier.PollTriggerTable();
//await poll_location.PollTriggerTable();
//await poll_stock.PollTriggerTable();
//await poll_order.PollTriggerTable();
//await poll_receipt.PollTriggerTable();

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
