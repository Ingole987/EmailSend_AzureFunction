using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;

namespace EmailSend_FunctionApp
{
    public static class SendEmail_LoicApp
    {
        [FunctionName("EmailSend_Function")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "EmailSend/post_method")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            try
            {
                log.LogInformation("Function App Started");

                //var configuration = new ConfigurationBuilder()
                //            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                //            .AddEnvironmentVariables()
                //            .Build();

                var configBuilder = new ConfigurationBuilder()
                                    .SetBasePath(context.FunctionAppDirectory)
                                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                                    .AddEnvironmentVariables();
                IConfigurationRoot configuration = configBuilder.Build();

                RequestModel requestModel = await LogicApp.GetMailParams(req , configuration);

                string response = "";

                List<EmailModel> requestList = await LogicApp.DataIngetion(log, requestModel);

                log.LogInformation("Email list Acquired");

                foreach (var requestData in requestList)
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

                    response = LogicApp.PostMail(json , log , requestModel);

                }
                return new OkObjectResult(response);
            }
            catch (Exception e)
            {
                var error = e.Message;

                if (error == null)
                {
                    log.LogInformation("StatusCode:401");
                    return new StatusCodeResult(401);
                }
                log.LogInformation(error);
                return new BadRequestObjectResult(error);
            }
            finally
            {
                log.LogInformation("Function App Closed");
            }
        }
    }
}
