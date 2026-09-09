using NLog;
using NLog.Web;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Database;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.DataLookups;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Models.Excel
{
    public class ProductQtyExcelReport
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public MemoryStream ms;
        public bool HasErrors;
        public List<ReceiptAndListsObject> ReceiptAndLists;

        public class ReceiptAndListsObject
        {
            public List<PURCH_ORD_PROD_DW> purchs { get; set; }
            public List<STOCK_MOVE_DW> stockmoves { get; set; }
            public List<ZoneProcessing.dbRow> zones { get; set; }
            public List<PALLET_DW> pallets { get; set; }
        }
        public ProductQtyExcelReport(List<ReceiptAndListsObject> inList)
        {
            ReceiptAndLists = inList;
            ms = new MemoryStream();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public async Task<bool> GenerateExcel()
        {
            try
            {
                using (ExcelPackage ep = new ExcelPackage(ms))
                {
                    MainPage(ep);
                    ep.Save();
                }
            }
            catch (Exception excpt)
            {
                logger.Error("ReceiptAndListsObject GenerateExcel: " + excpt.Message);
                return false;
            }

            return !HasErrors;
        }

        ExcelWorksheet WS = null;
        int rc = 2;

        Boolean FirstHeader = false;
        private void MainPage(ExcelPackage ep)
        {
            try
            {
                WS = ep.Workbook.Worksheets.Add("Summary");

                WS.View.FreezePanes(2, 1);
                WS.Cells[1, 1, 1, 3].Style.Font.Bold = true;
                WS.Cells[1, 1, 1, 3].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                WS.Column(1).Width = 20;
                WS.Column(2).Width = 10;
                WS.Column(3).Width = 10;

                WS.Cells[1, 1].Value = "Receipt";
                WS.Cells[1, 2].Value = "Qty";
                WS.Cells[1, 3].Value = "Date Recv";

                for (int i = 0; i < ReceiptAndLists.Count(); i++)
                {
                    string receipt = ReceiptAndLists[i].purchs[0].KEYID.Split('_')[0] + "_" + ReceiptAndLists[i].purchs[0].KEYID.Split('_')[1];
                    WS.Cells[2 + i, 2].Value = ReceiptAndLists[i].purchs.GroupBy(x => x.PROD_NO).Select(x => x.Average(y => y.QTY_ORD.GetValueOrDefault())).Sum();

                    WS.Cells[2 + i, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if (ProcessObj(ep, ReceiptAndLists[i], receipt, i))
                        WS.Cells[2 + i, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                        WS.Cells[2 + i, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    WS.Cells[2 + i, 1].Hyperlink = new Uri("#" + receipt.Replace("-", "").Replace("*", "") + "!A1", UriKind.Relative);
                    WS.Cells[2 + i, 1].Value = receipt;
                    WS.Cells[2 + i, 3].Value = ReceiptAndLists[i].purchs[0].DATE_RECV.GetValueOrDefault().ToString("d");
                }
            }
            catch (Exception e)
            {
                logger.Error("ReceiptAndListsObject MainPage: " + e.Message);
            }
        }

        private bool ProcessObj(ExcelPackage ep, ReceiptAndListsObject RLO, string receipt, int MPrw)
        {
            try
            {
                bool success = true;
                ExcelWorksheet subWS = ep.Workbook.Worksheets.Add(receipt.Replace("-","").Replace("*",""));
                subWS.View.FreezePanes(2, 1);
                subWS.Cells[1, 1, 1, 6].Style.Font.Bold = true;
                subWS.Cells[1, 1, 1, 6].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                subWS.Column(1).Width = 20;
                subWS.Column(2).Width = 20;
                subWS.Column(3).Width = 20;
                subWS.Column(4).Width = 20;
                subWS.Column(5).Width = 20;
                subWS.Column(6).Width = 20;

                subWS.Cells[1, 1].Hyperlink = new Uri("#Summary!A" + (MPrw + 2).ToString(), UriKind.Relative);
                subWS.Cells[1, 1].Value = "Product Code";
                subWS.Cells[1, 2].Value = "Purch Ord Prod Qty";
                subWS.Cells[1, 3].Value = "Stock Move Check In Qty";
                subWS.Cells[1, 4].Value = "Stock Move Warehouse";
                subWS.Cells[1, 5].Value = "Stock Move Put Away Qty";
                subWS.Cells[1, 6].Value = "Stock Move Full Rep Qty";
                int rxnm = 0;
                int totalaimfor = 0;
                foreach (string prodNum in RLO.purchs.Select(x => x.PROD_NO).Distinct())
                {
                    subWS.Cells[2 + rxnm, 1].Value = prodNum;
                    int numToAimFor = (int)RLO.purchs.Where(x => x.PROD_NO == prodNum).Average(x => x.QTY_ORD.GetValueOrDefault());
                    subWS.Cells[2 + rxnm, 2].Value = numToAimFor;
                    totalaimfor = totalaimfor + numToAimFor;
                    List<STOCK_MOVE_DW> checkins = RLO.stockmoves.Where(x => x.MOVE_TYPE == "CHECK.IN" && x.PRODUCT_CODE == prodNum).ToList();
                    if (checkins.Count() > 0)
                    {
                        int mvqty = (int)checkins.Sum(x => x.MOVE_QTY.GetValueOrDefault());
                        subWS.Cells[2 + rxnm, 3].Value = mvqty;
                        subWS.Cells[2 + rxnm, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        if (numToAimFor == mvqty)
                            subWS.Cells[2 + rxnm, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        else
                        {
                            subWS.Cells[2 + rxnm, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                            success = false;
                        }
                    }
                    List<STOCK_MOVE_DW> putaways = RLO.stockmoves.Where(x => x.MOVE_TYPE == "PUT.AWAY" && x.PRODUCT_CODE == prodNum).ToList();
                    int putawaynum = 0;
                    if (putaways.Count() > 0)
                    {
                        putawaynum = (int)putaways.Sum(x => x.MOVE_QTY.GetValueOrDefault());
                        subWS.Cells[2 + rxnm, 5].Value = putawaynum;
                    }
                    List<STOCK_MOVE_DW> fullrep = RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.REP" && x.PRODUCT_CODE == prodNum 
                    && RLO.zones.Where(y => y.IsReceiving).Any(y => y.ZoneNumber == x.FROM_ZONE)).ToList();
                    int fullrepnum = 0;
                    if (fullrep.Count() > 0)
                    {
                        fullrepnum = (int)fullrep.Sum(x => x.MOVE_QTY.GetValueOrDefault());
                        subWS.Cells[2 + rxnm, 6].Value = fullrepnum;
                    }
                    subWS.Cells[2 + rxnm, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    subWS.Cells[2 + rxnm, 4].Value = fullrepnum + putawaynum;
                    if (numToAimFor == putawaynum + fullrepnum)
                        subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }
                    
                    rxnm++;
                }
                subWS.Cells[2 + rxnm, 1].Value = RLO.purchs[0].DATE_RECV.GetValueOrDefault().ToString("d");
                subWS.Cells[2 + rxnm, 2].Value = "Total Purch: " + totalaimfor.ToString();
                subWS.Cells[2 + rxnm, 3].Value = "Total Check in: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "CHECK.IN").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 4].Value = "Total Warehouse: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "PUT.AWAY" || 
                (x.MOVE_TYPE == "FULL.REP" && RLO.zones.Where(y => y.IsReceiving).Any(y => y.ZoneNumber == x.FROM_ZONE))).Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 5].Value = "Total Put Away: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "PUT.AWAY").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 6].Value = "Total Full Rep: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.REP").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();

                rxnm++;
                subWS.Cells[2 + rxnm, 1].Value = "List Of Missing Pallet PALNO";
                List<string> Relaventpalnosinstockmoves = RLO.stockmoves.Where(x => x.MOVE_TYPE == "PUT.AWAY" || x.MOVE_TYPE == "CHECK.IN" 
                || (x.MOVE_TYPE == "FULL.REP" && RLO.zones.Any(y => y.ZoneNumber == x.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(x.SITE_NO).Item1) 
                && RLO.zones.First(y => y.ZoneNumber == x.FROM_ZONE && y.SiteID == wbm_api.GetSiteNumberCompanyID(x.SITE_NO).Item1).IsReceiving)
                ).Select(x => x.PAL_NO).Distinct().ToList();
                List<string> justPalNo = RLO.pallets.Select(x => x.PAL_NO).Distinct().ToList();
                subWS.Cells[2 + rxnm, 2].Value = string.Join(",", Relaventpalnosinstockmoves.Where(x => !justPalNo.Contains(x)));
                subWS.Cells[2 + rxnm, 3].Value = "List Of Missing Stock Pallet PALNO";
                subWS.Cells[2 + rxnm, 4].Value = string.Join(',', justPalNo.Where(x => !Relaventpalnosinstockmoves.Contains(x)));
                subWS.Cells[2 + rxnm, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if (Relaventpalnosinstockmoves.Where(x => !justPalNo.Contains(x)).Count() == 0 
                    && justPalNo.Where(x => !Relaventpalnosinstockmoves.Contains(x)).Count() == 0)
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    success = false;
                }
                rxnm++;
                subWS.Cells[2 + rxnm, 1].Value = "List Of Stock Move PALNO that aren't in Pallet at all";
                subWS.Cells[2 + rxnm, 2].Value = string.Join(",", RLO.stockmoves.Select(x => x.PAL_NO).Where(x => !justPalNo.Contains(x)).Distinct().Order());
                subWS.Cells[2 + rxnm, 3].Value = "List Of Pallet PALNO that aren't in Stock Move at all";
                subWS.Cells[2 + rxnm, 4].Value = string.Join(',', justPalNo.Where(x => !RLO.stockmoves.Select(x => x.PAL_NO).Contains(x)).Distinct().Order());

                return success;
            }
            catch (Exception e)
            {
                logger.Error("ReceiptAndListsObject ProcessObj: " + e.Message);
                return false;
            }
        }
    }
}
