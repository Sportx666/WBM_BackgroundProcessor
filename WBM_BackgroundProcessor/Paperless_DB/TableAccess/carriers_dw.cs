using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<CARRIERS_DW>>> CARRIERS_DW_ListByKeyID(string KeyID)
        {
            try
            {
                CARRIERS_DW carrier = await _dbContext.CARRIERS_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

                if (carrier == null)
                    return Result.Success<List<CARRIERS_DW>>(new List<CARRIERS_DW>());
                else
                    return Result.Success<List<CARRIERS_DW>>(new List<CARRIERS_DW> { carrier });
            }
            catch (Exception ex)
            {
                // logger.Error("CARRIERS_DW_ListByKeyID - " + KeyID + ": " + ex);
                return Result.Exception<List<CARRIERS_DW>>("Internal Exception occured");
            }
        }

    }
}
