using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PS2PDF.WindowsService
{
    public partial class PS2PDF : ServiceBase
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(PS2PDF));

        private DistillingService service;

        public PS2PDF()
        {
            log4net.Config.XmlConfigurator.Configure();

            try
            {
                service = new DistillingService();
                DistillingService.RunMode = DistillingService.RunModes.WinService;

                InitializeComponent();
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while initializing DistillingService WinService.", ex);
                Environment.Exit(1);
            }
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                service.Start();
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while starting DistillingService WinService.", ex);
                Environment.Exit(1);
            }
        }

        protected override void OnStop()
        {
            try
            {
                service.Stop();
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while stoppiung DistillingService WinService.", ex);
                Environment.Exit(1);
            }
        }
    }
}
