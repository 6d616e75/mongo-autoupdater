using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RedZoneDevelopment.MongoAutoUpdater.Data
{
    /// <summary>
    /// Default database config file meta information.
    /// </summary>
    internal class DtoDbInitConfig
    {
        /// <summary>
        /// Identifier at the data store.
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId id { get; set; }
        /// <summary>
        /// Version number of current db init.
        /// </summary>
        public string version { get; set; }
    }
}
