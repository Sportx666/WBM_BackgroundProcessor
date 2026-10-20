using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WBM_BackgroundProcessor.Paperless_DB;
using Microsoft.Extensions.DependencyInjection;
using WBM_BackgroundProcessor.Helpers;
using WBM_BackgroundProcessor.WBM_DB;
using WBM_BackgroundProcessor.Polling.FilePolling;

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

paperlessdbcontext.ConnectionString = config.GetConnectionString("paperlessdbConnection");
paperlessdbcontext pp_dbc = new paperlessdbcontext();
PaperlessDatabase db_Paperless = new PaperlessDatabase(pp_dbc);

WBMdbcontext.ConnectionString = config.GetConnectionString("wbmdbConnection");
WBMdbcontext wbmp_dbc = new WBMdbcontext();
WBMDatabase db_wbm = new WBMDatabase(wbmp_dbc);

stockOnHandFile.Poll.ImportFolder = config["StockOnHandImport:Folder"] ?? "";
if (int.TryParse(config["StockOnHandImport:BatchSize"], out int sohBatchSize) && sohBatchSize > 0)
    stockOnHandFile.Poll.BatchSize = sohBatchSize;

var host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options => {
        options.ServiceName = "WBM_BackgroundProcessor";
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHostedService(x => new Worker(db_Paperless, db_wbm));
    })
    .Build();
host.Run();

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
