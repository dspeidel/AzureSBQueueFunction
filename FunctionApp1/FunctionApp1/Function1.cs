using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FunctionApp1
{
    public static class Function1
    {
        [FunctionName("QueueAnItem")]
        public static async Task<IActionResult> WebRequest(
           [HttpTrigger(authLevel: Microsoft.Azure.WebJobs.Extensions.Http.AuthorizationLevel.Anonymous, methods: "post", Route = "queueRequest")] HttpRequestMessage req,
           [ServiceBus("a_devon", Connection = "serviceBusConnection")] IAsyncCollector<Message> queue, ExecutionContext executionContext)
        {
            var post = await req.Content.ReadAsAsync<QueueRequestMessage>().ConfigureAwait(false);
            var query = req.RequestUri.ParseQueryString();
            var howMany = 1;
            int.TryParse(query["howmany"], out howMany);
            try
            {
                string body = JsonConvert.SerializeObject(post, Formatting.Indented);
                for (int i = 1; i <= howMany; i++)
                {
                    await queue.AddAsync(new Message(Encoding.UTF8.GetBytes(body)));
                }
                
                
            }
            catch
            {
                return new BadRequestErrorMessageResult($"Unable to send request to queue.  InvocationId: {executionContext.InvocationId}");
            }

            return new AcceptedResult();
        }

        [FunctionName("ProcessQueuedItem")]
        public static async Task Run([ServiceBusTrigger("a_devon", Connection = "serviceBusConnection")] Message message, MessageReceiver messageReceiver, [ServiceBus("a_devon", Connection = "serviceBusConnection")] IAsyncCollector<Message> queue, ILogger logger)
        {
            var msg = JsonConvert.DeserializeObject<QueueRequestMessage>(Encoding.UTF8.GetString(message.Body));
            if (msg.CompleteBeforeProcessing)
            {
                logger.LogWarning("Completing message before processing");
                await messageReceiver.CompleteAsync(message.SystemProperties.LockToken);
            }

            try
            {
                logger.LogWarning($"Delay for {msg.DelaySeconds} seconds!");
                //simulate long running task
                await Task.Delay(TimeSpan.FromSeconds(msg.DelaySeconds));
                if (msg.ThrowError) throw new ApplicationException("Message said to throw an error");
                logger.LogWarning("Ok, I'm done");
            }
            catch
            {
                msg.ThrowError = false;
                string body = JsonConvert.SerializeObject(msg, Formatting.Indented);
                logger.LogWarning("Before requeue");
                await queue.AddAsync(new Message(Encoding.UTF8.GetBytes(body)));
                logger.LogWarning("After requeue");
                throw;
            }
            finally
            {
                if (!msg.CompleteBeforeProcessing)
                {
                    await messageReceiver.CompleteAsync(message.SystemProperties.LockToken);
                    logger.LogWarning("Completed message after processing or error.");
                }
            }
        }
    }

    public class QueueRequestMessage
    {
        public int DelaySeconds { get; set; }
        public bool ThrowError { get; set; }
        public bool CompleteBeforeProcessing { get; set; }
    }
}
