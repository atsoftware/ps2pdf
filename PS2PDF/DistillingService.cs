using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.ServiceModel;
using System.Threading;

namespace PS2PDF
{
    public class DistillingService
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(DistillingService));

        private FileSystemWatcher fsWatcher;
        private ServiceHost serviceHost;

        internal volatile List<ProcessJob> openJobs;

        public static RunModes RunMode = RunModes.WinService;
        public enum RunModes
        {
            ConsoleApp,
            WinService
        }

        #region Config Accessors
        private string inputFolderPath
        {
            get { return Properties.Settings.Default.InputFolderPath; }
        }

        private string inputFileFilter
        {
            get { return Properties.Settings.Default.InputFileFilter; }
        }
        
        private string outputFolderPath
        {
            get { return Properties.Settings.Default.OutputFolderPath; }
        }

        private int numWorkerThreads
        {
            get { return Properties.Settings.Default.NumWorkerThreads; }
        }
        #endregion

        public static DistillingServiceConfiguration GetCurrentConfig()
        {
            DistillingServiceConfiguration config = new DistillingServiceConfiguration();

            config.RunMode = DistillingService.RunMode.ToString();
            config.InputDirectory = Properties.Settings.Default.InputFolderPath;
            config.OutputDirectory = Properties.Settings.Default.OutputFolderPath;
            config.WriteJobLogFiles = Properties.Settings.Default.WriteJobLogFiles;
            config.KeepSourceFiles = Properties.Settings.Default.KeepSourceFiles;

            return config;
        }

        public DistillingService()
        {
            try
            {
                ThreadPool.SetMaxThreads(numWorkerThreads, numWorkerThreads);
                openJobs = new List<ProcessJob>();
                log.Info(string.Format("Distilling service initialized. Run mode: {0}. Max thread count: {1}.", RunMode, numWorkerThreads));
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while initializing DistillingService.", ex);
                throw ex;
            }
        }

        public void Start()
        {
            try
            {
                serviceHost = new ServiceHost(typeof(DistillingServiceControl), new Uri(DistillingServiceControlConstants.NamedPipesUri));
                serviceHost.AddServiceEndpoint(typeof(IDistillingServiceControl), new NetNamedPipeBinding(), DistillingServiceControlConstants.NamedPipesAddress);
                serviceHost.Open();
                log.Info(string.Format("ServiceControlHost listening on: {0}", DistillingServiceControlConstants.NamedPipesUriAndAddress));

                fsWatcher = new FileSystemWatcher(inputFolderPath);
                fsWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                fsWatcher.Created += (sender, eventargs) =>
                {
                    if(!eventargs.Name.ToLower().EndsWith(inputFileFilter.Replace("*", "").ToLower()))
                        return;

                    ProcessJob job = new ProcessJob(eventargs.Name, eventargs.FullPath);
                    openJobs.Add(job);
                
                    job.JobLog.CollectionChanged += (s, e) => { jobLogged(job, e); };
                    job.JobEnd += (state) => { openJobs.Remove(job); };
                    ThreadPool.QueueUserWorkItem(job.ProcessFile);
                };
                fsWatcher.EnableRaisingEvents = true;
                log.Info(string.Format("Distilling service started. Input directory: {0}", inputFolderPath));
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while starting DistillingService.", ex);
                throw ex;
            }
        }

        public void Stop()
        {
            try
            {
                log.Info("Stopping distilling service...");

                DistillingServiceControl.DisconnectClients();

                serviceHost.Close();
                serviceHost = null;

                fsWatcher.Dispose();
                fsWatcher = null;

                log.Info("Distilling service stopped.");
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while stopping DistillingService.", ex);
                throw ex;
            }
        }

        private void jobLogged(ProcessJob senderJob, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems.Count > 0)
            {
                foreach (var item in e.NewItems)
                {
                    var pair = (KeyValuePair<string, DistillingServiceControlConstants.LogSeverity>) item;
                    string log = string.Format("{0}\t{1}", senderJob.InputFileName, pair.Key);
                    DistillingServiceControl.BroadcastLogLine(log, pair.Value);
                }
            }
        }
    }
}
