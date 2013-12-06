using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace PS2PDF
{
    public class ProcessJob
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(ProcessJob));

        public enum JobState
        {
            Ready,
            InProgress,
            Successful,
            Failed
        }

        public JobState State { get; private set; }

        public delegate void JobEndDelegate(JobState state);
        public event JobEndDelegate JobEnd;

        private string inputFilePath;
        
        public string InputFileName { get; private set; }
        public ObservableCollection<KeyValuePair<string, DistillingServiceControlConstants.LogSeverity>> JobLog { get; private set; }

        public ProcessJob(string inputFileName, string inputFilePath)
        {
            JobLog = new ObservableCollection<KeyValuePair<string, DistillingServiceControlConstants.LogSeverity>>();
            
            InputFileName = inputFileName;
            this.inputFilePath = inputFilePath;
            
            State = JobState.Ready;
            
            logInfo("Job initialized.");
        }

        public void ProcessFile(object state)
        {
            State = JobState.InProgress;
            logInfo("Job started.");

            try
            {
                // ########## MOVE INPUT FILE TO WORK DIR ################################################################################

                if (!Directory.Exists(Properties.Settings.Default.WorkingFolderPath))
                    Directory.CreateDirectory(Properties.Settings.Default.WorkingFolderPath);

                string workingFilePath = Path.Combine(Properties.Settings.Default.WorkingFolderPath, InputFileName);

                if (File.Exists(workingFilePath))
                    File.Delete(workingFilePath);

                while (isFileLocked(new FileInfo(inputFilePath)))
                    Thread.Sleep(1000);

                File.Move(inputFilePath, workingFilePath);

                logInfo("Input file moved to working directory.");



                // ########## ANALYSE INPUT FILE #########################################################################################

                var allLines = File.ReadAllLines(workingFilePath);
                
                List<string> filesToConcat = allLines.Skip(8)                                                                // skip first 8 lines
                                                     .Select(s => s.TrimStart('(').Replace(") prun", "").Replace("/", "\\")) // remove "(" .. ") prun" and turn slashes to backslashes
                                                     .Where(s => !string.IsNullOrWhiteSpace(s))                              // skip empty lines (eof)
                                                     .ToList();

                foreach (string filePath in filesToConcat.ToList()) // clone collection to modify the original one.
                {
                    if (File.Exists(filePath))
                        continue;

                    filesToConcat.Remove(filePath);
                    logWarning(string.Format("File {0} doesn't exist. Removed from list!", filePath));
                }

                if (!filesToConcat.Any())
                {
                    logWarning("No Files to concatenate found, exit!");
                    return;
                }

                logInfo(string.Format("Found {0} files to concatenate.", filesToConcat.Count));



                // ########## WRITE PDFMARKS FILE ########################################################################################

                string pdfmarksFilePath = workingFilePath.Replace(".ps", "_pdfmarks.ps");
                File.WriteAllLines(pdfmarksFilePath, allLines.Take(2)); 
                filesToConcat.Add(pdfmarksFilePath);

                logInfo("PDFMarks file written.");



                // ########## GENERATE OUTPUT FILE PATH ##################################################################################

                string outfilePath = Path.Combine(Properties.Settings.Default.OutputFolderPath, InputFileName.Replace(".ps", ".pdf.tmp"));
                logInfo(string.Format("Writing to: {0}", outfilePath));
                


                // ########## START GHOSTSCRIPT ##########################################################################################

                Process gsProcess = new Process();

                gsProcess.StartInfo.CreateNoWindow = true;
                gsProcess.StartInfo.UseShellExecute = false;
                gsProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                gsProcess.StartInfo.FileName = string.Format("gswin{0}.exe", (IntPtr.Size == 8 ? "64" : "32")); // choose 64bit exe if we're running as a 64bit process.
                gsProcess.StartInfo.Arguments = string.Format(" -sDEVICE=\"pdfwrite\" -q -dQUIET -dNOPAUSE -dSAFER -dBATCH -sOUTPUTFILE=\"{0}\" {1}",
                    outfilePath,
                    string.Join(" ", filesToConcat.Select(s => "\"" + s + "\"")));
                
                gsProcess.StartInfo.RedirectStandardOutput = true;
                gsProcess.OutputDataReceived += (sender, args) => 
                {
                    if(!string.IsNullOrWhiteSpace(args.Data))
                        logWarning(string.Format("GS: {0}", args.Data));
                };
                
                gsProcess.Start();
                gsProcess.BeginOutputReadLine();
                gsProcess.WaitForExit();

                if (gsProcess.ExitCode != 0)
                    logWarning(string.Format("GS: Exit Code {0}.", gsProcess.ExitCode));
                else
                    logInfo("GS exited.");
    
                State = JobState.Successful;

                
                
                // ########## RENAME OUTPUT FILE FROM TMP TO PDF #########################################################################

                string finalOutputPath = outfilePath.Replace(".pdf.tmp", ".pdf");

                if(File.Exists(finalOutputPath))
                    File.Delete(finalOutputPath);

                File.Move(outfilePath, finalOutputPath);

                logInfo("Output file renamed.");
                


                // ########## REMOVIE INPUT FILE FROM WORK DIR ###########################################################################

                File.Delete(workingFilePath);
                File.Delete(pdfmarksFilePath);

                if (!Properties.Settings.Default.KeepSourceFiles)
                {
                    logInfo("Source files deleted.");
                    foreach (string file in filesToConcat)
                        File.Delete(file);
                }

                logInfo("Temp files deleted. Job succeeded.");



                // ########## WRITE JOB LOG IF CONFIGURED ################################################################################

                if (Properties.Settings.Default.WriteJobLogFiles)
                {
                    File.WriteAllLines(
                        Path.Combine(Properties.Settings.Default.OutputFolderPath, InputFileName.Replace(".ps", "_log.txt")), 
                        JobLog.Select(kvp => kvp.Key).ToArray());
                }

            }
            catch (Exception e)
            {
                State = JobState.Failed;
                logError("Exception while processing job.", e);
            }
            finally
            {
                if (JobEnd != null)
                    JobEnd.Invoke(State);
            }
        }

        private static bool isFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        private void logInfo(string logString)
        {
            JobLog.Add(new KeyValuePair<string,DistillingServiceControlConstants.LogSeverity>(string.Format("{0}\t{1}", DateTime.Now, logString), DistillingServiceControlConstants.LogSeverity.Info));
            log.Info(logString);
        }

        private void logWarning(string logString)
        {
            JobLog.Add(new KeyValuePair<string, DistillingServiceControlConstants.LogSeverity>(string.Format("{0}\t{1}", DateTime.Now, logString), DistillingServiceControlConstants.LogSeverity.Warning));
            log.Warn(logString);
        }

        private void logError(string logString, Exception ex)
        {
            JobLog.Add(new KeyValuePair<string, DistillingServiceControlConstants.LogSeverity>(string.Format("{0}\t{1} - {2}", DateTime.Now, logString, ex.Message), DistillingServiceControlConstants.LogSeverity.Error));
            log.Error(logString);
        }
    }
}
