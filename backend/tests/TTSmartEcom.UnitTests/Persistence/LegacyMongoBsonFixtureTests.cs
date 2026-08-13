using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Documents;
using TTSmartEcom.Infrastructure.MongoDb.Persistence.Mappings;

namespace TTSmartEcom.UnitTests.Persistence;

public sealed class LegacyMongoBsonFixtureTests
{
    [Fact]
    public void ProductFixture_ShouldRoundTripLegacyNamesExtraFieldsAndEmbeddedVariants()
    {
        LegacyMongoClassMaps.Register();
        ObjectId productId = ObjectId.GenerateNewId();
        ObjectId variantId = ObjectId.GenerateNewId();
        BsonDocument fixture = new()
        {
            ["_id"] = productId,
            ["__v"] = 4,
            ["name"] = "Legacy controller",
            ["code"] = "PLC-001",
            ["variant"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = variantId,
                    ["price"] = "1.250.000",
                    ["imgUrl"] = "/images/product_1700000000000.webp",
                    ["quantityForSale"] = 3.0,
                    ["legacyVariantFlag"] = true,
                },
            },
            ["legacyTopLevel"] = new BsonDocument("source", "fixture"),
        };

        ProductDocument value = BsonSerializer.Deserialize<ProductDocument>(fixture);

        Assert.Equal(productId, value.Id);
        Assert.Equal(4, value.Version);
        Assert.Equal("Legacy controller", value.Name);
        Assert.Single(value.Variants!);
        Assert.Equal(variantId, value.Variants![0].Id);
        Assert.Equal("1.250.000", value.Variants[0].Price);
        Assert.Equal("/images/product_1700000000000.webp", value.Variants[0].ImageUrl);
        Assert.True(value.Variants[0].ExtraElements!["legacyVariantFlag"].AsBoolean);
        Assert.Equal("fixture", value.ExtraElements!["legacyTopLevel"]["source"].AsString);

        BsonDocument roundTrip = value.ToBsonDocument();

        Assert.Equal(productId, roundTrip["_id"].AsObjectId);
        Assert.Equal("1.250.000", roundTrip["variant"][0]["price"].AsString);
        Assert.True(roundTrip["variant"][0]["legacyVariantFlag"].AsBoolean);
        Assert.Equal("fixture", roundTrip["legacyTopLevel"]["source"].AsString);
    }

    [Fact]
    public void ProductFixture_ShouldKeepMissingAndExplicitNullFieldsDistinct()
    {
        LegacyMongoClassMaps.Register();
        BsonDocument fixture = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["name"] = BsonNull.Value,
            ["variant"] = new BsonArray
            {
                new BsonDocument { ["imgUrl"] = BsonNull.Value },
            },
        };

        ProductDocument value = BsonSerializer.Deserialize<ProductDocument>(fixture);

        Assert.Null(value.Name);
        Assert.Single(value.Variants!);
        Assert.Null(value.Variants![0].ImageUrl);
        Assert.Null(value.Code);
        Assert.Null(value.InfoDoc);
        Assert.Empty(value.Documents!);

        BsonDocument roundTrip = value.ToBsonDocument();
        Assert.False(roundTrip.Contains("name"));
        Assert.True(roundTrip["variant"][0].AsBsonDocument["imgUrl"].IsBsonNull);
        Assert.False(roundTrip.Contains("code"));
        Assert.False(roundTrip.Contains("infoDoc"));
    }

    [Fact]
    public void OrderFixtures_ShouldPreserveInvoiceImageArrayAndLegacyVersion()
    {
        LegacyMongoClassMaps.Register();
        BsonDocument fixture = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["__v"] = 2,
            ["images"] = new BsonArray
            {
                "/invoice-images/invoice-scan-1700000000000-1.webp",
                "https://legacy.example/api/invoice-images/invoice-scan-1700000000000-2.webp?cache=1",
            },
            ["legacyStatus"] = "Processing",
        };

        OrderDocument value = BsonSerializer.Deserialize<OrderDocument>(fixture);

        Assert.Equal(2, value.Version);
        Assert.Equal(2, value.Images!.Count);
        Assert.Contains("invoice-scan-1700000000000-1.webp", value.Images[0]);
        Assert.Contains("invoice-scan-1700000000000-2.webp", value.Images[1]);
        Assert.Equal("Processing", value.ExtraElements!["legacyStatus"].AsString);

        BsonDocument roundTrip = value.ToBsonDocument();
        Assert.Equal(2, roundTrip["images"].AsBsonArray.Count);
        Assert.Equal("Processing", roundTrip["legacyStatus"].AsString);
    }

    [Fact]
    public void UserCartFixture_ShouldPreserveEmbeddedLegacyIdentifier()
    {
        LegacyMongoClassMaps.Register();
        ObjectId cartItemId = ObjectId.GenerateNewId();
        BsonDocument fixture = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["cart"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = cartItemId,
                    ["productId"] = ObjectId.GenerateNewId().ToString(),
                    ["variantIndex"] = 0,
                    ["quantity"] = 2,
                    ["status"] = true,
                },
            },
        };

        UserDocument value = BsonSerializer.Deserialize<UserDocument>(fixture);

        Assert.Equal(cartItemId, Assert.Single(value.Cart!).Id);
        Assert.Equal(cartItemId, value.ToBsonDocument()["cart"][0]["_id"].AsObjectId);
    }

    [Fact]
    public void SectionFixture_ShouldPreserveCapitalizedRootAndImageUrl()
    {
        LegacyMongoClassMaps.Register();
        BsonDocument fixture = new()
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["Section"] = new BsonArray
            {
                new BsonDocument
                {
                    ["_id"] = ObjectId.GenerateNewId(),
                    ["name"] = "Automation",
                    ["value"] = new BsonArray { "PLC", "Inverter" },
                    ["imgUrl"] = "/section-images/sectionImage_1700000000000.webp",
                },
            },
        };

        SectionDocument value = BsonSerializer.Deserialize<SectionDocument>(fixture);

        Assert.Single(value.Sections!);
        Assert.Equal("Automation", value.Sections![0].Name);
        Assert.Equal(["PLC", "Inverter"], value.Sections[0].Value);
        Assert.Equal("/section-images/sectionImage_1700000000000.webp", value.Sections[0].ImageUrl);

        BsonDocument roundTrip = value.ToBsonDocument();
        Assert.True(roundTrip.Contains("Section"));
        Assert.Equal("Automation", roundTrip["Section"][0]["name"].AsString);
        Assert.Equal("/section-images/sectionImage_1700000000000.webp", roundTrip["Section"][0]["imgUrl"].AsString);
    }

    [Fact]
    public void LegacyCollectionMap_ShouldUseExactMongoCollectionNames()
    {
        Assert.Equal("products", LegacyMongoClassMaps.GetCollectionName<ProductDocument>());
        Assert.Equal("orders", LegacyMongoClassMaps.GetCollectionName<OrderDocument>());
        Assert.Equal("sections", LegacyMongoClassMaps.GetCollectionName<SectionDocument>());
        Assert.Equal("iporders", LegacyMongoClassMaps.GetCollectionName<IpOrderDocument>());
        Assert.Equal("eporders", LegacyMongoClassMaps.GetCollectionName<EpOrderDocument>());
    }
}
