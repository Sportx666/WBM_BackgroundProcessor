using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<ZONE_DW>>> ZONE_DW_ListByKeyID(string KeyID)
        {
            try
            {
                ZONE_DW zone = await _dbContext.ZONE_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

                if (zone == null)
                    return Result.Success<List<ZONE_DW>>(new List<ZONE_DW>());
                else
                    return Result.Success<List<ZONE_DW>>(new List<ZONE_DW> { zone });
            }
            catch (Exception ex)
            {
                logger.Error("ZONE_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<ZONE_DW>>("Internal Exception occured");
            }
        }
    }
}