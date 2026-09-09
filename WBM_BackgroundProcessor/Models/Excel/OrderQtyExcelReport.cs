using NLog;
using NLog.Web;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.DataObjects;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Models.Excel
{
    public class OrderQtyExcelReport
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public MemoryStream ms;
        public bool HasErrors;
        public List<OrderAndListsObject> OrderAndLists;

        public class OrderAndListsObject
        {
            public PICK_HEAD_DW head { get; set; }
            public List<PICK_DETAIL_DW> details { get; set; }
            public List<STOCK_MOVE_DW> stockmoves { get; set; }
            public List<DB_TRANS> trans { get; set; }
            public List<TRUCK_LOAD_DW> truckloads { get; set; }
        }
        public OrderQtyExcelReport(List<OrderAndListsObject> inList)
        {
            OrderAndLists = inList;
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
                logger.Error("OrderQtyExcelReport GenerateExcel: " + excpt.Message);
                return false;
            }

            return !HasErrors;
        }

        ExcelWorksheet WS = null;
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

                for (int i = 0; i < OrderAndLists.Count(); i++)
                {
                    string receipt = OrderAndLists[i].head.KEYID;
                    WS.Cells[2 + i, 2].Value = OrderAndLists[i].details.GroupBy(x => x.PROD_NO).Select(x => x.Sum(y => y.QTY_PICKED.GetValueOrDefault())).Sum();

                    WS.Cells[2 + i, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if (ProcessObj(ep, OrderAndLists[i], receipt, i))
                        WS.Cells[2 + i, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                        WS.Cells[2 + i, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    WS.Cells[2 + i, 1].Hyperlink = new Uri("#" + receipt.Replace('-', '_').Replace('*', '_') + "!A1", UriKind.Relative);
                    WS.Cells[2 + i, 1].Value = receipt;
                    WS.Cells[2 + i, 3].Value = OrderAndLists[i].head.DATE_RECV.GetValueOrDefault().ToString("d");
                }
            }
            catch (Exception e)
            {
                logger.Error("OrderQtyExcelReport MainPage: " + e.Message);
            }
        }

        private bool ProcessObj(ExcelPackage ep, OrderAndListsObject RLO, string receipt, int MPrw)
        {
            try
            {
                bool hasTrans = RLO.trans.Count() > 0;
                bool success = true;
                ExcelWorksheet subWS = ep.Workbook.Worksheets.Add(receipt.Replace('-', '_').Replace('*', '_'));
                subWS.View.FreezePanes(2, 1);
                subWS.Cells[1, 1, 1, hasTrans ? 9 : 6].Style.Font.Bold = true;
                subWS.Cells[1, 1, 1, hasTrans ? 9 : 6].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                subWS.Column(1).Width = 20;
                subWS.Column(2).Width = 20;
                subWS.Column(3).Width = 20;
                subWS.Column(4).Width = 20;
                subWS.Column(5).Width = 20;
                subWS.Column(6).Width = 20;
                if (hasTrans)
                {
                    subWS.Column(7).Width = 20;
                    subWS.Column(8).Width = 20;
                    subWS.Column(9).Width = 20;
                }

                subWS.Cells[1, 1].Hyperlink = new Uri("#Summary!A" + (MPrw + 2).ToString(), UriKind.Relative);
                subWS.Cells[1, 1].Value = "Product Code";
                subWS.Cells[1, 2].Value = "Pick Detail Qty Ordered";
                subWS.Cells[1, 3].Value = "Stock Move PART.PCK";
                subWS.Cells[1, 4].Value = "Stock Move FULL.PICK";
                subWS.Cells[1, 5].Value = "Stock Move Total";
                subWS.Cells[1, 6].Value = "Stock Move DESP.DROP";
                if (hasTrans)
                {
                    subWS.Cells[1, 7].Value = "DB TRANS PART.PCK";
                    subWS.Cells[1, 8].Value = "DB TRANS FULL.PICK";
                    subWS.Cells[1, 9].Value = "DB TRANS Total";
                }
                int rxnm = 0;
                int totalaimfor = 0;
                foreach (string prodNum in RLO.details.Select(x => x.PROD_NO).Distinct())
                {
                    subWS.Cells[2 + rxnm, 1].Value = prodNum;
                    int numToAimFor = (int)RLO.details.Where(x => x.PROD_NO == prodNum).Sum(x => x.QTY_PICKED.GetValueOrDefault());
                    totalaimfor = totalaimfor + numToAimFor;
                    subWS.Cells[2 + rxnm, 2].Value = numToAimFor;
                    int partpick = (int)RLO.stockmoves.Where(x => x.PRODUCT_CODE == prodNum && x.MOVE_TYPE == "PART.PCK").Sum(x => x.MOVE_QTY.GetValueOrDefault());
                    subWS.Cells[2 + rxnm, 3].Value = partpick;
                    int fullpick = (int)RLO.stockmoves.Where(x => x.PRODUCT_CODE == prodNum && x.MOVE_TYPE == "FULL.PICK").Sum(x => x.MOVE_QTY.GetValueOrDefault());
                    subWS.Cells[2 + rxnm, 4].Value = fullpick;
                    subWS.Cells[2 + rxnm, 5].Value = fullpick + partpick;
                    subWS.Cells[2 + rxnm, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if (numToAimFor == fullpick + partpick)
                        subWS.Cells[2 + rxnm, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }
                    int despdrop = (int)RLO.stockmoves.Where(x => x.PRODUCT_CODE == prodNum && x.MOVE_TYPE == "DESP.DROP").Sum(x => x.MOVE_QTY.GetValueOrDefault());
                    subWS.Cells[2 + rxnm, 6].Value = despdrop;
                    subWS.Cells[2 + rxnm, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if (numToAimFor == despdrop)
                        subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }

                    if (hasTrans)
                    {
                        int transpartpick = (int)RLO.trans.Where(x => x.DESCRIPTION.StartsWith(prodNum) && x.ACTION == "PART.PCK").Sum(x => x.QUANTITY.GetValueOrDefault());
                        subWS.Cells[2 + rxnm, 7].Value = transpartpick;
                        subWS.Cells[2 + rxnm, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        if (transpartpick / 100 == partpick)
                            subWS.Cells[2 + rxnm, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        else
                        {
                            subWS.Cells[2 + rxnm, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                            success = false;
                        }
                        int transfullpick = (int)RLO.trans.Where(x => x.DESCRIPTION.StartsWith(prodNum) && x.ACTION == "FULL.PICK").Sum(x => x.QUANTITY.GetValueOrDefault());
                        subWS.Cells[2 + rxnm, 8].Value = transfullpick;
                        subWS.Cells[2 + rxnm, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        if (transfullpick / 100 == fullpick)
                            subWS.Cells[2 + rxnm, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        else
                        {
                            subWS.Cells[2 + rxnm, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                            success = false;
                        }
                        subWS.Cells[2 + rxnm, 9].Value = transfullpick + transpartpick;
                        subWS.Cells[2 + rxnm, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        if (numToAimFor == (transfullpick + transpartpick) / 100)
                            subWS.Cells[2 + rxnm, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        else
                        {
                            subWS.Cells[2 + rxnm, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                            success = false;
                        }
                    }
                    rxnm++;
                }
                subWS.Cells[2 + rxnm, 1].Value = RLO.head.DATE_RECV.GetValueOrDefault().ToString("d");
                subWS.Cells[2 + rxnm, 2].Value = "Total: " + totalaimfor.ToString();
                subWS.Cells[2 + rxnm, 3].Value = "Total: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "PART.PCK").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 4].Value = "Total: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.PICK").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 5].Value = "Total: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.PICK" || x.MOVE_TYPE == "PART.PCK").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.PICK" || x.MOVE_TYPE == "PART.PCK").Sum(x => x.MOVE_QTY.GetValueOrDefault()) == totalaimfor)
                    subWS.Cells[2 + rxnm, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 5].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    success = false;
                }
                subWS.Cells[2 + rxnm, 6].Value = "Total: " + ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "DESP.DROP").Sum(x => x.MOVE_QTY.GetValueOrDefault())).ToString();
                subWS.Cells[2 + rxnm, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "DESP.DROP").Sum(x => x.MOVE_QTY.GetValueOrDefault()) == totalaimfor)
                    subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    success = false;
                }
                if (hasTrans)
                {
                    subWS.Cells[2 + rxnm, 7].Value = "Total: " + ((int)RLO.trans.Where(x => x.ACTION == "PART.PCK").Sum(x => x.QUANTITY.GetValueOrDefault())).ToString();
                    subWS.Cells[2 + rxnm, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "PART.PCK").Sum(x => x.MOVE_QTY.GetValueOrDefault()) == 100 * (int)RLO.trans.Where(x => x.ACTION == "PART.PCK").Sum(x => x.QUANTITY.GetValueOrDefault()))
                        subWS.Cells[2 + rxnm, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 7].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }
                    subWS.Cells[2 + rxnm, 8].Value = "Total: " + ((int)RLO.trans.Where(x => x.ACTION == "FULL.PICK").Sum(x => x.QUANTITY.GetValueOrDefault())).ToString();
                    subWS.Cells[2 + rxnm, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "FULL.PICK").Sum(x => x.MOVE_QTY.GetValueOrDefault()) == 100 * (int)RLO.trans.Where(x => x.ACTION == "FULL.PICK").Sum(x => x.QUANTITY.GetValueOrDefault()))
                        subWS.Cells[2 + rxnm, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }
                    subWS.Cells[2 + rxnm, 9].Value = "Total: " + ((int)RLO.trans.Where(x => x.ACTION == "DESP.DROP").Sum(x => x.QUANTITY.GetValueOrDefault())).ToString();
                    subWS.Cells[2 + rxnm, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    if ((int)RLO.stockmoves.Where(x => x.MOVE_TYPE == "DESP.DROP").Sum(x => x.MOVE_QTY.GetValueOrDefault()) == 100 * (int)RLO.trans.Where(x => x.ACTION == "DESP.DROP").Sum(x => x.QUANTITY.GetValueOrDefault()))
                        subWS.Cells[2 + rxnm, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    else
                    {
                        subWS.Cells[2 + rxnm, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                        success = false;
                    }
                }

                rxnm = rxnm + 2;
                subWS.Cells[2 + rxnm, 1].Value = "Pick Head Total Pallets:";
                subWS.Cells[2 + rxnm, 2].Value = RLO.head.PAL_TYPE_COUNTED;
                subWS.Cells[2 + rxnm, 3].Value = "Truck Load Total Pallets:";
                subWS.Cells[2 + rxnm, 4].Value = ((int)RLO.truckloads.Sum(x => x.NO_PALS)).ToString();

                rxnm++;
                subWS.Cells[2 + rxnm, 1].Value = "Pick Head Cartons Qty:";
                subWS.Cells[2 + rxnm, 2].Value = ((int)RLO.head.CARTONS_QTY).ToString();
                subWS.Cells[2 + rxnm, 3].Value = "Truck Load Num Rows:";
                subWS.Cells[2 + rxnm, 4].Value = RLO.truckloads.Count().ToString();
                subWS.Cells[2 + rxnm, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if ((int)RLO.truckloads.Sum(x => x.NO_PALS) == RLO.truckloads.Count())
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    success = false;
                }

                rxnm++;
                subWS.Cells[2 + rxnm, 1].Value = "Pick Head Total Pick Ctns:";
                subWS.Cells[2 + rxnm, 2].Value = ((int)RLO.head.TOT_PICK_CTNS).ToString();
                /*subWS.Cells[2 + rxnm, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if ((int)RLO.head.TOT_PICK_CTNS == totalaimfor)
                    subWS.Cells[2 + rxnm, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 2].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    success = false;
                }*/

                rxnm++;
                subWS.Cells[2 + rxnm, 1].Value = "Pick Head Number of Lines:";
                subWS.Cells[2 + rxnm, 2].Value = ((int)RLO.head.NO_LNES).ToString();
                subWS.Cells[2 + rxnm, 3].Value = "Pick Detail number of Lines:";
                int detailsmax = RLO.details.Max(x => int.TryParse(x.HOST_LNE_NO, out int tmpint) ? tmpint : 0);
                List<int> distinctdetaillnnums = RLO.details.Select(x => int.TryParse(x.HOST_LNE_NO, out int tmpint) ? tmpint : 0)
                    .Where(x => x != 0).Distinct().ToList();
                subWS.Cells[2 + rxnm, 4].Value = distinctdetaillnnums.Count();
                List<int> uptodetaillist = Enumerable.Range(1, detailsmax).ToList();
                subWS.Cells[2 + rxnm, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if (uptodetaillist.Count() == distinctdetaillnnums.Count() && !uptodetaillist.Any(x => !distinctdetaillnnums.Contains(x)))
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    //success = false;
                }
                subWS.Cells[2 + rxnm, 5].Value = "Detail LnNo match Head LnNo:";
                subWS.Cells[2 + rxnm, 6].Value = detailsmax;
                subWS.Cells[2 + rxnm, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                if ((int)RLO.head.NO_LNES == RLO.details.Max(x => int.TryParse(x.HOST_LNE_NO, out int tmpint) ? tmpint : 0))
                    subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                else
                {
                    subWS.Cells[2 + rxnm, 6].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.Pink);
                    //success = false;
                }
                return success;
            }
            catch (Exception e)
            {
                logger.Error("OrderQtyExcelReport ProcessObj: " + e.Message);
                return false;
            }
        }
    }
}
