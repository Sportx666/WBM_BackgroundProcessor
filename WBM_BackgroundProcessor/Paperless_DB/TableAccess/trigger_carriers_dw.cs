using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_CARRIERS_DW>>> TRIGGER_CARRIERS_DW_List()
        {
            try
            {
                List<TRIGGER_CARRIERS_DW> trigger_carriers = _dbContext.TRIGGER_CARRIERS_DW.AsNoTracking().ToList();

                if (trigger_carriers == null)
                    return Result.Success<List<TRIGGER_CARRIERS_DW>>(new List<TRIGGER_CARRIERS_DW>());
                else
                    return Result.Success<List<TRIGGER_CARRIERS_DW>>(trigger_carriers);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_CARRIERS_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_CARRIERS_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_CARRIERS_DW_Delete(TRIGGER_CARRIERS_DW TCDW)
        {
            try
            {
                _dbContext.TRIGGER_CARRIERS_DW.Remove(TCDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_CARRIERS_DW_Delete - " + TCDW.KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
