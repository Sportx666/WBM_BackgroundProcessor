using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<STOCK_MOVE_DW>>> STOCK_MOVE_DW_ListByInvNoOrOrdNo(string InvNo)
        {
            try
            {
                List<STOCK_MOVE_DW> liststockmove = await _dbContext.STOCK_MOVE_DW.AsNoTracking().Where(x => x.INV_NO.StartsWith(InvNo) || x.ORDERNO.StartsWith(InvNo)).ToListAsync();

                if (liststockmove == null)
                    return Result.Success<List<STOCK_MOVE_DW>>(new List<STOCK_MOVE_DW>());
                else
                    return Result.Success<List<STOCK_MOVE_DW>>(liststockmove);
            }
            catch (Exception ex)
            {
                logger.Error("STOCK_MOVE_DW_ListByKeyIDStartsWith - " + InvNo + ": " + ex);
                return Result.Exception<List<STOCK_MOVE_DW>>("Internal Exception occured");
            }
        }
    }
}
