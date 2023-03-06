using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmailSend_FunctionApp
{
    class LogicApp
    {
        //public static async Task<RequestModel> GetMailParams(HttpRequest req)
        //{
        //    string requestBody = await req.ReadAsStringAsync();
        //    RequestModel requestModel = JsonConvert.DeserializeObject<RequestModel>(requestBody);
        //    return requestModel;
        //}


        public static async Task<RequestModel> GetMailParams(HttpRequest req, IConfiguration configuration)
        {
            string requestBody = await req.ReadAsStringAsync();
            RequestModel requestModel = JsonConvert.DeserializeObject<RequestModel>(requestBody);

            // Get default values from appsettings.json
            var defaultValues = configuration.GetSection("DefaultValues").Get<Dictionary<string, string>>();

            // Iterate over each property in RequestModel object
            foreach (var property in requestModel.GetType().GetProperties())
            {
                // Check if property value is null or empty
                if (string.IsNullOrEmpty(property.GetValue(requestModel)?.ToString()))
                {
                    // Set default value from appsettings.json
                    if (defaultValues.TryGetValue(property.Name, out string defaultValue))
                    {
                        property.SetValue(requestModel, defaultValue);
                    }
                }
            }

            return requestModel;
        }


        public static string PostMail(string json, ILogger log , RequestModel requestModel)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    //var url = "https://prod-02.eastus.logic.azure.com:443/workflows/2e6336d5b923410e87864c13a50c016d/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=JXiBWvS48SJO2pRWm1tYjC20pWXygpTmJCuGLAjj4t4";
                    var url = requestModel.LogicApp_Url;
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = client.PostAsync(url, content).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        log.LogInformation("Mail posted");
                    }
                    return "Success";
                }
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                Console.WriteLine(e.Message);
                return "Failed";
            }
        }

        public static async Task<List<EmailModel>> DataIngetion(ILogger log, RequestModel requestModel)
        {
            try
            {
                log.LogInformation("Data Ingetion started");
                //string sqlConnection = "Data Source=icapdissqlserver.database.windows.net;database=icapdisql_dev;uid=icapdis;password=Provana@123;";
                string sqlConnection = requestModel.DBConnection;

                using (SqlConnection con = new SqlConnection(sqlConnection))
                {
                    await con.OpenAsync();
                    SqlCommand cmd = new SqlCommand("select ClientName,SourceName, SynapseTriggerId,Status, LastActivityOn from  LivevoxDownloadCallAPIStatus where LastActivityOn > dateadd(minute, @Time_Interval, getdate()) and SynapseTriggerId like '%_Ingestion' and (Status like '%Completed' or datediff(hour,CreatedOn,Getdate()) > @Time_Threshold)", con);
                    cmd.CommandTimeout = 120;
                    SqlParameter parameter = new SqlParameter();
                    parameter.ParameterName = "@Time_Interval";
                    parameter.Value = Convert.ToInt32(requestModel.Time_Interval);
                    SqlParameter parameter2 = new SqlParameter();
                    parameter2.ParameterName = "@Time_Threshold";
                    parameter2.Value =requestModel.Time_Threshold;
                    cmd.Parameters.Add(parameter);
                    cmd.Parameters.Add(parameter2);
                    {
                        using (SqlDataReader dataReader = await cmd.ExecuteReaderAsync())
                        {
                            List<EmailModel> emailModels = new List<EmailModel>();
                            
                            while (dataReader.Read())
                            {
                                EmailModel emailModel = new EmailModel();
                                emailModel.title = requestModel.title;
                                emailModel.date = DateTime.Now;
                                emailModel.color = requestModel.color;
                                emailModel.addressTo = requestModel.addressTo;
                                //emailModel.addressCC = requestModel.addressCC;
                                emailModel.ClientName = dataReader["ClientName"].ToString();
                                emailModel.SourceName = dataReader["SourceName"].ToString();
                                emailModel.SynapseTriggerId = dataReader["SynapseTriggerId"].ToString();
                                emailModel.Status = dataReader["Status"].ToString();
                                emailModel.LastActivityOn = dataReader["LastActivityOn"].ToString();
                                emailModel.pipilineName = requestModel.pipilineName + " " +emailModel.ClientName + " (" + emailModel.SourceName + ")";

                                Stats newSynapseTriggerIdList = ParseStats(emailModel.SynapseTriggerId ,log);

                                Stats rep = await DataIngetion2(emailModel.ClientName, emailModel.SourceName ,log , requestModel);

                                string diffTotalRecords = (decimal.Parse(rep.TotalRecords) - decimal.Parse(newSynapseTriggerIdList.TotalRecords)).ToString();
                                string diffTotalDiscarded = (decimal.Parse(rep.TotalDiscarded) - decimal.Parse(newSynapseTriggerIdList.TotalDiscarded)).ToString();
                                string diffTotalRecordsForIngestion = (decimal.Parse(rep.TotalRecordsForIngestion) - decimal.Parse(newSynapseTriggerIdList.TotalRecordsForIngestion)).ToString();
                                string diffIngested = (decimal.Parse(rep.Ingested) - decimal.Parse(newSynapseTriggerIdList.Ingested)).ToString();
                                string diffIngestionFailed = (decimal.Parse(rep.IngestionFailed) - decimal.Parse(newSynapseTriggerIdList.IngestionFailed)).ToString();
                                string diffIngestionPending = (decimal.Parse(rep.IngestionPending) - decimal.Parse(newSynapseTriggerIdList.IngestionPending)).ToString();
                                string diffAudioDurationDiscard = (decimal.Parse(rep.AudioDurationDiscard) - decimal.Parse(newSynapseTriggerIdList.AudioDurationDiscard)).ToString();
                                string diffFilterDiscard = (decimal.Parse(rep.FilterDiscard) - decimal.Parse(newSynapseTriggerIdList.FilterDiscard)).ToString();
                                string diffInvalidClientCaptureDate = (decimal.Parse(rep.InvalidClientCaptureDate) - decimal.Parse(newSynapseTriggerIdList.InvalidClientCaptureDate)).ToString();
                                string diffInvalidClientID = (decimal.Parse(rep.InvalidClientID) - decimal.Parse(newSynapseTriggerIdList.InvalidClientID)).ToString();
                                string diffInvalidOutputAudioFileName = (decimal.Parse(rep.InvalidOutputAudioFileName) - decimal.Parse(newSynapseTriggerIdList.InvalidOutputAudioFileName)).ToString();


                                emailModel.message = "<!DOCTYPE html><html>" +
                                    $"<br/>Summary after pipeline run (runId:{newSynapseTriggerIdList.SynapseTriggerRunID})" +
                                    "<table width=\"1000\" border=\"1\" cellspacing=\"0\" cellpadding=\"0\" style=\"border:1px solid #ccc;\">" +
                                    "<tr align=\"center\"> " +
                                    "<th>Total Records</th>" +
                                    "<th>Total Discarded </th> " +
                                    "<th>Total Records for Ingestion</th> " +
                                    "<th>Ingested</th> " +
                                    "<th>Ingestion Failed</th>" +
                                    "<th>Ingestion Pending</th>" +
                                    "<th>Audio Duration Discard</th>" +
                                    "<th>Filter Discard</th>" +
                                    "<th>Records with <br/> Invalid ClientCaptureDate</th>" +
                                    "<th>Records with <br/> Invalid ClientID</th>" +
                                    "<th>Records with <br/> Invalid OutputAudioFileName</th>" +
                                    "</tr>" +
                                    "<tr align=\"center\">" +
                                    $"<td style=\"text-align: center\">{diffTotalRecords}</td>" +
                                    $"<td style=\"text-align: center\">{diffTotalDiscarded}</td>" +
                                    $"<td style=\"text-align: center\">{diffTotalRecordsForIngestion}</td>" +
                                    $"<td style=\"text-align: center\">{diffIngested}</td>" +
                                    $"<td style=\"text-align: center\">{diffIngestionFailed}</td>" +
                                    $"<td style=\"text-align: center\">{diffIngestionPending}</td>" +
                                    $"<td style=\"text-align: center\">{diffAudioDurationDiscard}</td>" +
                                    $"<td style=\"text-align: center\">{diffFilterDiscard}</td>" +
                                    $"<td style=\"text-align: center\">{diffInvalidClientCaptureDate}</td> " +
                                    $"<td style=\"text-align: center\">{diffInvalidClientID}</td> " +
                                    $"<td style=\"text-align: center\">{diffInvalidOutputAudioFileName}</td>" +
                                    "</tr> " +
                                    "</table></html>";

                                emailModels.Add(emailModel);
                            }
                            log.LogInformation("Data Ingetion Completed");
                            return emailModels;
                        }
                    }

                }

            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);

                throw;
            }
        }

        public static Stats ParseStats(string status, ILogger log)
        {
            try
            {
                string data = "";
                string data2 = "";
                Match stats = Regex.Match(status, @"\((.*?)\)_([^_]+)");
                Stats stats1 = new Stats();
                if (stats.Success)
                {
                    data = stats.Groups[1].Value;
                    data2 = stats.Groups[2].Value;
                    string[] values = data.Split('_');
                    string[] values2 = data2.Split('_');

                    stats1.TotalRecords = values[0];
                    stats1.TotalDiscarded = values[1];
                    stats1.TotalRecordsForIngestion = values[2];
                    stats1.Ingested = values[3];
                    stats1.IngestionFailed = values[4];
                    stats1.IngestionPending = values[5];
                    stats1.AudioDurationDiscard = values[6];
                    stats1.FilterDiscard = values[7];
                    stats1.InvalidClientCaptureDate = values[8];
                    stats1.InvalidClientID = values[9];
                    stats1.InvalidOutputAudioFileName = values[10];
                    stats1.SynapseTriggerRunID = values2[0];
                }
                return stats1;
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                throw;
            }
        }



        public static async Task<Stats> DataIngetion2(string ClientName, string SourceName, ILogger log , RequestModel requestModel)
        {
            try
            {
                log.LogInformation("DataIngetion2 Started");
                //string sqlConnection = "Data Source=icapdissqlserver.database.windows.net;database=icapdisql_dev;uid=icapdis;password=Provana@123;";
                string sqlConnection = requestModel.DBConnection;

                using (SqlConnection con = new SqlConnection(sqlConnection))
                {
                    await con.OpenAsync();
                    SqlCommand cmd = new SqlCommand(@" SELECT COUNT(*) TotalRecords,
                                                    COUNT (CASE WHEN Status like '%Discard%'  THEN Status END ) 
                                                    TotalDiscarded,
                                                    COUNT (CASE WHEN MergeStatus != 'Discard'  and MergeStatus != 'FilterDiscard' THEN MergeStatus   END  ) 
                                                    TotalRecordsForIngestion,
                                                    COUNT(CASE WHEN Status = 'Posted'  THEN Status END ) 
                                                    Ingested, 
                                                    COUNT (CASE  WHEN Status = 'Failed' AND ISNULL(PostReTryCount, 0) >= 10  THEN Status  END ) 
                                                    IngestionFailed, 
                                                    COUNT (CASE WHEN Status ='Pending' OR (Status = 'Failed' AND ISNULL(PostReTryCount, 0) < 10) THEN Status  END ) 
                                                    IngestionPending, 
                                                    COUNT (CASE WHEN Status ='Discard' THEN Status END ) 
                                                    AudioDurationDiscard,
                                                    COUNT (CASE WHEN Status ='FilterDiscard' THEN Status END ) 
                                                    FilterDiscard,
                                                    COUNT (CASE  WHEN ClientCaptureDate = NULL OR ClientCaptureDate = '' THEN ClientCaptureDate END ) 
                                                    InvalidClientCaptureDate,
                                                    COUNT (CASE WHEN ClientID = NULL  OR  ClientID = '' THEN ClientID  END ) 
                                                    InvalidClientID,
                                                    COUNT (CASE WHEN OutputAudioFileName = NULL  OR  OutputAudioFileName = ''  OR OutputAudioFileName NOT LIKE '%.%' THEN OutputAudioFileName  END  ) 
                                                InvalidOutputAudioFileName FROM callmetadata WHERE ClientName = @ClientName AND ClientSource = @ClientSource", con);
                    cmd.CommandTimeout = 120;
                    SqlParameter parameter = new SqlParameter();
                    parameter.ParameterName = "@ClientName";
                    parameter.Value = ClientName;
                    SqlParameter parameter2 = new SqlParameter();
                    parameter2.ParameterName = "@ClientSource";
                    parameter2.Value = SourceName;
                    cmd.Parameters.Add(parameter);
                    cmd.Parameters.Add(parameter2);


                    SqlDataReader dataReader = await cmd.ExecuteReaderAsync();

                    Stats stat = new Stats();
                    while (dataReader.Read())
                    {
                        stat.TotalRecords = dataReader["TotalRecords"].ToString();
                        stat.TotalDiscarded = dataReader["TotalDiscarded"].ToString();
                        stat.TotalRecordsForIngestion = dataReader["TotalRecordsForIngestion"].ToString();
                        stat.Ingested = dataReader["Ingested"].ToString();
                        stat.IngestionFailed = dataReader["IngestionFailed"].ToString();
                        stat.IngestionPending = dataReader["IngestionPending"].ToString();
                        stat.AudioDurationDiscard = dataReader["AudioDurationDiscard"].ToString();
                        stat.FilterDiscard = dataReader["FilterDiscard"].ToString();
                        stat.InvalidClientCaptureDate = dataReader["InvalidClientCaptureDate"].ToString();
                        stat.InvalidClientID = dataReader["InvalidClientID"].ToString();
                        stat.InvalidOutputAudioFileName = dataReader["InvalidOutputAudioFileName"].ToString();
                    }
                    log.LogInformation("DataIngetion2 Complete");

                    return stat;
                }
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                throw;
            }
        }
    }
}
