using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace PS2PDF
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.PerCall)]
    public class DistillingServiceControl : IDistillingServiceControl
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(DistillingServiceControl));

        private static Dictionary<IDistillingServiceControlCallback, string> callbackList = new Dictionary<IDistillingServiceControlCallback, string>();

        public DistillingServiceControl()
        {
        }

        public DistillingServiceConfiguration Register(string clientName)
        {
            IDistillingServiceControlCallback callbackChannel = OperationContext.Current.GetCallbackChannel<IDistillingServiceControlCallback>();
            if (!callbackList.ContainsKey(callbackChannel))
                callbackList.Add(callbackChannel, clientName);

            log.Debug(string.Format("{0} connected.", clientName));

            return DistillingService.GetCurrentConfig();
        }

        public void SetConfig(DistillingServiceConfiguration config)
        {
            Properties.Settings.Default.InputFolderPath = config.InputDirectory;
            Properties.Settings.Default.OutputFolderPath = config.OutputDirectory;
            Properties.Settings.Default.WriteJobLogFiles = config.WriteJobLogFiles;
            Properties.Settings.Default.KeepSourceFiles = config.KeepSourceFiles;
            Properties.Settings.Default.Save();
            
            IDistillingServiceControlCallback callbackChannel = OperationContext.Current.GetCallbackChannel<IDistillingServiceControlCallback>();
            string callerName = callbackList[callbackChannel];

            log.Info(string.Format("SetConfig called: {0}.", callerName));

            new System.Threading.Thread(() =>
            {
                BroadcastConfig();
                BroadcastLogLine(string.Format("Konfiguration aktualisiert von {0}.\n - Eingangsverzeichnis: {1}\n - Ausgangsverzeichnis: {2}\n - Job Protokollierung: {3}\n - Quelldateien behalten: {4}",
                    callerName, config.InputDirectory, config.OutputDirectory, config.WriteJobLogFiles ? "aktiviert" : "deaktiviert.", config.KeepSourceFiles ? "aktiviert" : "deaktiviert."), DistillingServiceControlConstants.LogSeverity.Warning);
            }).Start();
        }

        internal static void BroadcastLogLine(string logLine, DistillingServiceControlConstants.LogSeverity severity)
        {
            foreach (IDistillingServiceControlCallback callback in callbackList.Keys.ToList()) // clone list to remove inactive clients
            {
                try
                {
                    callback.ReceiveLogLine(logLine, severity);
                }
                catch (CommunicationObjectAbortedException)
                {
                    try
                    {
                        if (callbackList.ContainsKey(callback))
                            callbackList.Remove(callback);

                        log.Info(string.Format("Client {0} disconnected. Removed from client list.", callbackList[callback]));
                    }
                    catch (Exception)
                    { /* maybe another thread removed it already, don't care! */ }
                }
            }
        }

        internal static void BroadcastConfig()
        {
            log.Debug(string.Format("Broadcasting configuration to {0} recipients.", callbackList.Count));

            foreach (IDistillingServiceControlCallback callback in callbackList.Keys.ToList()) // clone list to remove inactive clients
            {
                try
                {
                    callback.ReceiveConfig(DistillingService.GetCurrentConfig());
                }
                catch (CommunicationObjectAbortedException)
                {
                    try
                    {
                        if(callbackList.ContainsKey(callback))
                            callbackList.Remove(callback);

                        log.Info(string.Format("Client {0} disconnected. Removed from client list.", callbackList[callback]));
                    }
                    catch (Exception)
                    { /* maybe another thread removed it already, don't care! */ }
                }
            }
        }

        internal static void DisconnectClients()
        {
            foreach (IDistillingServiceControlCallback callback in callbackList.Keys.ToList()) // clone list to remove inactive clients
            {
                try
                {
                    ICommunicationObject com = (ICommunicationObject)callback;
                    com.Close();
                }
                catch (CommunicationObjectAbortedException)
                {
                    // we wanted to close it anyway!
                }

                callbackList.Clear();
            }
        }
    }
}
