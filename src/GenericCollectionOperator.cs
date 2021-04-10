using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using RedZoneDevelopment.MongoAutoUpdater.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RedZoneDevelopment.MongoAutoUpdater
{
    /// <summary>
    /// Controls the data collection operations.
    /// </summary>
    /// <typeparam name="T">Type of data item</typeparam>
    internal class GenericCollectionOperator<T>
    {
        #region // private members
        private readonly IMongoCollection<T> _collection;
        private readonly string[] _keys;
        private readonly Dictionary<T, JToken> _items;
        private readonly ICustomUpdateOperation _updateOperationHandler;
        private readonly ILogger _logger;
        private string _idPropertyName;
        #endregion

        #region // Constructor
        /// <summary>
        /// Create a instance by data collection type.
        /// </summary>
        /// <param name="collection">Reference to data store collection</param>
        /// <param name="data">Json configuration of the current data collection</param>
        /// <param name="updateOperationHandler">Custom operation handler</param>
        /// <param name="logger">Reference to logger instance</param>
        public GenericCollectionOperator(IMongoCollection<T> collection, JToken data, ICustomUpdateOperation updateOperationHandler, ILogger logger)
        {
            _logger = logger;
            _updateOperationHandler = updateOperationHandler;
            _collection = collection;
            _keys = data["keys"].Values<string>().ToArray();
            _logger.LogDebug(_keys.Length + " key fields were found.");

            _idPropertyName = GetIdPropertyName();
            _logger.LogDebug("Name of id property: " + _idPropertyName);

            _items = new Dictionary<T, JToken>();
            foreach (var item in data["items"])
            {
                _items.Add(item.ToObject<T>(), item);
            }
            _logger.LogDebug(_items.Count + " items were found.");
        }
        #endregion

        #region // public methods
        /// <summary>
        /// Adds or updates the items at the data store. 
        /// </summary>
        public async Task CreateOrUpdateAsync()
        {
            _logger.LogDebug("Start database update of type " + typeof(T).ToString());
            foreach (var item in _items)
            {
                await CreateOrUpdateItemAsync(item.Key, item.Value);
            }
            _logger.LogDebug("Database update completed.");
        }
        #endregion

        #region // private methods
        /// <summary>
        /// Creates or updates a item at the data store.
        /// </summary>
        /// <param name="data">Data element</param>
        /// <param name="configSource">Configuration json content</param>
        private async Task CreateOrUpdateItemAsync(T data, JToken configSource)
        {
            _logger.LogDebug("Begin element create/update operations.");
            if (!await _updateOperationHandler.ItemProcessingBeginEventAsync(typeof(T), data, configSource))
            {
                _logger.LogDebug("Perform default item query implementation.");
                // Gets the query to find the current element at the data store
                var query = GetQuery(data);

                _logger.LogDebug("Query: " + query.ToBsonDocument().ToString());
                // Query the data store
                var dataResult = await _collection.Find(query).FirstOrDefaultAsync();

                if (dataResult == null)
                {
                    _logger.LogDebug("No existing item was found.");
                    // if item not exists at data store create a new entry
                    if (!await _updateOperationHandler.InsertItemEventAsync(typeof(T), data, configSource))
                    {
                        _logger.LogDebug("Perform default item insert implementation.");
                        await _collection.InsertOneAsync(data);
                        _logger.LogDebug("Element added to database.");
                    }
                }
                else
                {
                    _logger.LogDebug("Existing item was found.");
                    // Element already exists at data store
                    if (!await _updateOperationHandler.UpdateItemEventAsync(typeof(T), dataResult, data, configSource))
                    {
                        _logger.LogDebug("Perform default item update implementation.");
                        // Gets the property names which are defined at the configuration json of this object; Only defined values can be updated
                        var definedPropertiesByConfig = GetDefinedProperties(configSource);

                        var updateSource = Builders<T>.Update;
                        // Compares the data object to the json configuration item
                        var updateOperations = Compare(dataResult, data, definedPropertiesByConfig, updateSource);
                        if (updateOperations.Any())
                        {
                            _logger.LogDebug(updateOperations.Count + " document element updates found.");
                            // Updates only the changed values
                            await _collection.UpdateOneAsync(Builders<T>.Filter.Eq(_idPropertyName, GetValueByPropertyName(dataResult, _idPropertyName)), updateSource.Combine(updateOperations));
                            _logger.LogDebug("Element update completed at database.");
                        }else
                        {
                            _logger.LogDebug("No update required.");
                        }
                    }
                }

                await _updateOperationHandler.ItemProcessingBeginEventAsync(typeof(T), dataResult == null ? data : dataResult, configSource);                
            }
            _logger.LogDebug("End element create/update operations.");
        }

        /// <summary>
        /// Gets the property names of the confiuration data root element.
        /// </summary>
        /// <param name="configSource">Item configuration json content</param>
        /// <returns>Returns a collection of property names which are defined at the configuration json.</returns>
        private List<string> GetDefinedProperties(JToken configSource)
        {
            var result = new List<string>();

            foreach (var item in configSource.Children())
            {
                result.Add(item.ToObject<JProperty>().Name);
            }
            _logger.LogDebug(result.Count + " defined json properties found.");

            return result;
        }

        /// <summary>
        /// Compares two data objects and generate the update operations.
        /// </summary>
        /// <param name="source">Source data element</param>
        /// <param name="destination">Destination data element</param>
        /// <param name="properties">List of property names which should be compared.</param>
        /// <param name="updateSource">Reference to update definiton builder</param>
        /// <returns>Returns the operation which are requires to update the source object.</returns>
        private List<UpdateDefinition<T>> Compare(T source, T destination, List<string> properties, UpdateDefinitionBuilder<T> updateSource)
        {
            var result = new List<UpdateDefinition<T>>();

            foreach (string entry in properties)
            {
                if (IsDifferent(source, destination, entry, updateSource, out var updateOperation))
                    result.Add(updateOperation);
            }

            return result;
        }

        /// <summary>
        /// Compares the same property value of a two objects.
        /// </summary>
        /// <param name="source">Source object</param>
        /// <param name="destination">Config object</param>
        /// <param name="propertyName">Name of property</param>
        /// <param name="updateSource">Reference to update defintion builder</param>
        /// <param name="update">Out param if update is required</param>
        /// <returns>Return true if the objects are different.</returns>
        private bool IsDifferent(T source, T destination, string propertyName, UpdateDefinitionBuilder<T> updateSource, out UpdateDefinition<T> update)
        {
            update = null;

            var propertyType = typeof(T).GetProperty(propertyName);
            object sourceValue = propertyType.GetValue(source);
            object destinationValue = propertyType.GetValue(destination);

            if (sourceValue == null && destinationValue == null)
                return false;

            if (sourceValue == null && destinationValue != null
                || destinationValue == null && sourceValue != null)
            {
                _logger.LogDebug("Value of property " + propertyName + " is different.");
                update = updateSource.Set(propertyName, destinationValue);
                return true;
            }

            // Compare two elements via json conversation
            var sourceToken = JToken.FromObject(sourceValue);
            var destinationToken = JToken.FromObject(destinationValue);

            if(!JToken.DeepEquals(sourceToken, destinationToken))
            {
                _logger.LogDebug("Value of property " + propertyName + " is different.");
                update = updateSource.Set(propertyName, destinationValue);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets filter query by item.
        /// </summary>
        /// <param name="item">Instance of data object</param>
        /// <returns>Returns the query to find this item.</returns>
        private FilterDefinition<T> GetQuery(T item)
        {
            var queryParams = new List<FilterDefinition<T>>();
            var result = Builders<T>.Filter;
            foreach (string key in _keys)
            {
                queryParams.Add(Builders<T>.Filter.Eq(key, GetValueByPropertyName(item, key)));
            }

            return result.And(queryParams);
        }

        /// <summary>
        /// Gets property value by name.
        /// </summary>
        /// <param name="data">Instance of element</param>
        /// <param name="propertyPath">Path to the property</param>
        /// <returns>Returns the value of the requested object.</returns>
        private object GetValueByPropertyName(object data, string propertyPath)
        {
            string[] propertyNames = propertyPath.Split('.');
            object value = data.GetType().GetProperty(propertyNames[0]).GetValue(data, null);

            if (propertyNames.Length == 1 || value == null)
                return value;
            else
            {
                return GetValueByPropertyName(value, propertyPath.Replace(propertyNames[0] + ".", ""));
            }
        }

        /// <summary>
        /// Gets the name of the id property.
        /// </summary>
        /// <returns>Returns the name of the id property.</returns>
        private string GetIdPropertyName()
        {
            _logger.LogDebug("Try to get the id property name of the type " + typeof(T).ToString());
            var typeProperties = typeof(T).GetProperties();
            foreach (var property in typeProperties)
            {
                if (property.GetCustomAttribute<BsonIdAttribute>() != null)
                    return property.Name;
            }

            _logger.LogError("Could not find the id field of type " + typeof(T).ToString());
            throw new ApplicationException(typeof(T).ToString() + " has no BsonId attribute decoration. Please add the BsonId attribute to the id property.");
        }
        #endregion
    }
}
