﻿using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace RedZoneDevelopment.MongoDbInitializer.Interface
{
    public interface ICustomUpdateOperation
    {
        /// <summary>
        /// Will be called at the beginning of a item update operation.
        /// </summary>
        /// <param name="currentType">Current type of element which will be updated.</param>
        /// <param name="data">Source element data</param>
        /// <param name="configSource">Configuration json content</param>
        /// <returns>Returns true if the default update implementation should be skipped. Otherwise the default implementation will be performed.</returns>
        /// <remarks>If you skip the default implementation no other events of the item update will be called.</remarks>
        Task<bool> ItemProcessingBeginEventAsync(Type currentType, object data, JToken configSource);
        /// <summary>
        /// Will be called after a item update operation was performed.
        /// </summary>
        /// <param name="currentType">Current type of element which will be updated.</param>
        /// <param name="data">Element which was created or updated.</param>
        /// <param name="configSource">Configuration json content</param>
        /// <returns>Returns the handling task.</returns>
        Task ItemProcessingBeginEventAsyncEndEventAsync(Type currentType, object data, JToken configSource);
        /// <summary>
        /// Will be called if a new item will be added to the collection
        /// </summary>
        /// <param name="currentType">Current type of element which will be insert.</param>
        /// <param name="data">Element which will be added to collection</param>
        /// <param name="configSource">Configuration json content</param>
        /// <returns>Returns true if the default insert implementation should be skipped. Otherwise the default implementation will be performed.</returns>
        Task<bool> InsertItemEventAsync(Type currentType, object data, JToken configSource);
        /// <summary>
        /// Will be called if a item already exists at the collection.
        /// </summary>
        /// <param name="currentType">Current type of element which will be updated.</param>
        /// <param name="existing">Existing data object</param>
        /// <param name="data">Destination data object</param>
        /// <param name="configSource">Configuration json content</param>
        /// <returns>Returns true if the default update implementation should be skipped. Otherwise the default implementation will be performed.</returns>
        Task<bool> UpdateItemEventAsync(Type currentType, object existing, object data, JToken configSource);
    }
}