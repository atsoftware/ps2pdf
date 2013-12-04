using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace PS2PDF
{
    [ServiceContract(SessionMode=SessionMode.Required, CallbackContract=typeof(IDistillingServiceControlCallback))]
    public interface IDistillingServiceControl
    {
        [OperationContract]
        DistillingServiceConfiguration Register(string clientName);

        [OperationContract]
        void SetConfig(DistillingServiceConfiguration config);
    }

    public interface IDistillingServiceControlCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveLogLine(string logLine, DistillingServiceControlConstants.LogSeverity severity);

        [OperationContract(IsOneWay = true)]
        void ReceiveConfig(DistillingServiceConfiguration config);
    }

    [DataContract]
    public class DistillingServiceConfiguration
    {
        [DataMember]
        public string RunMode;

        [DataMember]
        public string InputDirectory;
        
        [DataMember]
        public string OutputDirectory;

        [DataMember]
        public bool WriteJobLogFiles;

        [DataMember]
        public bool KeepSourceFiles;
    }

    public static class DistillingServiceControlConstants
    {
        public const string NamedPipesUri = "net.pipe://localhost";
        public const string NamedPipesAddress = "ps2pdf";
        public const string NamedPipesUriAndAddress = "net.pipe://localhost/ps2pdf";

        public enum LogSeverity
        {
            Info,
            Warning,
            Error
        }
    }
}
