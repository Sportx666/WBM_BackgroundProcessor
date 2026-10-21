using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;
using WBM_BackgroundProcessor.Models.Paperless_DB;

namespace WBM_BackgroundProcessor.Paperless_DB
{
    public partial class PaperlessDatabase
    {
        public async Task<Result<List<PICK_HEAD_DW>>> PICK_HEAD_DW_ListByKeyID(string KeyID)
        {
            try
            {
                PICK_HEAD_DW pickhead = await _dbContext.PICK_HEAD_DW.AsNoTracking().FirstOrDefaultAsync(x => x.KEYID == KeyID);

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
    }
}
