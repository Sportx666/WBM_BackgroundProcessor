using Microsoft.EntityFrameworkCore;
using WBM_BackgroundProcessor.Models;using wbm_common.NonDatabaseObjects;
using WBM_BackgroundProcessor.Models.WBM_DB;

namespace WBM_BackgroundProcessor.WBM_DB
{
    public partial class WBMDatabase
    {
        public async Task<Result<ActiveServices>> ActiveServices_ReadService()
        {
            try
            {
                ActiveServices AS = await _dbContext.ActiveServices.AsNoTracking().FirstOrDefaultAsync(x => x.ApplicationID == 2);

                if (AS == null)
                    return Result.Failure<ActiveServices>("WBM Service not found");
                else
                    return Result.Success<ActiveServices>(AS);
            }
            catch (Exception ex)
            {
                logger.Error("ActiveServices_ReadService - 2: " + ex);
                return Result.Exception<ActiveServices>("Internal Exception occured");
            }
        }

        public bool ActiveServices_UpdateService(ActiveServices AS)
        {
            try
            {
                AS.LogDateTime = DateTime.Now;
                AS.NextStatusDue = AS.LogDateTime.AddMinutes(5);
                _dbContext.ActiveServices.Update(AS);
                _dbContext.SaveChanges();
                _dbContext.ChangeTracker.Clear();
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("ActiveServices_UpdateService - 2: " + ex);
                return false;
            }
        }
    }
}
