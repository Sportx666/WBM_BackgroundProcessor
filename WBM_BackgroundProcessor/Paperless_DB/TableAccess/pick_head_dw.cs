using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;
using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;
using wbm_common.Paperless_DB_DataObjects;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PICK_HEAD_DW>>> PICK_HEAD_DW_ListByKeyID(string KeyID)
        {
            try
            {
                PICK_HEAD_DW pickhead = await _dbContext.PICK_HEAD_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID.Contains(KeyID));

                if (pickhead == null)
                    return Result.Success<List<PICK_HEAD_DW>>(new List<PICK_HEAD_DW>());
                else
                    return Result.Success<List<PICK_HEAD_DW>>(new List<PICK_HEAD_DW> { pickhead });
            }
            catch (Exception ex)
            {
                logger.Error("PICK_HEAD_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<PICK_HEAD_DW>>("Internal Exception occured");
            }
        }
        public async Task<Result<List<PICK_HEAD_DW>>> PICK_HEAD_DW_ListBetweenDates(DateTime FromDate, DateTime ToDate)
        {
            try
            {
                List<PICK_HEAD_DW> pickheads = await _dbContext.PICK_HEAD_DW.AsNoTracking().Where(x => x.DATE_RECV != null && x.DATE_RECV >= FromDate && x.DATE_RECV <= ToDate).ToListAsync();

                if (pickheads == null)
                    return Result.Success<List<PICK_HEAD_DW>>(new List<PICK_HEAD_DW>());
                else
                    return Result.Success<List<PICK_HEAD_DW>>(pickheads);
            }
            catch (Exception ex)
            {
                logger.Error("PICK_HEAD_DW_ListBetweenDates - " + FromDate.ToString() + " - " + ToDate.ToString() + ": " + ex);
                return Result.Exception<List<PICK_HEAD_DW>>("Internal Exception occured");
            }
        }
    }
}
