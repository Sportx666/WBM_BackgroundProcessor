using Microsoft.Extensions.Configuration;
using WBM_BackgroundProcessor.Paperless_DB;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

paperlessdbcontext.ConnectionString = config.GetConnectionString("paperlessdbConnection");

paperlessdbcontext pp_dbc = new paperlessdbcontext();

PaperlessDatabase db_Paperless = new PaperlessDatabase(pp_dbc);

WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll poll_carrier = new WBM_BackgroundProcessor.Polling.PaperlessPolling.carrier.Poll(db_Paperless);
await poll_carrier.PollTriggerTable();


// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
