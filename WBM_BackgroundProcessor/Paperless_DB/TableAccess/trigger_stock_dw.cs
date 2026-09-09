using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_STOCK_DW>>> TRIGGER_STOCK_DW_List()
        {
            try
            {
                List<TRIGGER_STOCK_DW> trigger_stock = _dbContext.TRIGGER_STOCK_DW.AsNoTracking().ToList();

                if (trigger_stock == null)
                    return Result.Success<List<TRIGGER_STOCK_DW>>(new List<TRIGGER_STOCK_DW>());
                else
                    return Result.Success<List<TRIGGER_STOCK_DW>>(trigger_stock);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_STOCK_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_STOCK_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_STOCK_DW_Delete(TRIGGER_STOCK_DW TCDW)
        {
            try
            {
                _dbContext.TRIGGER_STOCK_DW.Remove(TCDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_STOCK_DW_Delete - " + TCDW.KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
