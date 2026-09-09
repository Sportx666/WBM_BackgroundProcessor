using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<DB_TRANS>>> DB_TRANS_ListByReference(string Reference)
        {
            try
            {
                List<DB_TRANS> listdbTrans = await _dbContext.DB_TRANS.AsNoTracking().Where(x => x.REFERENCE == Reference).ToListAsync();

                if (listdbTrans == null)
                    return Result.Success<List<DB_TRANS>>(new List<DB_TRANS>());
                else
                    return Result.Success<List<DB_TRANS>>(listdbTrans);
            }
            catch (Exception ex)
            {
                logger.Error("STOCK_MOVE_DW_ListByKeyReference - " + Reference + ": " + ex);
                return Result.Exception<List<DB_TRANS>>("Internal Exception occured");
            }
        }
    }
}
