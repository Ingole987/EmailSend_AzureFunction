using System;
using System.Collections.Generic;
using System.Text;

namespace EmailSend_FunctionApp
{
    class EmailModel
    {

        public string title { get; set; }
        public string message { get; set; }
        public string pipilineName { get; set; }
        public DateTime date { get; set; }

        public string color { get; set; }
        public string addressTo { get; set; }
        public string addressCC { get; set; }
        public string ClientName { get; set; }
        public string SourceName { get; set; }
        public string SynapseTriggerId  { get; set; }
        public string Status { get; set; }
        public string LastActivityOn { get; set; }

    }

    class Stats
    {
        public string TotalRecords { get; set; }
        public string TotalDiscarded { get; set; }
        public string TotalRecordsForIngestion { get; set; }
        public string Ingested { get; set; }
        public string IngestionFailed { get; set; }
        public string IngestionPending { get; set; }
        public string AudioDurationDiscard { get; set; }
        public string FilterDiscard { get; set; }
        public string InvalidClientCaptureDate { get; set; }
        public string InvalidClientID { get; set; }
        public string InvalidOutputAudioFileName { get; set; }

        public string SynapseTriggerRunID { get; set; }

    }

    class RequestModel
    {
        public string title { get; set; }
        public string message { get; set; }
        public string pipilineName { get; set; }
        public string color { get; set; }
        public string addressTo { get; set; }
        public string addressCC { get; set; }
        public string LogicApp_Url { get; set; }
        public string DBConnection { get; set; }
        public string Time_Interval { get; set; }
        public string Time_Threshold { get; set; }
    }
}

    
    