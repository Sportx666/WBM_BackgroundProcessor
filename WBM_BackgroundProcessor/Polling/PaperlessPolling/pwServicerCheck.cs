using Microsoft.Extensions.Logging.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WBM_BackgroundProcessor.Polling.PaperlessPolling
{
    public class pwServicerCheck
    {
        public void Process()
        {
            string[] ListFolders = Directory.GetDirectories("\\\\cti-dc-opdg\\PWServicerLogs");

            List<string> ListOrdered = ListFolders.OrderDescending().ToList();

            //string[] ListFolders = Directory.GetDirectories("\\\\cti-dc-opdg\\PWServicerLogs", "Errors0708");

            List<String> ListKnownErrors = new List<string>();
            ListKnownErrors.Add("SHUTDOWN is in progress");
            ListKnownErrors.Add("Invalid object name");
            ListKnownErrors.Add("String or binary data would be truncated in table");
            ListKnownErrors.Add(" Cannot find the object");
            ListKnownErrors.Add("Invalid column name");
            ListKnownErrors.Add("Invalid reply received from the host");
            ListKnownErrors.Add("The connection is broken and recovery is not possible");


            ListFolders.Order().ToList();

            foreach (string Folder in ListOrdered)
            {
                Console.WriteLine("Folder: " + Folder);

                string processedFolder = Folder + "\\Processed";

                if (Directory.Exists(processedFolder) == false)
                {
                    Directory.CreateDirectory(processedFolder);
                }


                List<string> ListFileNames = Directory.GetFiles(Folder).ToList();
                ListFileNames.Order().ToList();

                List<(string, string)> logerrorpair = new List<(string, string)>();

                foreach (string filename in ListFileNames.Where(x => !x.Contains(".ERR")))
                {
                    if (ListFileNames.Any(x => x.Contains(filename) && x != filename))
                        logerrorpair.Add((filename, ListFileNames.First(x => x.Contains(filename) && x != filename)));
                }

                int cnt = 0;
                Console.WriteLine("  " + logerrorpair.Count);
                foreach ((string, string) logerrpair in logerrorpair)
                {
                    cnt = cnt + 1;
                    List<string> errorfle = File.ReadAllLines(logerrpair.Item2).ToList();
                    List<string> sourcefle = File.ReadAllLines(logerrpair.Item1).ToList();

                    if (errorfle.Count() > 0 && errorfle[0].Contains("Object reference not set to an instance of an object."))
                    {
                        emptyfile(errorfle, sourcefle, logerrpair);
                        continue;
                    }

                    if (errorfle.Count() > 0)
                    {
                        Boolean FoundMatch = false;
                        foreach (string err in ListKnownErrors)
                        {
                            if (errorfle[0].Contains(err))
                            {
                                FoundMatch = true;
                                break;
                            }
                        }

                        if (FoundMatch)
                        {
                            string FromLoc = logerrpair.Item1;
                            string ToLoc = "\\\\cti-dc-opdg\\PWServicerTextFiles\\" + new FileInfo(logerrpair.Item1).Name;
                            File.Move(FromLoc, ToLoc, true);
                            File.Delete(logerrpair.Item2);
                        }
                    }

                    switch (new FileInfo(logerrpair.Item1).Name.Split(']')[0])
                    {// each own function eventually
                        case "POSITION_DW":
                            if (sourcefle.Count() >= 7 && sourcefle[6] == "#REF!")
                            {
                                // set to 0, move error file and delete error file. ALSO NO CURRENT ACCESS
                                sourcefle[6] = "0";
                                File.WriteAllLines(logerrpair.Item1, sourcefle);

                                string FromLoc = logerrpair.Item1;
                                string ToLoc = "\\\\cti-dc-opdg\\PWServicerTextFiles\\" + new FileInfo(logerrpair.Item1).Name;

                                File.Move(FromLoc, ToLoc, true);
                                File.Delete(logerrpair.Item2);
                            }
                            
                            if (sourcefle.Count() >= 7 && sourcefle[1] == "LK080" && sourcefle[6] == "V")
                            {
                                // glitched file, ignore and delete error file.                                
                                File.Delete(logerrpair.Item2);
                            }

                            break;
                        case "PALLET_DW":
                            break;
                        case "STOCK_MOVE_DW":
                            stockmove(errorfle, sourcefle, logerrpair);
                            break;
                        case "DB_TRANS":
                            // DB_TRANS field 25 = INV_LINE_NO
                            // If blank or non-numeric, change it to 0 and resend the record.
                            if (sourcefle.Count() >= 26 &&
                                !Int32.TryParse(sourcefle[25].Trim(), out int invLineNo))
                            {
                                sourcefle[25] = "0";
                                File.WriteAllLines(logerrpair.Item1, sourcefle);

                                string FromLoc = logerrpair.Item1;
                                string ToLoc = "\\\\cti-dc-opdg\\PWServicerTextFiles\\" + new FileInfo(logerrpair.Item1).Name;

                                File.Move(FromLoc, ToLoc, true);
                                File.Delete(logerrpair.Item2);

                            }

                            // DB_TRANS field 5 = REFERENCE
                            // If value contains '/' and has an extra ' at the end, we remove the extra ' and reprocess the record.
                            if (sourcefle.Count() >= 6 &&
                                sourcefle[5].Contains("/"))
                            {
                                sourcefle[5] = sourcefle[5].TrimEnd('\'');
                                sourcefle[22] = sourcefle[22].TrimEnd('\'');
                                File.WriteAllLines(logerrpair.Item1, sourcefle);

                                string FromLoc = logerrpair.Item1;
                                string ToLoc = "\\\\cti-dc-opdg\\PWServicerTextFiles\\" + new FileInfo(logerrpair.Item1).Name;

                                File.Move(FromLoc, ToLoc, true);
                                File.Delete(logerrpair.Item2);
                            }

                            

                            break;
                        case "STOCK_DW":
                            if (Decimal.TryParse(sourcefle[1].Trim(), out Decimal test))
                            {
                                if (test > 1000000m)
                                {
                                    errorfle.Add("Checked by temp PWServicerChecker, found to have " + sourcefle[1].Trim() + " as a weight, too long");
                                    File.WriteAllLines(logerrpair.Item2, errorfle);

                                    string FromLoc = logerrpair.Item1;
                                    string ToLoc = new FileInfo(logerrpair.Item1).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item1).Name;

                                    File.Move(FromLoc, ToLoc, true);

                                    FromLoc = logerrpair.Item2;
                                    ToLoc = new FileInfo(logerrpair.Item2).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item2).Name;

                                    File.Move(FromLoc, ToLoc, true);
                                }
                            }

                            if (sourcefle[22].Trim() == "287429" || sourcefle[22] == "285444")
                            {
                                // fixed product in PW, remove old errors 
                                File.Delete(logerrpair.Item2);
                            }                            
                            break;
                        default:
                            break;
                    }
                }

                Console.WriteLine("  Folder done");
            }
        }


        private static void stockmove(List<string> errorfle, List<string> sourcefle, (string, string) logerrpair)
        {
            // also check weight number added is too big?
            errorfle.Add("Checked by temp PWServicerChecker, found to have " + sourcefle[0].Trim());
            File.WriteAllLines(logerrpair.Item2, errorfle);
            switch (sourcefle[0].Trim())
            {
                case "COUNT.BACK":
                case "STK.CHK":
                case "CLOSE":
                case "MAN.MVE":
                    //move to processed
                    File.Move(logerrpair.Item1, new FileInfo(logerrpair.Item1).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item1).Name, true);
                    File.Move(logerrpair.Item2, new FileInfo(logerrpair.Item2).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item2).Name, true);
                    break;
                case "CHECK.IN":
                case "TR.LOAD":
                    //keep as is
                    break;
                default:
                    break;
            }
        }

        private static void emptyfile(List<string> errorfle, List<string> sourcefle, (string, string) logerrpair)
        {
            if (sourcefle.Count() < 2)
            {
                //move to processed as empty
                errorfle.Add("Checked by temp PWServicerChecker, found to have " + sourcefle[0].Trim());
                File.WriteAllLines(logerrpair.Item2, errorfle);
                File.Move(logerrpair.Item2, new FileInfo(logerrpair.Item2).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item2).Name, true);
                File.Move(logerrpair.Item1, new FileInfo(logerrpair.Item1).DirectoryName + "\\Processed\\" + new FileInfo(logerrpair.Item1).Name, true);
            }
            else
            {
                //move to be reprocessed as full
                string FromLoc = logerrpair.Item1;
                string ToLoc = "\\\\cti-dc-opdg\\PWServicerTextFiles\\" + new FileInfo(logerrpair.Item1).Name;
                File.Move(FromLoc, ToLoc, true);
                File.Delete(logerrpair.Item2);
            }
        }

    }
}
