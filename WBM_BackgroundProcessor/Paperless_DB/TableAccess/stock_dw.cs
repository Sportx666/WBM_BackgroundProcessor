using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        // this is for stock table
        public async Task<Result<List<STOCK_DW>>> STOCK_DW_ListByKeyID(string KeyID)
        {
            try
            {
                STOCK_DW stock = await _dbContext.STOCK_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

                if (stock == null)
                    return Result.Success<List<STOCK_DW>>(new List<STOCK_DW>());
                else
                    return Result.Success<List<STOCK_DW>>(new List<STOCK_DW> { stock });
            }
            catch (Exception ex)
            {
                logger.Error("STOCK_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<STOCK_DW>>("Internal Exception occured");
            }
        }
    }
}
