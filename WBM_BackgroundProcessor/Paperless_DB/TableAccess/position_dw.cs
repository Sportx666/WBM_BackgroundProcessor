using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<POSITION_DW>>> POSITION_DW_ListByKeyID(string KeyID)
        {
            try
            {
                POSITION_DW position = await _dbContext.POSITION_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

                if (position == null)
                    return Result.Success<List<POSITION_DW>>(new List<POSITION_DW>());
                else
                    return Result.Success<List<POSITION_DW>>(new List<POSITION_DW> { position });
            }
            catch (Exception ex)
            {
                logger.Error("POSITION_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<POSITION_DW>>("Internal Exception occured");
            }
        }
    }
}
