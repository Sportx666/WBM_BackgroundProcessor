using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models;
using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PURCH_ORD_PROD_DW>>> PURCH_ORD_PROD_DW_ListByKeyID(string KeyID)
        {
            try
            {
                List<PURCH_ORD_PROD_DW> purchords = await _dbContext.PURCH_ORD_PROD_DW.AsNoTracking().Where(x => x.KEYID.Contains(KeyID)).ToListAsync();

                if (purchords == null)
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(new List<PURCH_ORD_PROD_DW>());
                else
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(purchords);
            }
            catch (Exception ex)
            {
                logger.Error("PURCH_ORD_PROD_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<PURCH_ORD_PROD_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<List<PURCH_ORD_PROD_DW>>> PURCH_ORD_PROD_DW_ListLoadWithUFlagBetweenDates(DateTime FromDate, DateTime ToDate)
        {
            try
            {
                List<PURCH_ORD_PROD_DW> purchords = await _dbContext.PURCH_ORD_PROD_DW.AsNoTracking().Where(x => x.DATE_RECV >= FromDate && x.DATE_RECV <= ToDate && x.PO_CLR_LOAD_FLG.Trim() == "U").ToListAsync();

                if (purchords == null)
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(new List<PURCH_ORD_PROD_DW>());
                else
                    return Result.Success<List<PURCH_ORD_PROD_DW>>(purchords);
            }
            catch (Exception ex)
            {
                logger.Error("PURCH_ORD_PROD_DW_ListBetweenDates - " + FromDate.ToString() + " - " + ToDate.ToString() + ": " + ex);
                return Result.Exception<List<PURCH_ORD_PROD_DW>>("Internal Exception occured");
            }
        }
    }
}
