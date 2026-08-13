using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;

namespace TTSmartEcom.Infrastructure.MongoDb.Persistence.Mappings;

/// <summary>
/// Explicit, process-wide registration for the legacy Mongoose document shapes.
/// Registration is intentionally side-effect free with respect to MongoDB: it
/// only configures the driver's in-memory serializers and class maps.
/// </summary>
public static class LegacyMongoClassMaps
{
    private static readonly object SyncRoot = new();
    private static bool _registered;

    public static IReadOnlyDictionary<Type, string> CollectionNames { get; } =
        new Dictionary<Type, string>
        {
            [typeof(ActivityLogDocument)] = ActivityLogDocument.CollectionName,
            [typeof(BrandDocument)] = BrandDocument.CollectionName,
            [typeof(ChipDocument)] = ChipDocument.CollectionName,
            [typeof(SectionDocument)] = SectionDocument.CollectionName,
            [typeof(DrinkDocument)] = DrinkDocument.CollectionName,
            [typeof(DrinkToppingsDocument)] = DrinkToppingsDocument.CollectionName,
            [typeof(DrinkBillDocument)] = DrinkBillDocument.CollectionName,
            [typeof(DrinkOweListDocument)] = DrinkOweListDocument.CollectionName,
            [typeof(EpOrderDocument)] = EpOrderDocument.CollectionName,
            [typeof(IpOrderDocument)] = IpOrderDocument.CollectionName,
            [typeof(ManageDocument)] = ManageDocument.CollectionName,
            [typeof(CounterDocument)] = CounterDocument.CollectionName,
            [typeof(OrderDocument)] = OrderDocument.CollectionName,
            [typeof(ProductDocument)] = ProductDocument.CollectionName,
            [typeof(ProductTypeDocument)] = ProductTypeDocument.CollectionName,
            [typeof(StationDocument)] = StationDocument.CollectionName,
            [typeof(StorageHistoryDocument)] = StorageHistoryDocument.CollectionName,
            [typeof(UserDocument)] = UserDocument.CollectionName,
            [typeof(VoiceVocabDocument)] = VoiceVocabDocument.CollectionName,
            [typeof(TelegramConfigDocument)] = TelegramConfigDocument.CollectionName,
            [typeof(ZaloConfigDocument)] = ZaloConfigDocument.CollectionName,
        };

    /// <summary>
    /// Registers all root document maps once. Calling this method repeatedly is safe.
    /// </summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_registered)
            {
                return;
            }

            RegisterRoot<ActivityLogDocument>();
            RegisterRoot<BrandDocument>();
            RegisterRoot<ChipDocument>();
            RegisterRoot<SectionDocument>();
            RegisterRoot<DrinkDocument>();
            RegisterRoot<DrinkToppingsDocument>();
            RegisterRoot<DrinkBillDocument>();
            RegisterRoot<DrinkOweListDocument>();
            RegisterRoot<EpOrderDocument>();
            RegisterRoot<IpOrderDocument>();
            RegisterRoot<ManageDocument>();
            RegisterRoot<CounterDocument>();
            RegisterRoot<OrderDocument>();
            RegisterRoot<ProductDocument>();
            RegisterRoot<ProductTypeDocument>();
            RegisterRoot<StationDocument>();
            RegisterRoot<StorageHistoryDocument>();
            RegisterRoot<UserDocument>();
            RegisterRoot<VoiceVocabDocument>();
            RegisterRoot<TelegramConfigDocument>();
            RegisterRoot<ZaloConfigDocument>();

            _registered = true;
        }
    }

    public static string GetCollectionName<TDocument>()
        where TDocument : class
    {
        Register();
        return CollectionNames.TryGetValue(typeof(TDocument), out var name)
            ? name
            : throw new ArgumentException($"No legacy Mongo collection is mapped for {typeof(TDocument).FullName}.", nameof(TDocument));
    }

    private static void RegisterRoot<TDocument>()
        where TDocument : class
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(TDocument)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<TDocument>(classMap =>
        {
            classMap.AutoMap();
            // Unknown legacy fields must survive a read/write round-trip. The
            // [BsonExtraElements] member on LegacyMongoEntity captures them.
            classMap.SetIgnoreExtraElements(false);
        });
    }
}
