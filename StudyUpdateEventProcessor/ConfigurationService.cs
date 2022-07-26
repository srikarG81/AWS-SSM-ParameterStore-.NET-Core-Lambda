using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;

namespace StudyUpdateEventProcessor
{
    public class ConfigurationService : IConfigurationService
    {
        /// <inheritdoc />
        public IConfiguration GetConfiguration()
        {
            return new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddSystemsManager($"/curie/Dev", new AWSOptions() { Region = RegionEndpoint.APSouth1 }, false, TimeSpan.FromMinutes(10))
                .AddSystemsManager($"/curie/Config", new AWSOptions() { Region = RegionEndpoint.APSouth1 }, false, TimeSpan.FromSeconds(2))
                .AddEnvironmentVariables().Build();
        }
    }


    public interface IConfigurationService
    {
        /// <summary>
        /// Get application configuration properties (appsettings, environment variables)
        /// </summary>
        IConfiguration GetConfiguration();
    }
}
