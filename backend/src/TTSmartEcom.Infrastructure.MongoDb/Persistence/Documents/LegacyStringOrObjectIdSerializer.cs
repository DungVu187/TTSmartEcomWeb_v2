using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

public sealed class LegacyStringOrObjectIdSerializer : SerializerBase<string?>
{
    public override string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        BsonType type = context.Reader.GetCurrentBsonType();
        return type switch
        {
            BsonType.String => context.Reader.ReadString(),
            BsonType.ObjectId => context.Reader.ReadObjectId().ToString(),
            BsonType.Null => ReadNull(context.Reader),
            _ => throw new FormatException($"Không thể ánh xạ BSON {type} thành productId."),
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        context.Writer.WriteString(value);
    }

    private static string? ReadNull(IBsonReader reader)
    {
        reader.ReadNull();
        return null;
    }
}
