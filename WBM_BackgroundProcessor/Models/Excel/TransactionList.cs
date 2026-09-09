using NLog;
using NLog.Web;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Data;
using System.Reflection.PortableExecutable;
using wbm_common.DataObjects;
using static WBM_BackgroundProcessor.Models.Excel.ExcelExtraction;

namespace WBM_BackgroundProcessor.Models.Excel
{
    public class TransExcel
    {
        private static Logger logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        public class Trans
        {
            public TransactionHeader.dbRow header { get; set; }

            public List<TransactionDetail.dbRow> ListDetail { get; set; }

            public Trans(TransactionHeader.dbRow th, List<TransactionDetail.dbRow> ListTD)
            {
                this.header = th;
                this.ListDetail = ListTD;
            }
        }

        public MemoryStream ms;

        public List<Trans> ListTrans = null;

        public string ErrorMessage { get; set; }

        public Boolean HasErrors = false;

        public List<(string, string)> ValuesThatDidntComplete;

        public TransExcel()
        {
            ms = new MemoryStream();
        }

        public async Task<bool> GenerateExcel()
        {
            if (await GetData())
            {
                try
                {
                    using (ExcelPackage ep = new ExcelPackage(ms))
                    {
                        TransPage(ep);
                        ep.Save();
                    }
                }
                catch (Exception excpt)
                {
                    logger.Error("TransactionList GenerateExcel: " + excpt.Message);
                    return false;
                }
            }
            else
                HasErrors = true;

            return !HasErrors;
        }

        private async Task<bool> GetData()
        {
            return true;
        }

        Trans CurrentTrans = null;
        ExcelWorksheet WS = null;
        int rc = 2;

        Boolean FirstHeader = false;

        public Dictionary<string, List<EERow>> ExcelValues { get; set; }
        // matches up our text and their charges since their names are slightly different
        List<(string, string)> textmismatch = new List<(string, string)> { ("Wrap out pallet", "PALLET REWRAPPING - TRANSPORT"),
        ("EDI Order", "STANDARD ORDER CHARGE"), ("Pick carton", "CARTON PICK"), ("Outbound label", "DESPATCH LABEL FEE ")
            , ("Invoice Enclosed Envelope", "INVOICE ENCLOSED ENVELOPE")
        , ("Pick full pallet", " FULL PALLET PICK "), ("Load out full pallet", " TRUCKLOAD FULL PALLET ")
            , ("Outwards Pallet Wrapping", " PALLET REWRAPPING - TRANSPORT "), ("Outwards Labelling", " DESPATCH LABEL FEE ")
            , ("Load out carton", " TRUCKLOAD CARTON "), ("Truckload Pallet", " TRUCKLOAD FULL PALLET "),
        ("Putaway full pallet", " PUT AWAY PALLET "), ("Putaway Carton", " PUT AWAY CARTON "), ("Putaway Pallet", " PUT AWAY PALLET "),
        ("Wrap In Pallet", "INWARDS PALLET WRAPPING"), ("EDI Receipt Return", "RECEIPT PROCESSING FEE"),
        ("Container Lift", "CONTAINER LIFT CHARGE"), ("Devan Loose","CONTAINER UN-LOADING - 40' (L)"),
        ("3rd Pty Connote","3RD PARTY CON NOTE PREPARATION"), ("Priority Order", "PRIORITY ORDER FEE")
        , ("3rd Party Connote", "3RD PARTY CON NOTE PREPARATION")};
        List<string> NoQtyCheckNeeded = new List<string> { "Container Lift", "Devan Loose" };

        private void TransPage(ExcelPackage ep)
        {
            try
            {
                WS = ep.Workbook.Worksheets.Add("Transactions");

                WS.View.FreezePanes(2, 1);
                WS.Cells[1, 1, 1, 21].Style.Font.Bold = true;
                WS.Cells[1, 1, 1, 21].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
                WS.Column(1).Width = 10;
                WS.Column(2).Width = 10;
                WS.Column(3).Width = 10;
                WS.Column(4).Width = 10;
                WS.Column(5).Width = 17;
                WS.Column(5).Style.Numberformat.Format = "dd/mm/yy HH:mm";
                WS.Column(6).Width = 18;
                WS.Column(7).Width = 20;
                WS.Column(8).Width = 25;
                WS.Column(9).Width = 3;
                WS.Column(10).Width = 17;
                WS.Column(11).Width = 20;
                WS.Column(12).Width = 20;
                WS.Column(13).Width = 30;
                WS.Column(14).Width = 10;
                WS.Column(15).Width = 13;
                //WS.Column(15).Style.Numberformat.Format = "$#,##0.00";

                WS.Column(16).Width = 13;
                //WS.Column(16).Style.Numberformat.Format = "$#,##0.00";

                WS.Cells[1, 1].Value = "Company";
                WS.Cells[1, 2].Value = "Owner";
                WS.Cells[1, 3].Value = "Site";
                WS.Cells[1, 4].Value = "Source";
                WS.Cells[1, 5].Value = "Transaction Date";
                WS.Cells[1, 6].Value = "Transaction Type";
                WS.Cells[1, 7].Value = "Reference";
                WS.Cells[1, 8].Value = "Description";
                WS.Cells[1, 9].Value = "";
                WS.Cells[1, 10].Value = "Rate Function ID";
                WS.Cells[1, 11].Value = "Rate Code";
                WS.Cells[1, 12].Value = "Charge Description";
                WS.Cells[1, 13].Value = "Full Description";
                WS.Cells[1, 14].Value = "Quantity";
                WS.Cells[1, 15].Value = "Total Charge";
                WS.Cells[1, 16].Value = "Line Charge";

                WS.Column(17).Width = 20;
                WS.Column(18).Width = 35;
                WS.Cells[1, 18].Value = "Descr";
                WS.Cells[1, 19].Value = "SumOfQty";
                WS.Cells[1, 20].Value = "Rate";
                WS.Cells[1, 21].Value = "Amount";

                for (int i = 0; i < ListTrans.Count(); i++)
                {
                    CurrentTrans = ListTrans[i];

                    ProcessTransaction();
                }
                foreach ((string, string) othervalues in ValuesThatDidntComplete)
                {
                    WS.Cells[rc, 7].Value = othervalues.Item1;
                    WS.Cells[rc, 8].Value = othervalues.Item2;
                    rc++;
                }
            }
            catch (Exception e)
            {
                logger.Error("TransactionList TransPage: " + e.Message);
                HasErrors = true;
            }
        }

        private void ProcessTransaction()
        {
            try
            {
                if (CurrentTrans.ListDetail.Count() > 0)
                {
                    FirstHeader = true;
                    foreach (TransactionDetail.dbRow detail in CurrentTrans.ListDetail.Where(x => x.ActualCustomerExtended > 0))
                    {
                        ProcessHeader();
                        ProcessDetail(detail);
                    }
                    if (ExcelValues.TryGetValue(CurrentTrans.header.TransactionReference, out List<EERow> output))
                    {
                        List<EERow> missingrows = output.Where(x => x.Descr != "" && !CurrentTrans.ListDetail.Any(y => y.FullDescription.ToUpper() == x.Descr || textmismatch.Any(z => z.Item1 == y.FullDescription && z.Item2.Trim() == x.Descr.Trim()))).ToList();
                        if (missingrows.Count() > 0)
                        {
                            foreach(var mr in missingrows)
                            {
                                WS.Cells[rc, 7].Value = CurrentTrans.header.TransactionReference;
                                WS.Cells[rc, 17].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                WS.Cells[rc, 17].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                                WS.Cells[rc, 17].Value = "MISSING ROW:";
                                WS.Cells[rc, 18].Value = mr.Descr;
                                WS.Cells[rc, 19].Value = mr.SumQty.ToString("0");
                                WS.Cells[rc, 20].Value = mr.MinRate.ToString("0.00");
                                WS.Cells[rc, 21].Value = mr.SumTotal.ToString("0.00");
                                rc++;
                            }
                        }
                    }
                }
                else
                {
                    ProcessHeader();
                }
            }
            catch (Exception e)
            {
                logger.Error("TransactionList ProcessTransaction: " + e.Message);
                HasErrors = true;
            }
        }

        private void ProcessHeader()
        {
            try
            {
                TransactionHeader.dbRow header = CurrentTrans.header;

                WS.Cells[rc, 1].Value = header.CompanyID;
                WS.Cells[rc, 2].Value = header.OwnerID;
                WS.Cells[rc, 3].Value = header.LastAmendedMethod;

                WS.Cells[rc, 4].Value = header.CreatedMethod;
                WS.Cells[rc, 5].Value = header.TransactionDateTime.ToOADate();
                WS.Cells[rc, 6].Value = header.TransactionTypeID;
                WS.Cells[rc, 7].Value = header.TransactionReference;
                WS.Cells[rc, 8].Value = header.Description;

                if (FirstHeader)
                {
                    WS.Cells[rc, 15].Value = header.InvoiceExFuel;
                    FirstHeader = false;

                    if (ExcelValues.TryGetValue(CurrentTrans.header.TransactionReference, out List<EERow> output))
                    {
                        List<EERow> totalRow = output.Where(x => x.Descr == "").ToList();
                        if (totalRow.Count() == 1)
                        {
                            if (CurrentTrans.header.InvoiceExFuel == totalRow[0].SumTotal)
                            {
                                WS.Cells[rc, 15].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                WS.Cells[rc, 15].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                            }
                            else
                            {
                                WS.Cells[rc, 15].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                WS.Cells[rc, 15].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                            }
                            rc++;
                        }
                    }
                }

            }
            catch (Exception e)
            {
                logger.Error("TransactionList ProcessHeader: " + e.Message);
                HasErrors = true;
            }
        }

        private void ProcessDetail(TransactionDetail.dbRow detail)
        {
            try
            {

                WS.Cells[rc, 7].Value = CurrentTrans.header.TransactionReference;
                WS.Cells[rc, 10].Value = detail.RateCodeID;
                WS.Cells[rc, 11].Value = detail.RateCode;
                WS.Cells[rc, 12].Value = detail.ChargeDescription;
                WS.Cells[rc, 13].Value = detail.FullDescription;
                WS.Cells[rc, 14].Value = detail.Quantity;

                WS.Cells[rc, 16].Value = detail.ActualCustomerExtended;


                if (ExcelValues.TryGetValue(CurrentTrans.header.TransactionReference, out List<EERow> output))
                {
                    EERow match = output.FirstOrDefault(x => x.Descr == detail.FullDescription.ToUpper() || textmismatch.Any(y => y.Item1 == detail.FullDescription && y.Item2.Trim() == x.Descr.Trim()));
                    if (match != null)
                    {
                        WS.Cells[rc, 7].Value = CurrentTrans.header.TransactionReference;
                        WS.Cells[rc, 17].Value = "MATCH:";
                        WS.Cells[rc, 18].Value = match.Descr;
                        WS.Cells[rc, 19].Value = match.SumQty.ToString("0");
                        WS.Cells[rc, 20].Value = match.MinRate.ToString("0.00");
                        WS.Cells[rc, 21].Value = match.SumTotal.ToString("0.00");
                        if (Math.Abs(detail.ActualCustomerExtended - match.SumTotal) < (decimal)0.01)
                        {
                            WS.Cells[rc, 21].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            WS.Cells[rc, 21].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                        }
                        else
                        {
                            WS.Cells[rc, 21].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            WS.Cells[rc, 21].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                        }
                        if (!NoQtyCheckNeeded.Contains(detail.FullDescription))
                        {
                            if ((decimal)detail.Quantity == match.SumQty)
                            {
                                WS.Cells[rc, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                WS.Cells[rc, 19].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                            }
                            else
                            {
                                WS.Cells[rc, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                WS.Cells[rc, 19].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                            }
                        }
                    }
                    else
                    {
                        WS.Cells[rc, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        WS.Cells[rc, 13].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightPink);
                    }
                }
                rc++;
            }
            catch (Exception e)
            {
                logger.Error("TransactionList ProcessDetail: " + e.Message);
                HasErrors = true;
            }
        }
    }
}
