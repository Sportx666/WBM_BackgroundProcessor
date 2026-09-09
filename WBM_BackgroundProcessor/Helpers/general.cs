using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WBM_BackgroundProcessor.Helpers
{
    public class general
    {
        private static string tempFolder
        {
            get
            {
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetTempPath() + "WBM_API");
                }
                catch (Exception excpt)
                {
                    //logger.Error("Trying to create a folder for Excel temp file" + excpt.Message);
                    return "";
                }
                return System.IO.Path.GetTempPath() + "WBM_API\\";
            }
        }

        public static string GetExcelTempName()
        {
            return tempFolder == "" ? "" : tempFolder + DateTime.Now.ToString("yyyyMMddHHmmss") + ".xlsx";
        }

        public static void debugWriteFile(MemoryStream ms, string routineName)
        {
            string ExcelFile = GetExcelTempName();
            FileInfo exportFile = new FileInfo(ExcelFile);

            if (exportFile.Exists)
            {
                try
                {
                    exportFile.Delete();
                    exportFile = new FileInfo(ExcelFile);
                }
                catch (Exception excpt)
                {
                    //logger.Error(routineName + ": " + excpt.Message);
                    //return Helpers.ActionResultUtils.Send500Response(new List<ErrorMessage> { new ErrorMessage { Error = ErrorCodeHelper.ErrorCodeAsString(ErrorCodes.LocalFileFolder) } }, false, apiTransactionLog, _WBMDB);
                }
            }
            try
            {
                using (FileStream file = new FileStream(ExcelFile, FileMode.Create, System.IO.FileAccess.Write))
                    ms.WriteTo(file);
                Process p = new Process();
                p.StartInfo = new ProcessStartInfo(ExcelFile)
                {
                    UseShellExecute = true
                };
                p.Start();
            }
            catch (Exception excpt)
            {
                //logger.Error(routineName + ": " + excpt.Message);
                //return Helpers.ActionResultUtils.Send500Response(new List<ErrorMessage> { new ErrorMessage { Error = ErrorCodeHelper.ErrorCodeAsString(ErrorCodes.LocalFileFolder) } }, false, apiTransactionLog, _WBMDB);
            }
        }

    }
}
