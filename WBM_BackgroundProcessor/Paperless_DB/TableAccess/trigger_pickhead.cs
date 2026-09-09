using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRIGGER_PICK_HEAD_DW>>> TRIGGER_PICK_HEAD_DW_List()
        {
            try
            {
                List<TRIGGER_PICK_HEAD_DW> trigger_PICK_HEAD = _dbContext.TRIGGER_PICK_HEAD_DW.AsNoTracking().ToList();

                if (trigger_PICK_HEAD == null)
                    return Result.Success<List<TRIGGER_PICK_HEAD_DW>>(new List<TRIGGER_PICK_HEAD_DW>());
                else
                    return Result.Success<List<TRIGGER_PICK_HEAD_DW>>(trigger_PICK_HEAD);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_PICK_HEAD_DW_List: " + ex);
                return Result.Exception<List<TRIGGER_PICK_HEAD_DW>>("Internal Exception occured");
            }
        }

        public async Task<Result<int>> TRIGGER_PICK_HEAD_DW_Delete(TRIGGER_PICK_HEAD_DW TCDW)
        {
            try
            {
                _dbContext.TRIGGER_PICK_HEAD_DW.Remove(TCDW);
                int result = await _dbContext.SaveChangesAsync();
                return Result.Success<int>(result);
            }
            catch (Exception ex)
            {
                logger.Error("TRIGGER_PICK_HEAD_DW_Delete - " + TCDW.KEYID + ": " + ex);
                return Result.Exception<int>("Internal Exception occured");
            }
        }
    }
}
