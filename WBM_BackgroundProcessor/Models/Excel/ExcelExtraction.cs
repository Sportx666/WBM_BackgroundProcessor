using NLog;
using NLog.Web;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Information;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WBM_BackgroundProcessor.Models.Excel
{

    public class ExcelExtraction
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public class EERow
        {
            public string Descr { get; set; }
            public decimal SumQty { get; set; }
            public decimal MinRate { get; set; }
            public decimal SumTotal { get; set; }
        }
        public static Dictionary<string, List<EERow>> ExtractOrder()
        {
            return Extract(ConfigurationManager.AppSettings["OrderSheetName"]);
        }

        public static Dictionary<string, List<EERow>> ExtractReceipt()
        {
            return Extract(ConfigurationManager.AppSettings["ReceiptSheetName"]);
        }
        private static Dictionary<string, List<EERow>> Extract(string WSName)
        {
            Dictionary<string, List<EERow>> ExcelValues = new Dictionary<string, List<EERow>>();
            try
            {
                MemoryStream ms = new MemoryStream();
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                ExcelPackage epmap = new ExcelPackage(new System.IO.FileInfo(ConfigurationManager.AppSettings["WEFileName"]));
                ExcelWorksheets wss = epmap.Workbook.Worksheets;
                ExcelWorksheet ws = wss.Where(x => x.Name == WSName).First();
                int rwcnt = 1;
                while (ws.Cells[rwcnt, 3].Value == null || !decimal.TryParse(ws.Cells[rwcnt, 3].Value.ToString(), out decimal tmlforic))
                    rwcnt++;
                string lastord = "";
                while (!(ws.Cells[rwcnt, 1].Value != null && ws.Cells[rwcnt, 1].Value.ToString() == "Grand Total"))
                {
                    if (ws.Cells[rwcnt, 1].Value == null || !ws.Cells[rwcnt, 1].Value.ToString().Trim().Contains("Total"))
                    {
                        if (ws.Cells[rwcnt, 1].Value != null && ws.Cells[rwcnt, 1].Value.ToString() != "")
                            lastord = 
                                //(isRec ? "02300" : "") + 
                                ws.Cells[rwcnt, 1].Value.ToString().Trim().Split(" ")[0];
                        EERow curRow = new EERow();
                        curRow.Descr = ws.Cells[rwcnt, 2].Value != null ? ws.Cells[rwcnt, 2].Value.ToString() : "";
                        curRow.SumQty = decimal.Parse(ws.Cells[rwcnt, 3].Value.ToString());
                        curRow.MinRate = decimal.Parse(ws.Cells[rwcnt, 4].Value.ToString());
                        curRow.SumTotal = decimal.Parse(ws.Cells[rwcnt, 5].Value.ToString());
                        if (ExcelValues.TryGetValue(lastord, out List<EERow> outtoaddto))
                            outtoaddto.Add(curRow);
                        else
                            ExcelValues.Add(lastord, new List<EERow> { curRow });
                    }
                    rwcnt++;
                }
            }
            catch(Exception ex)
            {
                logger.Error("Excel Extraction Error: " + ex.Message);
            }
            return ExcelValues;
        }
    }
}
