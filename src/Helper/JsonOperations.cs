using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace RedZoneDevelopment.MongoAutoUpdater.Helper
{
    /// <summary>
    /// Json operation helper.
    /// </summary>
    internal static class JsonOperations
    {
        /// <summary>
        /// Gets the value of the requested property.
        /// </summary>
        /// <typeparam name="T">Type of value</typeparam>
        /// <param name="data">Reference to the data container</param>
        /// <param name="name">Name of the requested property</param>
        /// <returns>Returns the value of the property.</returns>
        /// <remarks>If the property value is null than the default value of the type will be returned.</remarks>
        internal static T TryGetValue<T>(JToken data, string name)
        {
            if (data == null || data[name] == null)
                return default(T);
            else
                return data[name].Value<T>();
        }

        /// <summary>
        /// Gets values of the requested property.
        /// </summary>
        /// <typeparam name="T">Type of values</typeparam>
        /// <param name="data">Reference to the data container</param>
        /// <param name="name">Name of the requested property</param>
        /// <returns>Returns the values of the property or null if the value not exists or null.</returns>
        internal static IEnumerable<T> TryGetValues<T>(JToken data, string name)
        {
            if (data == null || data[name] == null)
                return null;
            else
                return data[name].Values<T>();
        }
    }
}
