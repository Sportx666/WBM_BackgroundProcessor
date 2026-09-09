using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<TRUCK_LOAD_DW>>> TRUCK_LOAD_DW_ListByLoad_No(string Load_No)
        {
            try
            {
                List<TRUCK_LOAD_DW> listTruckLoad = await _dbContext.TRUCK_LOAD_DW.AsNoTracking().Where(x => x.LOAD_NO == Load_No).ToListAsync();

                if (listTruckLoad == null)
                    return Result.Success<List<TRUCK_LOAD_DW>>(new List<TRUCK_LOAD_DW>());
                else
                    return Result.Success<List<TRUCK_LOAD_DW>>(listTruckLoad);
            }
            catch (Exception ex)
            {
                logger.Error("TRUCK_LOAD_DW_ListByLoad_No - " + Load_No + ": " + ex);
                return Result.Exception<List<TRUCK_LOAD_DW>>("Internal Exception occured");
            }
        }
    }
}
