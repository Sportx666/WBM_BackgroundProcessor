using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_PURCH_ORD_PROD_DW>>> TRIGGER_PURCH_ORD_PROD_DW_List()
        {
            try
            {
                List<TRIGGER_PURCH_ORD_PROD_DW> trigger_PICK_HEAD = _dbContext.TRIGGER_PURCH_ORD_PROD_DW.AsNoTracking().ToList();

                if (trigger_PICK_HEAD == null)
                    return Result.Success<List<TRIGGER_PURCH_ORD_PROD_DW>>(new List<TRIGGER_PURCH_ORD_PROD_DW>());
                else
                    return Result.Success<List<TRIGGER_PURCH_ORD_PROD_DW>>(trigger_PICK_HEAD);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_PURCH_ORD_PROD_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_PURCH_ORD_PROD_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_PURCH_ORD_PROD_DW_DeleteList(List<TRIGGER_PURCH_ORD_PROD_DW> TCDW)
        {
            try
            {
                _dbContext.TRIGGER_PURCH_ORD_PROD_DW.RemoveRange(TCDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_PURCH_ORD_PROD_DW_Delete - " + TCDW[0].KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
