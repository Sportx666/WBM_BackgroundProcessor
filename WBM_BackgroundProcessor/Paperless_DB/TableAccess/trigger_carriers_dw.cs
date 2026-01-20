using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;
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
                // logger.Error("CARRIERS_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<TRIGGER_CARRIERS_DW>>("Internal Exception occured");
            }
        }

    }
}
