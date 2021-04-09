using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using RedZoneDevelopment.MongoAutoUpdater.Data;
using RedZoneDevelopment.MongoAutoUpdater.Helper;
using RedZoneDevelopment.MongoAutoUpdater.Interface;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace RedZoneDevelopment.MongoAutoUpdater
{
    /// <summary>
    /// Handles the database operations.
    /// </summary>
    internal class DatabaseHandler
    {
        #region // private members
        private readonly ILogger _logger;
        private readonly ICustomUpdateOperation _updateOperationHandler;
        private readonly IMongoDatabase _databaseContext;
        private readonly Assembly _dataModelContainer;
        #endregion

        #region // Constructor
        /// <summary>
        /// Creates a database handler instance by data context reference.
        /// </summary>
        /// <param name="databaseContext">Reference to Mongo database context.</param>
        /// <param name="modelContainer">Assembly which contains the mongo data models.</param>
        /// <param name="updateOperationHandler">Custom update operation defintion</param>
        /// <param name="logger">Reference to logger instance</param>
        public DatabaseHandler(IMongoDatabase databaseContext, Assembly modelContainer, ICustomUpdateOperation updateOperationHandler, ILogger logger)
        {
            _logger = logger;
            _databaseContext = databaseContext;
            _updateOperationHandler = updateOperationHandler;
            _dataModelContainer = modelContainer;
        }
        #endregion

        #region // public methods
        /// <summary>
        /// Starts the database update process.
        /// </summary>
        /// <param name="data">Json configuration of item data</param>
        /// <returns>Returns the handling task.</returns>
        /// <remarks>Update process will be only started if the version of the json configuration is newer than the version which is stored in database.</remarks>
        public async Task RunAsync(JObject data)
        {
            string versionValue = JsonOperations.TryGetValue<string>(data, "version");
            _logger.LogDebug("Config version: " + versionValue);

            string versionCollectionName = JsonOperations.TryGetValue<string>(data, "customVersionCollectionName");
            _logger.LogDebug("Config version collection nane: " + versionCollectionName);            

            // sets default version collection name if not exists
            if (string.IsNullOrEmpty(versionCollectionName))
            {                
                versionCollectionName = "DbInitConfig";
                _logger.LogInformation("Version collection name was not set. Default name will be used. -> " + versionCollectionName);
            }

            // Checks if configuration version is newer than existing version
            if (await IsNewerConfigVersionAsync(versionValue, versionCollectionName))
            {
                var collectionConfigurations = data["data"];

                foreach (var config in collectionConfigurations)
                {
                    var instance = CreateOperationInstance(config);
                    await UpdateCollectionAsync(instance);
                }
                // Updates version at database
                await UpdateConfigVersionAsync(versionValue, versionCollectionName);
                _logger.LogDebug("Version update at datanase completed.");
            }
            else
            {
                _logger.LogInformation("No newer version found. -> " + versionValue);
            }
        }
        #endregion

        #region // private methods
        /// <summary>
        /// Starts the update progress
        /// </summary>
        /// <typeparam name="T">Type of data collection base element</typeparam>
        /// <param name="instance">Reference to GenericCollectionOperator</param>
        private async Task UpdateCollectionAsync<T>(GenericCollectionOperator<T> instance)
        {
            await instance.CreateOrUpdateAsync();
        }

        /// <summary>
        /// Creates a new generic instance of the collection operation by type.
        /// </summary>
        /// <param name="config">Json configuration of current collection</param>
        /// <returns>Returns a instance of the GenericCollectionOperator which fits the configuration type.</returns>
        private dynamic CreateOperationInstance(JToken config)
        {
            string dbCollectionName = JsonOperations.TryGetValue<string>(config, "collectionName");
            _logger.LogDebug("Name of collection: " + dbCollectionName);

            string typeName = JsonOperations.TryGetValue<string>(config, "typeName");
            _logger.LogDebug("Name of type: " + typeName);

            Type genericeType = GetTypeByName(typeName);
            
            _logger.LogDebug("Collection at data context found.");
            var operationType = typeof(GenericCollectionOperator<>);
            var genericInstanceHandler = operationType.MakeGenericType(genericeType);
            dynamic instance = Activator.CreateInstance(genericInstanceHandler, new object[] { GetCollectionByTypeInstance(genericeType, dbCollectionName), config, _updateOperationHandler, _logger });            

            _logger.LogDebug("Collection operator created for collection " + dbCollectionName);

            return instance;
        }

        /// <summary>
        /// Gets a type by name.
        /// </summary>
        /// <param name="name">name of the type</param>
        /// <returns>Returns the requested type.</returns>
        private Type GetTypeByName(string name)
        {
            _logger.LogDebug("Get type by name " + name);

            var modelType = _dataModelContainer.GetType(name);

            if(modelType == null)
            {
                _logger.LogError($"Model type {name} was not found at assigned assembly {_dataModelContainer.FullName}.");
                throw new ApplicationException($"Type name {name} was not found at assingned models assembly {_dataModelContainer.FullName}.");
            }

            _logger.LogDebug("Property type " + modelType.ToString());

            return modelType;
        }

        /// <summary>
        /// Generates a reference to a generic mongo collection.
        /// </summary>
        /// <param name="typeOfModel">Type of collection</param>
        /// <param name="collectionName">Name of database collection</param>
        /// <returns>Returns a instance of the requested database collection</returns>
        private object GetCollectionByTypeInstance(Type typeOfModel, string collectionName)
        {
            _logger.LogDebug($"Get generic data collection by name {collectionName} and type {typeOfModel}.");
            var info = this.GetType().GetMethod(nameof(GetCollectionByType), BindingFlags.NonPublic | BindingFlags.Instance);
            var caller = info.MakeGenericMethod(typeOfModel);
            return caller.Invoke(this, new object[] { collectionName });
        }

        /// <summary>
        /// Gets the database collection by collection name.
        /// </summary>
        /// <typeparam name="T">Type of collection data model</typeparam>
        /// <param name="collectionName">Name of collection</param>
        /// <returns>Returns the collection property.</returns>
        private IMongoCollection<T> GetCollectionByType<T>(string collectionName)
        {
            _logger.LogDebug($"Get data collection by name {collectionName}.");
            return _databaseContext.GetCollection<T>(collectionName);
        }

        /// <summary>
        /// Check if the version value of the configuration json is newer than the database configuration value.
        /// </summary>
        /// <param name="configDocumentVersionValue">Value of version at the configuration json</param>
        /// <param name="configCollectionName">Name of configuration database collection</param>
        /// <returns>Returns true if the configuration is newer.</returns>
        private async Task<bool> IsNewerConfigVersionAsync(string configDocumentVersionValue, string configCollectionName)
        {
            if (string.IsNullOrEmpty(configDocumentVersionValue) || !Version.TryParse(configDocumentVersionValue, out var configDocumentVersion))
            {
                _logger.LogError("Configuration version value is missing or not a valid version string. -> " + configDocumentVersionValue);
                return false;
            }

            var config = await _databaseContext.GetCollection<DtoDbInitConfig>(configCollectionName).Find(x => true).FirstOrDefaultAsync();

            if (config == null)
            {
                _logger.LogInformation("Configuration item was not found at database.");
                return true;
            }
            else
            {
                if (!Version.TryParse(config.version, out var currentVerion))
                {
                    _logger.LogInformation("Version value at database is not valid. -> " + config.version);
                    return true;
                }                    
                else
                {
                    _logger.LogDebug("Valid version value was found at database. -> " + config.version);
                    return currentVerion.CompareTo(configDocumentVersion) == -1;
                }
            }
        }

        /// <summary>
        /// Updates the current configuration version value at data store.
        /// </summary>
        /// <param name="configDocumentVersionValue">Value of version</param>
        /// <param name="configCollectionName">Name of configuration database collection</param>
        private async Task UpdateConfigVersionAsync(string configDocumentVersionValue, string configCollectionName)
        {
            var configCollection = _databaseContext.GetCollection<DtoDbInitConfig>(configCollectionName);

            var dataResult = await configCollection.Find(x => true).FirstOrDefaultAsync();

            if(dataResult == null)
            {
                _logger.LogInformation("No configuration element was found at database.");
                var configItem = new DtoDbInitConfig
                {
                    version = configDocumentVersionValue
                };

                await configCollection.InsertOneAsync(configItem);
                _logger.LogDebug("New configuration element was inserted at database.");
            }
            else
            {
                _logger.LogDebug("Existing configuration element was found at database.");
                await configCollection.UpdateOneAsync(Builders<DtoDbInitConfig>.Filter.Eq(x => x.id, dataResult.id), Builders<DtoDbInitConfig>.Update.Set(x => x.version, configDocumentVersionValue));
                _logger.LogDebug("Version value was updated at database. -> " + configDocumentVersionValue);
            }
        }
        #endregion
    }
}
