using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<CONTAINER_DW>>> CONTAINER_DW_ListByListContainer(List<string> conts)
        {
            try
            {
                CONTAINER_DW container = await _dbContext.CONTAINER_DW.AsNoTracking().FirstOrDefaultAsync(x => conts.Contains(x.CONTAINER));

                if (container == null)
                    return Result.Success<List<CONTAINER_DW>>(new List<CONTAINER_DW>());
                else
                    return Result.Success<List<CONTAINER_DW>>(new List<CONTAINER_DW> { container });
            }
            catch (Exception ex)
            {
                logger.Error("CONTAINER_DW_ListByKeyID - " + string.Join(", ", conts) + ": " + ex);
                return Result.Exception<List<CONTAINER_DW>>("Internal Exception occured");
            }
        }
    }
}
