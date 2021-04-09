using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using RedZoneDevelopment.MongoAutoUpdater.Helper;
using RedZoneDevelopment.MongoAutoUpdater.Interface;

namespace RedZoneDevelopment.MongoAutoUpdater
{
    /// <summary>
    /// Handles the database data initialization.
    /// </summary>
    public class Initializer
    {
        #region // private members
        private readonly ILogger _logger;
        private readonly DatabaseHandler _databaseHandler;
        #endregion

        #region // constructor
        /// <summary>
        /// Creates a instance by Mongo data context.
        /// </summary>
        /// <param name="databaseContext">Reference to the mongo database instance.</param>
        /// <param name="modelContainer">Assembly which contains the mongo data models.</param>
        /// <param name="logger">Reference to the logger instance</param>
        public Initializer(IMongoDatabase databaseContext, Assembly modelContainer, ILogger logger) : this(databaseContext, modelContainer, logger, null)
        {}

        /// <summary>
        /// Creates a instance by Mongo data context and update operation handler.
        /// </summary>
        /// <param name="databaseContext">Reference to the mongo data context which contains the MongoDatabase property.</param>
        /// <param name="modelContainer">Assembly which contains the mongo data models.</param>
        /// <param name="logger">Reference to the logger instance</param>
        /// <param name="updateOperationHandler">Custom update operation handler</param>
        public Initializer(IMongoDatabase databaseContext, Assembly modelContainer, ILogger logger, ICustomUpdateOperation updateOperationHandler)
        {
            _logger = logger;
            if (updateOperationHandler == null)
                updateOperationHandler = new DefaultUpdateOperation();
            _databaseHandler = new DatabaseHandler(databaseContext, modelContainer, updateOperationHandler, logger);
        }
        #endregion

        #region // public methods
        /// <summary>
        /// Starts the database initialization operations.
        /// </summary>
        /// <param name="pathToConfigFile">Path to the json configuration file</param>
        public async Task RunAsync(string pathToConfigFile)
        {
            if (string.IsNullOrEmpty(pathToConfigFile))
            {
                _logger.LogError("Path to json configuration file is missing.");
                throw new ArgumentException("Value cannot be empty.", nameof(pathToConfigFile));
            }
            if (!File.Exists(pathToConfigFile)) 
            {
                _logger.LogError("Path to json configuration file was not found.");
                throw new ArgumentException("File not found.", nameof(pathToConfigFile));
            }

            _logger.LogDebug("Path to config file " + pathToConfigFile);

            string content = File.ReadAllText(pathToConfigFile);
            _logger.LogDebug("Json configuration file was read.");

            var data = JObject.Parse(content);
            _logger.LogDebug("Json configuration files was parsed to JObject.");

            await RunAsync(data);
        }

        /// <summary>
        /// Starts the database initialization operations.
        /// </summary>
        /// <param name="configuration">Json configuration of the database elements</param>
        public async Task RunAsync(JObject configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (!ValidateConfiguration(configuration))
            {
                _logger.LogError("Json configuration is not valid.");
                throw new ApplicationException("Configuration is not valid. Please check if all required configuration nodes exists.");
            }
            _logger.LogDebug("Json configuration validation was succesfully passed.");

            await _databaseHandler.RunAsync(configuration);
        }
        #endregion

        #region // private methods
        /// <summary>
        /// Validates the json configration.
        /// </summary>
        /// <param name="configuration">Json configuration content</param>
        /// <returns>Returns true if the configuration is valid.</returns>
        private bool ValidateConfiguration(JObject configuration)
        {
            if (string.IsNullOrEmpty(JsonOperations.TryGetValue<string>(configuration, "version")))
            {
                _logger.LogError("version element is missing at json configuration.");
                return false;
            }

            if(JsonOperations.TryGetValue<object>(configuration, "data") == null)
            {
                _logger.LogError("data element is missing at json configuration.");
                return false;
            }
                         
            foreach(var element in configuration["data"])
            {
                if(string.IsNullOrEmpty(JsonOperations.TryGetValue<string>(element, "collectionName")))
                {
                    _logger.LogError("collectionName element is missing at json configuration.");
                    return false;
                }
                if (string.IsNullOrEmpty(JsonOperations.TryGetValue<string>(element, "typeName")))
                {
                    _logger.LogError("typeName element is missing at json configuration.");
                    return false;
                }
                if (JsonOperations.TryGetValues<string>(element, "keys") == null)
                {
                    _logger.LogError("keys element is missing at json configuration.");
                    return false;
                }
                if (JsonOperations.TryGetValues<string>(element, "items") == null)
                {
                    _logger.LogError("items element is missing at json configuration.");
                    return false;
                }
            }

            return true;
        }
        #endregion
    }
}
