using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PS2PDF
{
    class Program
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            try
            {
                DistillingService.RunMode = DistillingService.RunModes.ConsoleApp;
                DistillingService service = new DistillingService();
                service.Start();

                Console.ReadLine();
                service.Stop();
            }
            catch (Exception ex)
            {
                log.Fatal("Exception while running DistillingService.ConsoleApp.", ex);
                Environment.Exit(1);
            }
        }
    }
}
