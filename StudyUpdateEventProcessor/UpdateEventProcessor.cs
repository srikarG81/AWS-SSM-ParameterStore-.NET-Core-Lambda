using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using AWSXRay.SQS.Publisher.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace StudyUpdateEventProcessor

{
    public class UpdateEventProcessor
    {
        private ILogger _logger;
        private IAmazonSQS SqsClient;
        public IConfigurationService ConfigService;
        private IConfiguration configuration;
        public UpdateEventProcessor()
        {
            Amazon.XRay.Recorder.Handlers.AwsSdk.AWSSDKHandler.RegisterXRayForAllServices();
            AWSSDKSQSHandler.RegisterXRayForSQS();
            var configServiceCollection = new ServiceCollection();
            ConfigureServices(configServiceCollection);
            var serviceProvider = configServiceCollection.BuildServiceProvider();
            // Get Configuration Service from DI system
            ConfigService = serviceProvider.GetService<IConfigurationService>();
            configuration = ConfigService?.GetConfiguration();
            SqsClient = new AmazonSQSClient();
            var loggerFactory=  serviceProvider.GetService<ILoggerFactory>();
            Configure(loggerFactory, configuration);
            _logger= loggerFactory.CreateLogger(String.Empty);
        }

        public void Configure(ILoggerFactory loggerFactory, IConfiguration configuration)
        {

            var loggerOptions = new LambdaLoggerOptions(configuration);
            // Configure Lambda logging
            loggerFactory
                .AddLambdaLogger(loggerOptions);
        }
        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task UpdateEventProcessorHandler(CloudWatchEvent<UpdateEvent> updateEvent, ILambdaContext context)
        {

            using (_logger.BeginScope("TraceId {0}", Environment.GetEnvironmentVariable("_X_AMZN_TRACE_ID")))
            {
                if (Environment.GetEnvironmentVariable("reload")?.ToString() == "true" || configuration.GetValue<bool>("Refresh"))
                {
                    IConfigurationRoot root = (IConfigurationRoot)configuration;
                    root.Reload();
                }

                _logger.Log(LogLevel.Information, configuration.GetValue<string>("SecureKeybyCMK"));
                _logger.Log(LogLevel.Information, JsonSerializer.Serialize(updateEvent));
                await ProcessUpdateEvent(updateEvent, _logger);
            }
        }

        private async Task ProcessUpdateEvent(CloudWatchEvent<UpdateEvent> updateEvent, ILogger logger)
        {
            var message = new
            {
                DICOMStudyId = updateEvent.Detail.Prop1,
                DatastoreId = updateEvent.Detail.Prop2,
                Operation = updateEvent.Detail.Prop3,
            };
            logger.Log(LogLevel.Information, $"Received Study update event DICOMStudyId:{message.DICOMStudyId}, DICOMStudyId:{message.DICOMStudyId}");
            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = Environment.GetEnvironmentVariable("StudyUpdateFIFOQueue"),
                MessageBody = JsonSerializer.Serialize(message)
            };
            sendMessageRequest.MessageGroupId = $"{message.DICOMStudyId}_{message.DICOMStudyId}";
            sendMessageRequest.MessageDeduplicationId = Guid.NewGuid().ToString();
            var sendMessageResponse = await SqsClient.SendMessageAsync(sendMessageRequest);
            if (sendMessageResponse.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Fail to process the record" + JsonSerializer.Serialize(sendMessageResponse));
            }
        }

       
        private void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Register services with DI system
            serviceCollection.AddTransient<IConfigurationService, ConfigurationService>();
            serviceCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
        }
    }
}
