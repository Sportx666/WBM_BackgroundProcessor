using Microsoft.EntityFrameworkCore.Storage.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using wbm_common.DataObjects;

namespace WBM_BackgroundProcessor.Helpers
{
    public class tempCosting
    {

        public List<RateCollection.dbRow> ListRateCollection { get; set; }

        public List<RateCode.dbRow> ListRateCode { get; set; }

        public List<TableRate.dbRow> ListTableRate { get; set; }


        private TransactionHeader.dbRow currentHeader;

        private List<TransactionDetail.dbRow> currentListDetail;
        private TransactionDetail.dbRow currentDetail;


        public void ProcessListTrans(ref List<WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans> ListTrans)
        {
            foreach (WBM_BackgroundProcessor.Models.Excel.TransExcel.Trans t in ListTrans)
            {
                currentHeader = t.header;
                currentListDetail = t.ListDetail;

                foreach (TransactionDetail.dbRow detRow in  currentListDetail)
                {
                    currentDetail = detRow;
                    ProcessDetail();

                    currentHeader.InvoiceExFuel += currentDetail.ActualCustomerExtended;
                }

            }
        }


        private void ProcessDetail()
        {
            // If we have a ratecode then use that; otherwise RateCodeID is actually RateFunctionID

            RateCode.dbRow RateCodeRow = null;
            List<TableRate.dbRow> ListTableRateForRate = null;


            RateCodeRow = GetRateCodeRow_ByID(currentDetail.RateCodeID);

            if (RateCodeRow != null)
            {
                ListTableRateForRate = GetTableRate_ByRateCodeID(currentDetail.RateCodeID);
            }

            Decimal ChargeAmount = RateCodeRow.Basic;

            if (ListTableRateForRate != null)
            {
                int NumUnits = currentDetail.Quantity - RateCodeRow.IncludedUnits;

                foreach (TableRate.dbRow trow in ListTableRateForRate)
                {
                    ChargeAmount += (NumUnits * trow.TableCharge);
                    NumUnits -= trow.TableBreak;

                    if (NumUnits < 0)
                        break;
                }
            }

            if (RateCodeRow.MinimumCharge > 0)
            {
                if (ChargeAmount < RateCodeRow.MinimumCharge)
                    ChargeAmount = RateCodeRow.MinimumCharge;
            }

            currentDetail.ActualCustomerExtended = ChargeAmount;

        }

        private RateCode.dbRow GetRateCodeRow_ByID(int ID)
        {
            return ListRateCode.FirstOrDefault(r => r.ID == ID);
        }

        private List<TableRate.dbRow> GetTableRate_ByRateCodeID(int ID)
        {
            return ListTableRate.Where(r => r.RateID == ID).ToList();
        }
    }
}
