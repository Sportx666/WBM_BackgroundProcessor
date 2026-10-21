using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using NLog;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Models.Excel
{
    public class TransExcel
    {
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

        public TransExcel(List<Trans> ListTrans)
        {
            ms = new MemoryStream();
            this.ListTrans = ListTrans;
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
                    //logger.Error("ToDoExcelReport: " + excpt.Message);
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

        private void TransPage(ExcelPackage ep)
        {
            try
            {
                WS = ep.Workbook.Worksheets.Add("Transactions");

                WS.View.FreezePanes(2, 1);
                WS.Cells[1, 1, 1, 16].Style.Font.Bold = true;
                WS.Cells[1, 1, 1, 16].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
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
                WS.Column(15).Style.Numberformat.Format = "$#,##0.00";

                WS.Column(16).Width = 13;
                WS.Column(16).Style.Numberformat.Format = "$#,##0.00";

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
                WS.Cells[1, 16].Value = "LIne Charge";


                for (int i = 0; i < ListTrans.Count(); i++)
                {
                    CurrentTrans = ListTrans[i];

                    ProcessTransaction();
                }
            }
            catch (Exception e)
            {
                int i = 1;
            }
        }

        private void ProcessTransaction()
        {
            try
            {
                if (CurrentTrans.ListDetail.Count() > 0)
                {
                    FirstHeader = true;
                    foreach (TransactionDetail.dbRow detail in CurrentTrans.ListDetail)
                    {
                        ProcessHeader();
                        ProcessDetail(detail);
                    }
                }
                else
                {
                    ProcessHeader();
                }
            }
            catch (Exception e)
            {
                int i = 1;
            }

        }

        private void ProcessHeader()
        {
            try
            {
                TransactionHeader.dbRow header = CurrentTrans.header;

                WS.Cells[rc, 1].Value = header.CompanyID;
                WS.Cells[rc, 2].Value = header.OwnerID;
                //WS.Cells[rc, 3].Value = header.SiteID;
                WS.Cells[rc, 3].Value = header.LastAmendedMethod;

                //WS.Cells[rc, 4].Value = header.TransactionSourceID;
                WS.Cells[rc, 4].Value = header.CreatedMethod;
                WS.Cells[rc, 5].Value = header.TransactionDateTime.ToOADate();
                WS.Cells[rc, 6].Value = header.TransactionTypeID;
                WS.Cells[rc, 7].Value = header.TransactionReference;
                WS.Cells[rc, 8].Value = header.Description;

                if (FirstHeader)
                {
                    WS.Cells[rc, 15].Value = header.InvoiceExFuel;
                    FirstHeader = false;
                }

            }
            catch (Exception e)
            {
                int i = 1;
            }   
        }

        private void ProcessDetail(TransactionDetail.dbRow detail)
        {
            try
            {

                WS.Cells[rc, 10].Value = detail.RateCodeID;
                WS.Cells[rc, 11].Value = detail.RateCode;
                WS.Cells[rc, 12].Value = detail.ChargeDescription;
                WS.Cells[rc, 13].Value = detail.FullDescription;
                WS.Cells[rc, 14].Value = detail.Quantity;

                WS.Cells[rc, 16].Value = detail.ActualCustomerExtended;


                rc++;
            }
            catch (Exception e)
            {
                int i = 1;
            }
        }
    }
}
