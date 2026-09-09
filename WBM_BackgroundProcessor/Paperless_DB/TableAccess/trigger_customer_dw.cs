using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_CUSTOMER_DW>>> TRIGGER_CUSTOMER_DW_List()
        {
            try
            {
                List<TRIGGER_CUSTOMER_DW> trigger_customers = _dbContext.TRIGGER_CUSTOMER_DW.AsNoTracking().ToList();

                if (trigger_customers == null)
                    return Result.Success<List<TRIGGER_CUSTOMER_DW>>(new List<TRIGGER_CUSTOMER_DW>());
                else
                    return Result.Success<List<TRIGGER_CUSTOMER_DW>>(trigger_customers);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_CUSTOMER_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_CUSTOMER_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_CUSTOMER_DW_Delete(TRIGGER_CUSTOMER_DW TCDW)
        {
            try
            {
                _dbContext.TRIGGER_CUSTOMER_DW.Remove(TCDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_CUSTOMER_DW_Delete - " + TCDW.KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
