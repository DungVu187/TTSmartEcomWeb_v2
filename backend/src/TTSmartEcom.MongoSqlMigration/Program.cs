using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Data;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.DataProtection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;


if (args.Length is 3 or 4 && string.Equals(args[0], "profile", StringComparison.OrdinalIgnoreCase))
{
    await ProfileAsync(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}

if (args.Length == 2 && string.Equals(args[0], "schema-recreate", StringComparison.OrdinalIgnoreCase))
{
    await RecreateSchemaAsync(args[1]);
    return;
}

if (args.Length == 2 && string.Equals(args[0], "schema-verify", StringComparison.OrdinalIgnoreCase))
{
    await VerifySchemaAsync(args[1]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "reconcile", StringComparison.OrdinalIgnoreCase))
{
    await ReconcileAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "assign-file-owners", StringComparison.OrdinalIgnoreCase))
{
    await AssignFileOwnersAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-users", StringComparison.OrdinalIgnoreCase))
{
    await BackfillUsersAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-stations", StringComparison.OrdinalIgnoreCase))
{
    await BackfillStationsAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-products", StringComparison.OrdinalIgnoreCase))
{
    await BackfillProductsAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-orders", StringComparison.OrdinalIgnoreCase))
{
    await BackfillOrdersAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-storage-history", StringComparison.OrdinalIgnoreCase))
{
    await BackfillStorageHistoryAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "backfill-integrations", StringComparison.OrdinalIgnoreCase))
{
    await BackfillIntegrationsAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 5 && string.Equals(args[0], "migrate-files", StringComparison.OrdinalIgnoreCase))
{
    await MigrateFilesAsync(args[1], args[2], args[3], args[4]);
    return;
}

if (args.Length == 3 && string.Equals(args[0], "verify-files", StringComparison.OrdinalIgnoreCase))
{
    await VerifyFilesAsync(args[1], args[2]);
    return;
}

if (args.Length == 4 && string.Equals(args[0], "recover-files", StringComparison.OrdinalIgnoreCase))
{
    await RecoverFilesAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length == 3 && string.Equals(args[0], "prune-missing-file-metadata", StringComparison.OrdinalIgnoreCase))
{
    await PruneMissingFileMetadataAsync(args[1], args[2]);
    return;
}

if (args.Length != 4 || !string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Usage: migrate <mongo-uri> <source-db> <sql-connection-string>");
var sqlBuilder = new SqlConnectionStringBuilder(args[3]);
if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
    throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
await using var sql = new SqlConnection(args[3]); await sql.OpenAsync();
if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", sql).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
    throw new InvalidOperationException("SQL target must be the allowlisted database TTSmart.");
var settings = MongoClientSettings.FromConnectionString(args[1]); settings.ReadPreference = ReadPreference.SecondaryPreferred;
var mongo = new MongoClient(settings).GetDatabase(args[2]);
long sourceTotal=0, mappedTotal=0, excludedTotal=0, errorsTotal=0;
foreach (var name in (await mongo.ListCollectionNamesAsync()).ToList().OrderBy(x=>x))
{
    var collection=mongo.GetCollection<BsonDocument>(name); var count=await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty); sourceTotal+=count;
    var run=await RunAsync(sql,args[2],name); long mapped=0, excluded=0, errors=0;
    using var cursor=await collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while(await cursor.MoveNextAsync()) foreach(var document in cursor.Current)
    {
        var key=Key(document); var id=GuidFrom($"{name}:{key}");
        await using var tx=(SqlTransaction)await sql.BeginTransactionAsync();
        try {
            if(name=="chatmessages" || (name == "products" && MigrationExclusions.ExcludedProductIds.Contains(key))) { await MappingAsync(sql,tx,run,args[2],name,key,"", "OwnerExcluded",null); excluded++; await tx.CommitAsync(); continue; }
            var json=RedactedJson(document); await LegacyAsync(sql,tx,run,args[2],name,key,json);
            var target=await MapAsync(sql,tx,run,args[2],name,document,key,id);
            Guid targetId = target == "LegacyRecords" ? GuidFrom("legacy:" + Hash(name + ":" + key)) : id;
            await MappingAsync(sql,tx,run,args[2],name,key,"",target,targetId); mapped++; await tx.CommitAsync();
        } catch(Exception ex) { await tx.RollbackAsync(); errors++; await IssueAsync(sql,run,$"{name}:{key}","MapperError",ex.GetType().Name); }
    }
    await ManifestAsync(sql,run,args[2],name,count,mapped,excluded,errors); await CompleteAsync(sql,run,errors);
    Console.WriteLine($"{name}: source={count}; mapped={mapped}; ownerExcluded={excluded}; errors={errors}");
    mappedTotal+=mapped; excludedTotal+=excluded; errorsTotal+=errors;
}
Console.WriteLine($"TOTAL: source={sourceTotal}; mapped={mappedTotal}; ownerExcluded={excludedTotal}; blocked=0; skipped=0; errors={errorsTotal}");
if (errorsTotal != 0) throw new InvalidOperationException("Root mapper reported errors; field-level backfill was not started.");

// `migrate` is the authoritative path: the field-level mappers below are shared
// with the narrowly scoped maintenance commands and complete the nested/derived
// relational projections that cannot be represented by the root document alone.
await BackfillUsersAsync(args[1], args[2], args[3]);
await BackfillStationsAsync(args[1], args[2], args[3]);
await BackfillProductsAsync(args[1], args[2], args[3]);
await BackfillOrdersAsync(args[1], args[2], args[3]);
await BackfillStorageHistoryAsync(args[1], args[2], args[3]);
await BackfillIntegrationsAsync(args[1], args[2], args[3]);

static async Task<Guid> RunAsync(SqlConnection c,string db,string col) { var id=GuidFrom("run:"+db+":"+col); var q="IF NOT EXISTS(SELECT 1 FROM dbo.MigrationRuns WHERE MigrationRunId=@id) INSERT dbo.MigrationRuns(MigrationRunId,SourceSystem,SourceDatabase,SourceCollection,Status,StartedAtUtc) VALUES(@id,N'MongoDB',@db,@col,N'Running',SYSUTCDATETIME()); ELSE UPDATE dbo.MigrationRuns SET Status=N'Running',FinishedAtUtc=NULL WHERE MigrationRunId=@id;"; await Exec(c,null,q,("@id",id),("@db",db),("@col",col)); return id; }
static async Task CompleteAsync(SqlConnection c,Guid id,long e)=>await Exec(c,null,"UPDATE dbo.MigrationRuns SET Status=@s,FinishedAtUtc=SYSUTCDATETIME() WHERE MigrationRunId=@id;",("@id",id),("@s",e==0?"Completed":"Failed"));
static async Task LegacyAsync(SqlConnection c,SqlTransaction t,Guid run,string db,string col,string key,string json) { var fp=Hash(col+":"+key); await Exec(c,t,"MERGE dbo.LegacyRecords WITH(HOLDLOCK) AS target USING(SELECT @f AS SourceFingerprint) AS source ON target.SourceFingerprint=source.SourceFingerprint WHEN MATCHED THEN UPDATE SET MigrationRunId=@r,CanonicalExtendedJson=@j,ContentSha256=@h,PreservationReason=N'CanonicalEvidence' WHEN NOT MATCHED THEN INSERT(LegacyRecordId,MigrationRunId,SourceDatabase,SourceCollection,SourceKey,SourceKeyType,SourcePath,SourceFingerprint,CanonicalExtendedJson,ContentSha256,PreservationReason) VALUES(@id,@r,@db,@c,@k,N'ObjectId',N'',@f,@j,@h,N'CanonicalEvidence');",("@id",GuidFrom("legacy:"+fp)),("@r",run),("@db",db),("@c",col),("@k",key),("@f",fp),("@j",json),("@h",Hash(json))); }
static async Task MappingAsync(SqlConnection c,SqlTransaction t,Guid run,string db,string col,string key,string path,string table,Guid? id) { var fp=Hash($"{col}:{key}:{path}:{table}"); await Exec(c,t,"MERGE dbo.MigrationMappings WITH(HOLDLOCK) AS target USING(SELECT @f AS MappingFingerprint) AS source ON target.MappingFingerprint=source.MappingFingerprint WHEN MATCHED THEN UPDATE SET MigrationRunId=@r,TargetTable=@t,TargetId=@id WHEN NOT MATCHED THEN INSERT(MigrationMappingId,MigrationRunId,SourceSystem,SourceDatabase,SourceCollection,SourceKey,SourceKeyType,SourcePath,MappingFingerprint,TargetTable,TargetId) VALUES(NEWID(),@r,N'MongoDB',@db,@c,@k,N'ObjectId',@p,@f,@t,@id);",("@r",run),("@db",db),("@c",col),("@k",key),("@p",path),("@f",fp),("@t",table),("@id",id??(object)DBNull.Value)); }
static async Task IssueAsync(SqlConnection c,Guid r,string path,string code,string detail)=>await Exec(c,null,"INSERT dbo.MigrationIssues(MigrationIssueId,MigrationRunId,SourcePath,IssueCode,Severity,Status,SafeDetail) VALUES(NEWID(),@r,@p,@c,N'Error',N'Open',@d);",("@r",r),("@p",path),("@c",code),("@d",detail));
static async Task ManifestAsync(SqlConnection c,Guid r,string db,string col,long s,long m,long x,long e) { var h=Hash($"{db}|{col}|{s}|{m}|{x}|{e}"); await Exec(c,null,"MERGE dbo.MigrationManifests AS x USING(SELECT @r r,@c c) s ON x.MigrationRunId=s.r AND x.SourceCollection=s.c WHEN MATCHED THEN UPDATE SET DocumentCount=@n,MappedCount=@m,OwnerExcludedCount=@x,ErrorCount=@e,ManifestChecksum=@h,ProfiledAtUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(MigrationManifestId,MigrationRunId,SourceDatabase,SourceCollection,DocumentCount,MappedCount,OwnerExcludedCount,BlockedCount,SkippedCount,ErrorCount,FileCount,ManifestChecksum,ProfiledAtUtc) VALUES(NEWID(),@r,@db,@c,@n,@m,@x,0,0,@e,0,@h,SYSUTCDATETIME());",("@r",r),("@db",db),("@c",col),("@n",s),("@m",m),("@x",x),("@e",e),("@h",h)); }
static async Task<string> MapAsync(SqlConnection c,SqlTransaction t,Guid run,string db,string col,BsonDocument d,string publicId,Guid id) {
    var json=RedactedJson(d); var v=Long(d,"__v");
    switch(col) {
      case "brands": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Brands WHERE PublicId=@p) INSERT dbo.Brands(BrandId,PublicId,Name,Version,SourceJson) VALUES(@i,@p,@n,@v,@j);",("@i",id),("@p",publicId),("@n",Text(d,"Brand")),("@v",v),("@j",json)); return "Brands";
      case "types": await Exec(c,t,"MERGE dbo.ProductTypes WITH(HOLDLOCK) AS target USING(SELECT @p AS PublicId) AS source ON target.PublicId=source.PublicId WHEN MATCHED AND EXISTS (SELECT @n,@x,@updated,@v,@j EXCEPT SELECT Name,Icon,SourceUpdatedAtUtc,Version,SourceJson FROM dbo.ProductTypes WHERE PublicId=@p) THEN UPDATE SET Name=@n,Icon=@x,SourceUpdatedAtUtc=@updated,Version=@v,SourceJson=@j WHEN NOT MATCHED THEN INSERT(ProductTypeId,PublicId,Name,Icon,SourceUpdatedAtUtc,Version,SourceJson) VALUES(@i,@p,@n,@x,@updated,@v,@j);",("@i",id),("@p",publicId),("@n",Text(d,"Type")),("@x",Text(d,"icon")),("@updated",(object?)Date(d,"updatedAt")??DBNull.Value),("@v",v),("@j",json)); return "ProductTypes";
      case "products":
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Products WHERE PublicId=@p) INSERT dbo.Products(ProductId,PublicId,Name,NameUnsigned,Code,BrandName,TypeName,CategoryName,CategoryValue,Description,VatRaw,Display,Adjusted,DetailsJson,ImagesJson,DocumentsJson,Version) VALUES(@i,@p,@n,@nu,@c,@b,@t,@s,@va,@d,@vr,@di,@ad,@j,@im,@do,@v);",("@i",id),("@p",publicId),("@n",Text(d,"name")),("@nu",Text(d,"nameUnsigned")),("@c",Text(d,"code")),("@b",Text(d,"brand")),("@t",Text(d,"type")),("@s",Text(d,"section")),("@va",Text(d,"value")),("@d",Text(d,"description")),("@vr",Text(d,"vat")),("@di",Bool(d,"display")),("@ad",Bool(d,"adjusted")),("@j",json),("@im",Json(d,"images")),("@do",Json(d,"documents")),("@v",v));
        if(d.TryGetValue("variant",out var variants)&&variants.IsBsonArray) for(var i=0;i<variants.AsBsonArray.Count;i++) if(variants[i].IsBsonDocument) { var x=variants[i].AsBsonDocument; var pid=Key(x); var vid=GuidFrom("variant:"+pid); await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.ProductVariants WHERE PublicId=@p) INSERT dbo.ProductVariants(ProductVariantId,PublicId,ProductId,SortOrder,Price,PriceRaw,ImportPrice,ImportPriceRaw,Vat,VatRaw,QuantityForSale,QuantityInStorage,DetailsJson,Version) VALUES(@i,@p,@pr,@o,@a,@ar,@b,@br,@v,@vr,@q,@qs,@j,0);",("@i",vid),("@p",pid),("@pr",id),("@o",i),("@a",Decimal(x,"price")),("@ar",Text(x,"price")),("@b",Decimal(x,"importPrice")),("@br",Text(x,"importPrice")),("@v",Decimal(d,"vat")),("@vr",Text(d,"vat")),("@q",Decimal(x,"quantityForSale")),("@qs",Decimal(x,"quantityInStorage")),("@j",RedactedJson(x))); }
        return "Products";
      case "sections":
        if(d.TryGetValue("Section",out var sections)&&sections.IsBsonArray) for(var index=0;index<sections.AsBsonArray.Count;index++) if(sections.AsBsonArray[index].IsBsonDocument){var x=sections.AsBsonArray[index].AsBsonDocument;var pk=Key(x);var categoryId=GuidFrom("category:"+pk);await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Categories WHERE PublicId=@p) INSERT dbo.Categories(CategoryId,PublicId,Name,ImageUrl,ValuesJson,Version,SourceJson) VALUES(@i,@p,@n,@u,@v,0,@j);",("@i",categoryId),("@p",pk),("@n",Text(x,"name")),("@u",Text(x,"imgUrl")),("@v",Json(x,"value")),("@j",RedactedJson(x)));await MappingAsync(c,t,run,db,"sections",publicId,$"Section[{NestedKey(x,index)}]","Categories",categoryId);} return "LegacyRecords";
      case "chips":
        foreach(var kind in new[]{"Color","Shapes","Frames","ButtonCount"}) if(d.TryGetValue(kind,out var choices)&&choices.IsBsonArray) for(var i=0;i<choices.AsBsonArray.Count;i++){var value=choices[i].ToString();var pk=Hash(kind+":"+i+":"+value).Substring(0,24).ToLowerInvariant();var optionId=GuidFrom("option:"+pk);await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.ProductOptions WHERE PublicId=@p) INSERT dbo.ProductOptions(ProductOptionId,PublicId,OptionType,Value,SortOrder,Version) VALUES(@i,@p,@t,@v,@o,0);",("@i",optionId),("@p",pk),("@t",kind),("@v",value),("@o",i));await MappingAsync(c,t,run,db,"chips",publicId,$"{kind}[{i}]","ProductOptions",optionId);} return "LegacyRecords";
      case "users": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE PublicId=@p) INSERT dbo.Users(UserId,PublicId,Name,Phone,Email,PasswordHash,Role,FunctionsJson,PermissionsJson,AddressesJson,OrderTemplatesJson,StationIdsJson,Version) VALUES(@i,@p,@n,@ph,@e,@pw,@ro,@f,@pm,@a,@o,@s,@v);",("@i",id),("@p",publicId),("@n",Text(d,"name")),("@ph",Text(d,"phone")),("@e",Text(d,"email")),("@pw",Text(d,"password")),("@ro",Text(d,"role")),("@f",Json(d,"functions")),("@pm",Json(d,"permissions")),("@a",Json(d,"address")),("@o",Json(d,"orderTemplate")),("@s",Json(d,"stations")),("@v",v)); return "Users";
      case "stations":
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Stations WHERE PublicId=@p) INSERT dbo.Stations(StationId,PublicId,Name,Code,DetailsJson,Version) VALUES(@i,@p,@n,@co,@j,@v);",("@i",id),("@p",publicId),("@n",Text(d,"stationName")),("@co",Text(d,"stationCode")),("@j",json),("@v",v));
        if(d.TryGetValue("productId",out var stationProducts)&&stationProducts.IsBsonArray) for(var i=0;i<stationProducts.AsBsonArray.Count;i++){var source=stationProducts[i].ToString();var pk=Hash("station-product:"+publicId+":"+i+":"+source).Substring(0,24).ToLowerInvariant();await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.StationProducts WHERE PublicId=@p) INSERT dbo.StationProducts(StationProductId,PublicId,StationId,SourceProductId,SortOrder,Version) VALUES(@i,@p,@s,@x,@o,0);",("@i",GuidFrom("station-product:"+pk)),("@p",pk),("@s",id),("@x",source),("@o",i));}
        return "Stations";
      case "orders":
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.SalesOrders WHERE PublicId=@p) INSERT dbo.SalesOrders(SalesOrderId,PublicId,OrderCode,CustomerPhoneSnapshot,CustomerNameSnapshot,Total,TotalRaw,Status,State,Paid,ImagesJson,Version) VALUES(@i,@p,@o,@ph,@n,@t,@tr,@s,@st,@pa,@im,@v);",("@i",id),("@p",publicId),("@o",Text(d,"orderCode")),("@ph",Text(d,"userPhone")),("@n",Text(d,"userName")),("@t",Decimal(d,"total")),("@tr",Text(d,"total")),("@s",Text(d,"status")),("@st",Text(d,"state")),("@pa",Bool(d,"payment")),("@im",Json(d,"images")),("@v",v));
        if(d.TryGetValue("cartItems",out var cart)&&cart.IsBsonArray) for(var i=0;i<cart.AsBsonArray.Count;i++) if(cart[i].IsBsonDocument){var x=cart[i].AsBsonDocument;var pk=Key(x);await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.SalesOrderItems WHERE PublicId=@p) INSERT dbo.SalesOrderItems(SalesOrderItemId,PublicId,SalesOrderId,SourceProductId,VariantIndex,Quantity,DetailsJson,SortOrder,Version) VALUES(@i,@p,@o,@s,@v,@q,@j,@n,0);",("@i",GuidFrom("sales-line:"+pk)),("@p",pk),("@o",id),("@s",Text(x,"productId")),("@v",(int)Long(x,"variantIndex")),("@q",Decimal(x,"quantity")),("@j",RedactedJson(x)),("@n",i));}
        return "SalesOrders";
      case "iporders": case "eporders":
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.InventoryOrders WHERE PublicId=@p) INSERT dbo.InventoryOrders(InventoryOrderId,PublicId,Direction,OrderName,Note,UserName,Total,TotalRaw,Status,ImagesJson,Version) VALUES(@i,@p,@d,@n,@no,@u,@t,@tr,@s,@im,@v);",("@i",id),("@p",publicId),("@d",col=="iporders"?"Import":"Export"),("@n",Text(d,"orderName")),("@no",Text(d,"note")),("@u",Text(d,"userName")),("@t",Decimal(d,"total")),("@tr",Text(d,"total")),("@s",Bool(d,"status")),("@im",Json(d,"images")),("@v",v));
        if(d.TryGetValue("productList",out var lines)&&lines.IsBsonArray) for(var i=0;i<lines.AsBsonArray.Count;i++) if(lines[i].IsBsonDocument){var x=lines[i].AsBsonDocument;var pk=Key(x);await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.InventoryOrderItems WHERE PublicId=@p) INSERT dbo.InventoryOrderItems(InventoryOrderItemId,PublicId,InventoryOrderId,SourceProductId,Price,PriceRaw,Vat,VatRaw,Quantity,ProgressQuantity,StockAppliedQuantity,Unit,Note,DetailsJson,SortOrder,Version) VALUES(@i,@p,@o,@s,@a,@ar,@v,@vr,@q,@pr,@sa,@u,@n,@j,@x,0);",("@i",GuidFrom("inventory-line:"+pk)),("@p",pk),("@o",id),("@s",Text(x,"productId")),("@a",Decimal(x,"price")),("@ar",Text(x,"price")),("@v",Decimal(x,"vat")),("@vr",Text(x,"vat")),("@q",Decimal(x,"quantity")),("@pr",Decimal(x,col=="iporders"?"quantityRe":"exportedQuantity")),("@sa",Decimal(x,"stockAppliedQuantity")),("@u",Text(x,"unit")),("@n",Text(x,"note")),("@j",RedactedJson(x)),("@x",i));}
        return "InventoryOrders";
      case "activitylogs": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.ActivityLogs WHERE PublicId=@p) INSERT dbo.ActivityLogs(ActivityLogId,PublicId,Action,ActorName,DetailsJson,CreatedAtUtc,Version) VALUES(@i,@p,@a,@n,@j,@d,@v);",("@i",id),("@p",publicId),("@a",Text(d,"action")),("@n",Text(d,"userName")),("@j",json),("@d",Date(d,"createdAt")),("@v",v)); return "ActivityLogs";
      case "storagehistories":
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.StockOperations WHERE PublicId=@p) INSERT dbo.StockOperations(StockOperationId,PublicId,OperationType,SourceReference,OccurredAtUtc,DetailsJson,Version) VALUES(@i,@p,@t,@r,@d,@j,@v);",("@i",id),("@p",publicId),("@t",Text(d,"source")),("@r",Text(d,"orderId")),("@d",Date(d,"createdAt")),("@j",json),("@v",v));
        await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.StockMovementLines WHERE PublicId=@p) INSERT dbo.StockMovementLines(StockMovementLineId,PublicId,StockOperationId,SourceProductId,Quantity,DetailsJson,SortOrder,Version) VALUES(@i,@p,@o,@s,@q,@j,0,0);",("@i",GuidFrom("stock-line:"+publicId)),("@p",Hash("stock-line:"+publicId).Substring(0,24).ToLowerInvariant()),("@o",id),("@s",Text(d,"productId")),("@q",Decimal(d,"quantity")),("@j",json)); return "StockOperations";
      case "manages": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.StorefrontSettings WHERE PublicId=@p) INSERT dbo.StorefrontSettings(StorefrontSettingsId,PublicId,ConfigurationJson,Version) VALUES(@i,@p,@j,@v);",("@i",id),("@p",publicId),("@j",json),("@v",v)); return "StorefrontSettings";
      case "voicevocabs": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.VoiceSettings WHERE PublicId=@p) INSERT dbo.VoiceSettings(VoiceSettingsId,PublicId,ConfigurationJson,Version) VALUES(@i,@p,@j,@v);",("@i",id),("@p",publicId),("@j",json),("@v",v)); return "VoiceSettings";
      case "telegramconfigs": case "zaloconfigs": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.Integrations WHERE IntegrationType=@t) INSERT dbo.Integrations(IntegrationId,PublicId,IntegrationType,ConfigurationJson,Version) VALUES(@i,@p,@t,@j,@v);",("@i",id),("@p",publicId),("@t",col=="telegramconfigs"?"Telegram":"Zalo"),("@j",json),("@v",v)); return "Integrations";
      case "counters": await Exec(c,t,"IF NOT EXISTS(SELECT 1 FROM dbo.NumberSequences WHERE SequenceCode=@c) INSERT dbo.NumberSequences(NumberSequenceId,SequenceCode,NextValue,Version) VALUES(@i,@c,@n,@v);",("@i",id),("@c",Text(d,"id")),("@n",Long(d,"seq")+1),("@v",v)); return "NumberSequences";
      default: return "LegacyRecords";
    }
}

static async Task AssignFileOwnersAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri); settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoDatabase mongo = new MongoClient(settings).GetDatabase(sourceDatabase);
    await using SqlConnection sql = new(sqlConnectionString); await sql.OpenAsync();
    long assigned = 0;
    async Task AssignAsync(string ownerType, string ownerPublicId, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        string leaf = Path.GetFileName(Uri.UnescapeDataString(url.Replace('\\', '/').Split('?', '#')[0]));
        if (string.IsNullOrWhiteSpace(leaf)) return;
        var matches = new List<Guid>();
        await using (SqlCommand find = new("SELECT FileId FROM dbo.Files WHERE FileName=@name;", sql))
        {
            find.Parameters.AddWithValue("@name", leaf);
            await using SqlDataReader reader = await find.ExecuteReaderAsync();
            while (await reader.ReadAsync()) matches.Add(reader.GetGuid(0));
        }
        if (matches.Count != 1) return;
        await Exec(sql, null, "UPDATE dbo.Files SET OwnerType=@type,OwnerPublicId=@owner WHERE FileId=@id AND (ISNULL(OwnerType,N'')<>@type OR ISNULL(OwnerPublicId,N'')<>@owner);", ("@type", ownerType), ("@owner", ownerPublicId), ("@id", matches[0]));
        assigned++;
    }
    foreach (BsonDocument section in await mongo.GetCollection<BsonDocument>("sections").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
        for (int i=0;i<Values(section,"Section").Count();i++) if (Values(section,"Section").ElementAt(i).IsBsonDocument) { BsonDocument item=Values(section,"Section").ElementAt(i).AsBsonDocument; await AssignAsync("Category",Key(item),Text(item,"imgUrl")); }
    foreach (BsonDocument station in await mongo.GetCollection<BsonDocument>("stations").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()) await AssignAsync("Station",Key(station),Text(station,"imgUrl"));
    foreach (BsonDocument product in await mongo.GetCollection<BsonDocument>("products").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()) foreach (BsonValue value in Values(product,"variant")) if(value.IsBsonDocument){BsonDocument variant=value.AsBsonDocument;await AssignAsync("ProductVariant",Key(variant),Text(variant,"imgUrl"));}
    foreach (string collection in new[]{"iporders","eporders"}) foreach(BsonDocument order in await mongo.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync()) foreach(BsonValue image in Values(order,"images")) await AssignAsync("InventoryOrder",Key(order),image.IsString?image.AsString:null);
    long linked = await ScalarLongAsync(sql, "SELECT COUNT(*) FROM dbo.Files WHERE OwnerType IS NOT NULL;");
    long unlinked = await ScalarLongAsync(sql, "SELECT COUNT(*) FROM dbo.Files WHERE OwnerType IS NULL;");
    Console.WriteLine($"FILE OWNERS: assigned={assigned}; linked={linked}; unlinked={unlinked}");
    if (linked != 243 || unlinked != 86) throw new InvalidOperationException("File owner reconciliation failed.");
}

static async Task ReconcileAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri); settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoDatabase mongo = new MongoClient(settings).GetDatabase(sourceDatabase);
    long source = 0;
    foreach (string name in (await mongo.ListCollectionNamesAsync()).ToList())
        source += await mongo.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    await using SqlConnection sql = new(sqlConnectionString); await sql.OpenAsync();
    const string query = @"
SELECT N'MappingNull' AS CheckName, COUNT_BIG(*) AS CountValue FROM dbo.MigrationMappings WHERE TargetId IS NULL AND TargetTable<>N'OwnerExcluded'
UNION ALL SELECT N'MappingBroken', COUNT_BIG(*) FROM dbo.MigrationMappings m WHERE m.TargetId IS NOT NULL AND NOT (
    (m.TargetTable=N'Brands' AND EXISTS(SELECT 1 FROM dbo.Brands x WHERE x.BrandId=m.TargetId)) OR
    (m.TargetTable=N'ProductTypes' AND EXISTS(SELECT 1 FROM dbo.ProductTypes x WHERE x.ProductTypeId=m.TargetId)) OR
    (m.TargetTable=N'Categories' AND EXISTS(SELECT 1 FROM dbo.Categories x WHERE x.CategoryId=m.TargetId)) OR
    (m.TargetTable=N'Products' AND EXISTS(SELECT 1 FROM dbo.Products x WHERE x.ProductId=m.TargetId)) OR
    (m.TargetTable=N'ProductVariants' AND EXISTS(SELECT 1 FROM dbo.ProductVariants x WHERE x.ProductVariantId=m.TargetId)) OR
    (m.TargetTable=N'Users' AND EXISTS(SELECT 1 FROM dbo.Users x WHERE x.UserId=m.TargetId)) OR
    (m.TargetTable=N'CartItems' AND EXISTS(SELECT 1 FROM dbo.CartItems x WHERE x.CartItemId=m.TargetId)) OR
    (m.TargetTable=N'UserStations' AND EXISTS(SELECT 1 FROM dbo.UserStations x WHERE x.UserStationId=m.TargetId)) OR
    (m.TargetTable=N'Stations' AND EXISTS(SELECT 1 FROM dbo.Stations x WHERE x.StationId=m.TargetId)) OR
    (m.TargetTable=N'StationProducts' AND EXISTS(SELECT 1 FROM dbo.StationProducts x WHERE x.StationProductId=m.TargetId)) OR
    (m.TargetTable=N'SalesOrders' AND EXISTS(SELECT 1 FROM dbo.SalesOrders x WHERE x.SalesOrderId=m.TargetId)) OR
    (m.TargetTable=N'SalesOrderItems' AND EXISTS(SELECT 1 FROM dbo.SalesOrderItems x WHERE x.SalesOrderItemId=m.TargetId)) OR
    (m.TargetTable=N'InventoryOrders' AND EXISTS(SELECT 1 FROM dbo.InventoryOrders x WHERE x.InventoryOrderId=m.TargetId)) OR
    (m.TargetTable=N'InventoryOrderItems' AND EXISTS(SELECT 1 FROM dbo.InventoryOrderItems x WHERE x.InventoryOrderItemId=m.TargetId)) OR
    (m.TargetTable=N'StockOperations' AND EXISTS(SELECT 1 FROM dbo.StockOperations x WHERE x.StockOperationId=m.TargetId)) OR
    (m.TargetTable=N'StockMovementLines' AND EXISTS(SELECT 1 FROM dbo.StockMovementLines x WHERE x.StockMovementLineId=m.TargetId)) OR
    (m.TargetTable=N'ActivityLogs' AND EXISTS(SELECT 1 FROM dbo.ActivityLogs x WHERE x.ActivityLogId=m.TargetId)) OR
    (m.TargetTable=N'NumberSequences' AND EXISTS(SELECT 1 FROM dbo.NumberSequences x WHERE x.NumberSequenceId=m.TargetId)) OR
    (m.TargetTable=N'ProductOptions' AND EXISTS(SELECT 1 FROM dbo.ProductOptions x WHERE x.ProductOptionId=m.TargetId)) OR
    (m.TargetTable=N'StorefrontSettings' AND EXISTS(SELECT 1 FROM dbo.StorefrontSettings x WHERE x.StorefrontSettingsId=m.TargetId)) OR
    (m.TargetTable=N'VoiceSettings' AND EXISTS(SELECT 1 FROM dbo.VoiceSettings x WHERE x.VoiceSettingsId=m.TargetId)) OR
    (m.TargetTable=N'Files' AND EXISTS(SELECT 1 FROM dbo.Files x WHERE x.FileId=m.TargetId)) OR
    (m.TargetTable=N'Integrations' AND EXISTS(SELECT 1 FROM dbo.Integrations x WHERE x.IntegrationId=m.TargetId)) OR
    (m.TargetTable=N'LegacyRecords' AND EXISTS(SELECT 1 FROM dbo.LegacyRecords x WHERE x.LegacyRecordId=m.TargetId)) OR
    m.TargetTable=N'OwnerExcluded')
UNION ALL SELECT N'MappingDuplicate', COUNT_BIG(*) FROM (SELECT MappingFingerprint FROM dbo.MigrationMappings GROUP BY MappingFingerprint HAVING COUNT_BIG(*)>1) d
UNION ALL SELECT N'OpenIssues', COUNT_BIG(*) FROM dbo.MigrationIssues WHERE Status=N'Open'
UNION ALL SELECT N'FailedRuns', COUNT_BIG(*) FROM dbo.MigrationRuns WHERE Status<>N'Completed'
UNION ALL SELECT N'PlaintextIntegrationSecret', COUNT_BIG(*) FROM dbo.Integrations WHERE ConfigurationJson LIKE N'%chatId%'
UNION ALL SELECT N'FileChecksumMissing', COUNT_BIG(*) FROM dbo.Files WHERE Sha256 IS NULL OR LEN(Sha256)<>64;";
    var results = new List<(string Name,long Count)>();
    await using (SqlCommand command = new(query, sql))
    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync()) results.Add((reader.GetString(0), reader.GetInt64(1)));
    var references = new HashSet<string>(StringComparer.Ordinal);
    await using (SqlCommand command = new("SELECT ConfigurationJson,SecretReference FROM dbo.Integrations;", sql))
    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1)) references.Add(reader.GetString(1));
            if (!reader.IsDBNull(0)) CollectSecretReferences(JsonNode.Parse(reader.GetString(0)), references);
        }
    string secretDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TTSmartEcom", "secrets");
    var files = Directory.Exists(secretDirectory)
        ? Directory.EnumerateFiles(secretDirectory, "*.secret", SearchOption.TopDirectoryOnly).Select(Path.GetFileNameWithoutExtension).Where(static name => name is not null).Select(static name => name!).ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    results.Add(("SecretReferenceMissing", references.Count(reference => !files.Contains(reference))));
    results.Add(("SecretFileUnreferenced", files.Count(file => !references.Contains(file))));
    var rootChecks = await ReconcileRootIdentityAsync(mongo, sql);
    results.Add(("RootPublicIdMismatch", rootChecks.PublicIdMismatch));
    results.Add(("RootVersionMismatch", rootChecks.VersionMismatch));
    results.Add(("TimestampMismatch", await ReconcileTimestampsAsync(mongo, sql)));
    long canonicalEvidenceMismatch = await ReconcileCanonicalEvidenceAsync(mongo, sql);
    results.Add(("CanonicalEvidenceMismatch", canonicalEvidenceMismatch));
    results.Add(("FieldMismatch", canonicalEvidenceMismatch));
    Console.WriteLine($"RECONCILE: mongoDocuments={source}");
    foreach (var result in results) Console.WriteLine($"{result.Name}={result.Count}");
    if (results.Any(static x => x.Name != "SecretFileUnreferenced" && x.Count != 0)) throw new InvalidOperationException("Reconcile failed; one or more SQL integrity checks are nonzero.");
}

static async Task<long> ReconcileCanonicalEvidenceAsync(IMongoDatabase mongo, SqlConnection sql)
{
    long mismatch = 0;
    foreach (string collection in (await mongo.ListCollectionNamesAsync()).ToList())
    {
        if (string.Equals(collection, "chatmessages", StringComparison.Ordinal)) continue;
        using IAsyncCursor<BsonDocument> cursor = await mongo.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
        while (await cursor.MoveNextAsync()) foreach (BsonDocument document in cursor.Current)
        {
            string key = Key(document);
            if (string.Equals(collection, "products", StringComparison.Ordinal) && MigrationExclusions.ExcludedProductIds.Contains(key)) continue;
            string expected = Hash(RedactedJson(document));
            await using SqlCommand command = new("SELECT ContentSha256 FROM dbo.LegacyRecords WHERE SourceFingerprint=@fingerprint;", sql);
            command.Parameters.AddWithValue("@fingerprint", Hash(collection + ":" + key));
            object? value = await command.ExecuteScalarAsync();
            if (!string.Equals(value as string, expected, StringComparison.OrdinalIgnoreCase)) mismatch++;
        }
    }
    return mismatch;
}

static async Task<long> ReconcileTimestampsAsync(IMongoDatabase mongo, SqlConnection sql)
{
    var checks = new[]
    {
        ("types", "ProductTypes", new[] { ("updatedAt", "SourceUpdatedAtUtc") }),
        ("products", "Products", new[] { ("createdAt", "SourceCreatedAtUtc"), ("updatedAt", "SourceUpdatedAtUtc") }),
        ("orders", "SalesOrders", new[] { ("createdAt", "SourceCreatedAtUtc"), ("updatedAt", "SourceUpdatedAtUtc"), ("completedAt", "CompletedAtUtc") }),
        ("iporders", "InventoryOrders", new[] { ("createdAt", "SourceCreatedAtUtc"), ("updatedAt", "SourceUpdatedAtUtc"), ("completedAt", "CompletedAtUtc") }),
        ("eporders", "InventoryOrders", new[] { ("createdAt", "SourceCreatedAtUtc"), ("updatedAt", "SourceUpdatedAtUtc"), ("completedAt", "CompletedAtUtc") }),
        ("storagehistories", "StockOperations", new[] { ("createdAt", "SourceCreatedAtUtc"), ("updatedAt", "SourceUpdatedAtUtc"), ("createdAt", "OccurredAtUtc") })
    };
    long mismatch = 0;
    foreach ((string collection, string table, (string Source, string Target)[] fields) in checks)
    {
        string columns = string.Join(',', fields.Select(static field => $"[{field.Target}]"));
        using IAsyncCursor<BsonDocument> cursor = await mongo.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
        while (await cursor.MoveNextAsync()) foreach (BsonDocument document in cursor.Current)
        {
            if (string.Equals(collection, "products", StringComparison.Ordinal) && MigrationExclusions.ExcludedProductIds.Contains(Key(document))) continue;
            await using SqlCommand command = new($"SELECT {columns} FROM dbo.[{table}] WHERE PublicId=@id;", sql); command.Parameters.AddWithValue("@id", Key(document));
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) { mismatch += fields.Length; continue; }
            for (int index = 0; index < fields.Length; index++)
            {
                DateTime? expected = Date(document, fields[index].Source);
                DateTime? actual = reader.IsDBNull(index) ? null : DateTime.SpecifyKind(reader.GetDateTime(index), DateTimeKind.Utc);
                if (expected?.Ticks != actual?.Ticks) mismatch++;
            }
        }
    }
    return mismatch;
}

static async Task<(long PublicIdMismatch, long VersionMismatch)> ReconcileRootIdentityAsync(IMongoDatabase mongo, SqlConnection sql)
{
    var targets = new Dictionary<string, (string Table, string IdColumn)>(StringComparer.Ordinal)
    {
        ["activitylogs"] = ("ActivityLogs", "ActivityLogId"), ["brands"] = ("Brands", "BrandId"), ["products"] = ("Products", "ProductId"),
        ["types"] = ("ProductTypes", "ProductTypeId"), ["users"] = ("Users", "UserId"), ["stations"] = ("Stations", "StationId"),
        ["orders"] = ("SalesOrders", "SalesOrderId"), ["iporders"] = ("InventoryOrders", "InventoryOrderId"), ["eporders"] = ("InventoryOrders", "InventoryOrderId"),
        ["storagehistories"] = ("StockOperations", "StockOperationId"), ["manages"] = ("StorefrontSettings", "StorefrontSettingsId"),
        ["voicevocabs"] = ("VoiceSettings", "VoiceSettingsId"),
        ["telegramconfigs"] = ("Integrations", "IntegrationId"), ["zaloconfigs"] = ("Integrations", "IntegrationId")
    };
    long publicIdMismatch = 0, versionMismatch = 0;
    foreach ((string collection, (string table, string idColumn)) in targets)
    {
        using IAsyncCursor<BsonDocument> cursor = await mongo.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
        while (await cursor.MoveNextAsync()) foreach (BsonDocument document in cursor.Current)
        {
            string publicId = Key(document);
            if (string.Equals(collection, "products", StringComparison.Ordinal) && MigrationExclusions.ExcludedProductIds.Contains(publicId)) continue;
            await using SqlCommand command = new($"SELECT PublicId,Version FROM dbo.[{table}] WHERE PublicId=@id;", sql);
            command.Parameters.AddWithValue("@id", publicId);
            await using SqlDataReader reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) { publicIdMismatch++; continue; }
            if (!string.Equals(reader.GetString(0), publicId, StringComparison.Ordinal)) publicIdMismatch++;
            if (reader.GetInt64(1) != Long(document, "__v")) versionMismatch++;
        }
    }
    return (publicIdMismatch, versionMismatch);
}

static void CollectSecretReferences(JsonNode? node, ISet<string> references)
{
    if (node is JsonObject obj)
    {
        foreach ((string name, JsonNode? value) in obj)
        {
            if (name.EndsWith("Reference", StringComparison.OrdinalIgnoreCase) && value is JsonValue scalar && scalar.TryGetValue<string>(out string? reference) && !string.IsNullOrWhiteSpace(reference)) references.Add(reference);
            CollectSecretReferences(value, references);
        }
    }
    else if (node is JsonArray array) foreach (JsonNode? value in array) CollectSecretReferences(value, references);
}

static async Task RecreateSchemaAsync(string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");

    string root = FindRepositoryRoot();
    var scripts = new[]
    {
        (1, "000_RecreateTTSmart30.sql"),
        (10, "010_AddProductPurchaseCount.sql"),
        (11, "011_AddStockHistorySourceTimestamps.sql"),
        (12, "012_AddProductTypeSourceUpdatedAt.sql")
    };
    foreach (var (number, file) in scripts)
    {
        string path = Path.Combine(root, "database", "sqlserver", "ttsmart", file);
        string content = await File.ReadAllTextAsync(path, Encoding.UTF8);
        string checksum = Hash(content);
        await ExecuteBatchesAsync(sqlConnectionString, content);

        await using SqlConnection target = new(sqlConnectionString);
        await target.OpenAsync();
        if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", target).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
            throw new InvalidOperationException("Schema runner was redirected from TTSmart.");
        await Exec(target, null, "MERGE dbo.SchemaVersions WITH(HOLDLOCK) AS target USING(SELECT @number AS MigrationNumber) AS source ON target.MigrationNumber=source.MigrationNumber WHEN MATCHED AND (target.MigrationName<>@name OR target.ScriptChecksum<>@checksum) THEN UPDATE SET MigrationName=@name,ScriptChecksum=@checksum WHEN NOT MATCHED THEN INSERT(SchemaVersionId,MigrationNumber,MigrationName,ScriptChecksum) VALUES(NEWID(),@number,@name,@checksum);", ("@number", number), ("@name", file), ("@checksum", checksum));
    }
    await VerifySchemaAsync(sqlConnectionString);
}

static async Task VerifySchemaAsync(string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    string root = FindRepositoryRoot();
    foreach (var (number, file) in new[] { (1, "000_RecreateTTSmart30.sql"), (10, "010_AddProductPurchaseCount.sql"), (11, "011_AddStockHistorySourceTimestamps.sql"), (12, "012_AddProductTypeSourceUpdatedAt.sql") })
    {
        string checksum = Hash(await File.ReadAllTextAsync(Path.Combine(root, "database", "sqlserver", "ttsmart", file), Encoding.UTF8));
        await using SqlConnection connection = new(sqlConnectionString); await connection.OpenAsync();
        await using SqlCommand command = new("SELECT ScriptChecksum FROM dbo.SchemaVersions WHERE MigrationNumber=@number AND MigrationName=@name;", connection);
        command.Parameters.AddWithValue("@number", number); command.Parameters.AddWithValue("@name", file);
        if (!string.Equals((string?)await command.ExecuteScalarAsync(), checksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Schema checksum drift at migration {number}.");
    }
    Console.WriteLine("SCHEMA: verified checksum and required migrations.");
}

static async Task ExecuteBatchesAsync(string sqlConnectionString, string script)
{
    SqlConnectionStringBuilder template = new(sqlConnectionString);
    string catalog = template.InitialCatalog;
    foreach (string batch in Regex.Split(script, @"(?im)^\s*GO\s*(?:--.*)?$"))
    {
        if (string.IsNullOrWhiteSpace(batch)) continue;
        Match use = Regex.Match(batch, @"(?im)^\s*USE\s+\[(?<database>[^\]]+)\]\s*;");
        if (use.Success) catalog = use.Groups["database"].Value;
        SqlConnectionStringBuilder builder = new(sqlConnectionString) { InitialCatalog = catalog };
        await using SqlConnection connection = new(builder.ConnectionString); await connection.OpenAsync();
        await using SqlCommand command = new(batch, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? current = new(Environment.CurrentDirectory); current is not null; current = current.Parent)
        if (Directory.Exists(Path.Combine(current.FullName, "database", "sqlserver", "ttsmart"))) return current.FullName;
    throw new DirectoryNotFoundException("Không tìm thấy thư mục database/sqlserver/ttsmart.");
}

static async Task ProfileAsync(string mongoUri, string sourceDatabase, string? requestedCollection)
{
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri);
    settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoDatabase database = new MongoClient(settings).GetDatabase(sourceDatabase);
    string[] collections = string.IsNullOrWhiteSpace(requestedCollection)
        ? (await database.ListCollectionNamesAsync()).ToList().OrderBy(static name => name, StringComparer.Ordinal).ToArray()
        : [requestedCollection.Trim()];

    foreach (string name in collections)
    {
        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(name);
        long count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        SortedSet<string> fields = new(StringComparer.Ordinal);
        using IAsyncCursor<BsonDocument> cursor = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
        while (await cursor.MoveNextAsync())
        {
            foreach (BsonDocument document in cursor.Current) CollectFieldPaths(document, string.Empty, fields);
        }

        Console.WriteLine($"PROFILE {name}: documents={count}; fields={string.Join(',', fields)}");
    }
}

static async Task BackfillUsersAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder sqlBuilder = new(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");

    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri);
    settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoCollection<BsonDocument> users = new MongoClient(settings).GetDatabase(sourceDatabase).GetCollection<BsonDocument>("users");
    await using SqlConnection sql = new(sqlConnectionString);
    await sql.OpenAsync();
    Guid run = await RunAsync(sql, sourceDatabase, "users");
    long processed = 0, missingUsers = 0, cartItems = 0, userStations = 0;

    using IAsyncCursor<BsonDocument> cursor = await users.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while (await cursor.MoveNextAsync())
    {
        foreach (BsonDocument source in cursor.Current)
        {
            string publicId = Key(source);
            await using SqlTransaction transaction = (SqlTransaction)await sql.BeginTransactionAsync();
            try
            {
                Guid? userId = await FindIdAsync(sql, transaction, "SELECT UserId FROM dbo.Users WHERE PublicId=@id;", publicId);
                if (userId is null)
                {
                    missingUsers++;
                    await transaction.RollbackAsync();
                    continue;
                }

                string addresses = ToJsonForField(source, "addresses", "[]");
                string templates = ToJsonForField(source, "orderTemplate", "[]");
                string[] stationIds = ReadIdentifiers(source, "station");
                string stationsJson = JsonArray(stationIds);
                string? token = Text(source, "logInString");
                string? tokenHash = string.IsNullOrWhiteSpace(token) ? null : Hash(token);
                DateTime? passwordChangedAt = Date(source, "passwordChangedAt");
                await Exec(sql, transaction, "UPDATE dbo.Users SET AddressesJson=@addresses,OrderTemplatesJson=@templates,StationIdsJson=@stations,AutoLoginTokenHash=@token,PasswordChangedAtUtc=@changed,Version=@version WHERE UserId=@id AND EXISTS (SELECT @addresses,@templates,@stations,@token,@changed,@version EXCEPT SELECT AddressesJson,OrderTemplatesJson,StationIdsJson,AutoLoginTokenHash,PasswordChangedAtUtc,Version FROM dbo.Users WHERE UserId=@id);",
                    ("@addresses", addresses), ("@templates", templates), ("@stations", stationsJson), ("@token", (object?)tokenHash ?? DBNull.Value), ("@changed", (object?)passwordChangedAt ?? DBNull.Value), ("@id", userId.Value), ("@version", Long(source, "__v")));
                await MappingAsync(sql, transaction, run, sourceDatabase, "users", publicId, "", "Users", userId);
                await LegacyAsync(sql, transaction, run, sourceDatabase, "users", publicId, RedactedJson(source));

                int addressIndex = 0;
                foreach (BsonValue address in Values(source, "addresses"))
                {
                    string path = $"addresses[{NestedKey(address, addressIndex)}]";
                    await MappingAsync(sql, transaction, run, sourceDatabase, "users", publicId, path, "Users", userId);
                    addressIndex++;
                }
                int templateIndex = 0;
                foreach (BsonValue template in Values(source, "orderTemplate"))
                {
                    string path = $"orderTemplate[{NestedKey(template, templateIndex)}]";
                    await MappingAsync(sql, transaction, run, sourceDatabase, "users", publicId, path, "Users", userId);
                    templateIndex++;
                }

                int cartIndex = 0;
                foreach (BsonValue cart in Values(source, "cart"))
                {
                    BsonDocument? item = cart.IsBsonDocument ? cart.AsBsonDocument : null;
                    string sourceProductId = item is null ? string.Empty : Text(item, "productId") ?? string.Empty;
                    int? variantIndex = item is null ? null : Int(item, "variantIndex");
                    decimal? quantity = item is null ? null : Decimal(item, "quantity");
                    bool status = item is null || (Bool(item, "status") ?? true);
                    (Guid? productId, Guid? variantId) = await FindProductAsync(sql, transaction, sourceProductId, variantIndex);
                    string cartPublicId = NestedKey(cart, cartIndex);
                    Guid cartId = GuidFrom($"cart:{publicId}:{cartPublicId}");
                    await Exec(sql, transaction, "MERGE dbo.CartItems WITH(HOLDLOCK) AS target USING(SELECT @public AS PublicId) AS source ON target.PublicId=source.PublicId WHEN MATCHED AND EXISTS (SELECT @user,@product,@variant,@source,@index,@quantity,@sort,@status,0 EXCEPT SELECT UserId,ProductId,ProductVariantId,SourceProductId,VariantIndex,Quantity,SortOrder,Status,Version FROM dbo.CartItems WHERE PublicId=@public) THEN UPDATE SET UserId=@user,ProductId=@product,ProductVariantId=@variant,SourceProductId=@source,VariantIndex=@index,Quantity=@quantity,SortOrder=@sort,Status=@status,Version=0 WHEN NOT MATCHED THEN INSERT(CartItemId,PublicId,UserId,ProductId,ProductVariantId,SourceProductId,VariantIndex,Quantity,SortOrder,Status,Version) VALUES(@id,@public,@user,@product,@variant,@source,@index,@quantity,@sort,@status,0);",
                        ("@id", cartId), ("@public", cartPublicId), ("@user", userId.Value), ("@product", (object?)productId ?? DBNull.Value), ("@variant", (object?)variantId ?? DBNull.Value), ("@source", (object?)sourceProductId ?? DBNull.Value), ("@index", (object?)variantIndex ?? DBNull.Value), ("@quantity", (object?)quantity ?? DBNull.Value), ("@sort", cartIndex), ("@status", status));
                    await MappingAsync(sql, transaction, run, sourceDatabase, "users", publicId, $"cart[{NestedKey(cart, cartIndex)}]", "CartItems", cartId);
                    cartItems++; cartIndex++;
                }

                for (int index = 0; index < stationIds.Length; index++)
                {
                    Guid? stationId = await FindIdAsync(sql, transaction, "SELECT StationId FROM dbo.Stations WHERE PublicId=@id;", stationIds[index]);
                    Guid assignmentId = GuidFrom($"user-station:{publicId}:{index}:{stationIds[index]}");
                    await Exec(sql, transaction, "MERGE dbo.UserStations WITH(HOLDLOCK) AS target USING(SELECT @id AS UserStationId) AS source ON target.UserStationId=source.UserStationId WHEN MATCHED AND EXISTS (SELECT @user,@station,@source,@sort,0 EXCEPT SELECT UserId,StationId,SourceStationId,SortOrder,Version FROM dbo.UserStations WHERE UserStationId=@id) THEN UPDATE SET UserId=@user,StationId=@station,SourceStationId=@source,SortOrder=@sort,Version=0 WHEN NOT MATCHED THEN INSERT(UserStationId,UserId,StationId,SourceStationId,SortOrder,Version) VALUES(@id,@user,@station,@source,@sort,0);",
                        ("@id", assignmentId), ("@user", userId.Value), ("@station", (object?)stationId ?? DBNull.Value), ("@source", stationIds[index]), ("@sort", index));
                    await MappingAsync(sql, transaction, run, sourceDatabase, "users", publicId, $"station[{index}]", "UserStations", assignmentId);
                    userStations++;
                }

                await transaction.CommitAsync();
                processed++;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
    await CompleteAsync(sql, run, 0);
    Console.WriteLine($"BACKFILL users: processed={processed}; missingUsers={missingUsers}; cartItems={cartItems}; userStations={userStations}");
}

static async Task BackfillStationsAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder sqlBuilder = new(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri); settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoCollection<BsonDocument> stations = new MongoClient(settings).GetDatabase(sourceDatabase).GetCollection<BsonDocument>("stations");
    await using SqlConnection sql = new(sqlConnectionString); await sql.OpenAsync();
    Guid run = await RunAsync(sql, sourceDatabase, "stations");
    long processed = 0, stationProducts = 0, linkedProducts = 0;
    using IAsyncCursor<BsonDocument> cursor = await stations.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while (await cursor.MoveNextAsync()) foreach (BsonDocument source in cursor.Current)
    {
        string publicId = Key(source);
        await using SqlTransaction transaction = (SqlTransaction)await sql.BeginTransactionAsync();
        try
        {
            Guid? stationId = await FindIdAsync(sql, transaction, "SELECT StationId FROM dbo.Stations WHERE PublicId=@id;", publicId);
            if (stationId is null) throw new InvalidOperationException("Station target is missing.");
            await Exec(sql, transaction, "UPDATE dbo.Stations SET Name=@name,Code=@code,DetailsJson=@details,Version=@version WHERE StationId=@id AND EXISTS (SELECT @name,@code,@details,@version EXCEPT SELECT Name,Code,DetailsJson,Version FROM dbo.Stations WHERE StationId=@id);",
                ("@name", (object?)Text(source, "stationName") ?? DBNull.Value), ("@code", (object?)Text(source, "stationCode") ?? DBNull.Value), ("@details", ToJsonNode(source)!.ToJsonString()), ("@id", stationId.Value), ("@version", Long(source, "__v")));
            await MappingAsync(sql, transaction, run, sourceDatabase, "stations", publicId, "", "Stations", stationId);
            int index = 0;
            foreach (BsonValue value in Values(source, "productId"))
            {
                string sourceProductId = value.ToString() ?? string.Empty;
                string rowPublicId = Hash($"station-product:{publicId}:{index}:{sourceProductId}")[..24].ToLowerInvariant();
                Guid rowId = GuidFrom($"station-product:{rowPublicId}");
                Guid? productId = await FindIdAsync(sql, transaction, "SELECT ProductId FROM dbo.Products WHERE PublicId=@id;", sourceProductId);
                await Exec(sql, transaction, "MERGE dbo.StationProducts WITH(HOLDLOCK) AS target USING(SELECT @public AS PublicId) AS source ON target.PublicId=source.PublicId WHEN MATCHED AND EXISTS (SELECT @station,@product,@sourceProduct,@sort,@details,0 EXCEPT SELECT StationId,ProductId,SourceProductId,SortOrder,DetailsJson,Version FROM dbo.StationProducts WHERE PublicId=@public) THEN UPDATE SET StationId=@station,ProductId=@product,SourceProductId=@sourceProduct,SortOrder=@sort,DetailsJson=@details,Version=0 WHEN NOT MATCHED THEN INSERT(StationProductId,PublicId,StationId,ProductId,SourceProductId,SortOrder,DetailsJson,Version) VALUES(@row,@public,@station,@product,@sourceProduct,@sort,@details,0);",
                    ("@row", rowId), ("@public", rowPublicId), ("@station", stationId.Value), ("@product", (object?)productId ?? DBNull.Value), ("@sourceProduct", (object?)sourceProductId ?? DBNull.Value), ("@sort", index), ("@details", "{}"));
                Guid? actualId = await FindIdAsync(sql, transaction, "SELECT StationProductId FROM dbo.StationProducts WHERE PublicId=@id;", rowPublicId);
                if (actualId is null) throw new InvalidOperationException("Station product target is missing after upsert.");
                await MappingAsync(sql, transaction, run, sourceDatabase, "stations", publicId, $"productId[{index}]", "StationProducts", actualId);
                stationProducts++; if (productId is not null) linkedProducts++; index++;
            }
            await transaction.CommitAsync(); processed++;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }
    await CompleteAsync(sql, run, 0);
    Console.WriteLine($"BACKFILL stations: processed={processed}; stationProducts={stationProducts}; linkedProducts={linkedProducts}");
}

static async Task BackfillProductsAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri); settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoCollection<BsonDocument> products = new MongoClient(settings).GetDatabase(sourceDatabase).GetCollection<BsonDocument>("products");
    await using SqlConnection sql = new(sqlConnectionString); await sql.OpenAsync();
    Guid run = await RunAsync(sql, sourceDatabase, "products");
    long count = 0, variantCount = 0, purchaseCount = 0;
    using IAsyncCursor<BsonDocument> cursor = await products.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while (await cursor.MoveNextAsync()) foreach (BsonDocument source in cursor.Current)
    {
        string id = Key(source);
        if (MigrationExclusions.ExcludedProductIds.Contains(id)) continue;
        await using SqlTransaction tx = (SqlTransaction)await sql.BeginTransactionAsync();
        try
        {
            Guid? productId = await FindIdAsync(sql, tx, "SELECT ProductId FROM dbo.Products WHERE PublicId=@id;", id);
            if (productId is null) throw new InvalidOperationException("Product target is missing.");
            long purchased = Long(source, "purchaseCount");
            await Exec(sql, tx, "UPDATE dbo.Products SET Name=@name,NameUnsigned=@unsigned,Code=@code,BrandName=@brand,TypeName=@type,CategoryName=@section,CategoryValue=@value,Description=@description,VatRaw=@vat,Display=@display,Adjusted=@adjusted,DetailsJson=@details,DocumentsJson=@documents,PurchaseCount=@purchase,SourceCreatedAtUtc=@created,SourceUpdatedAtUtc=@updated,Version=@version WHERE ProductId=@id AND EXISTS (SELECT @name,@unsigned,@code,@brand,@type,@section,@value,@description,@vat,@display,@adjusted,@details,@documents,@purchase,@created,@updated,@version EXCEPT SELECT Name,NameUnsigned,Code,BrandName,TypeName,CategoryName,CategoryValue,Description,VatRaw,Display,Adjusted,DetailsJson,DocumentsJson,PurchaseCount,SourceCreatedAtUtc,SourceUpdatedAtUtc,Version FROM dbo.Products WHERE ProductId=@id);",
                ("@name", (object?)Text(source,"name")??DBNull.Value),("@unsigned",(object?)Text(source,"nameUnsigned")??DBNull.Value),("@code",(object?)Text(source,"code")??DBNull.Value),("@brand",(object?)Text(source,"brand")??DBNull.Value),("@type",(object?)Text(source,"type")??DBNull.Value),("@section",(object?)Text(source,"section")??DBNull.Value),("@value",(object?)Text(source,"value")??DBNull.Value),("@description",(object?)Text(source,"description")??DBNull.Value),("@vat",(object?)Text(source,"vat")??DBNull.Value),("@display",(object?)Bool(source,"display")??DBNull.Value),("@adjusted",(object?)Bool(source,"adjusted")??DBNull.Value),("@details",ToJsonNode(source)!.ToJsonString()),("@documents",ToJsonForField(source,"documents","[]")),("@purchase",purchased),("@created",(object?)Date(source,"createdAt")??DBNull.Value),("@updated",(object?)Date(source,"updatedAt")??DBNull.Value),("@id",productId.Value),("@version",Long(source,"__v")));
            await MappingAsync(sql,tx,run,sourceDatabase,"products",id,"","Products",productId);
            purchaseCount+=purchased;
            int index=0;
            foreach(BsonValue raw in Values(source,"variant"))
            {
                if(!raw.IsBsonDocument){index++;continue;}
                BsonDocument variant=raw.AsBsonDocument;string variantPublicId=NestedKey(variant,index);
                Guid? variantId=await FindIdAsync(sql,tx,"SELECT ProductVariantId FROM dbo.ProductVariants WHERE PublicId=@id;",variantPublicId);
                if(variantId is null)throw new InvalidOperationException("Product variant target is missing.");
                await Exec(sql,tx,"UPDATE dbo.ProductVariants SET ProductId=@product,SortOrder=@sort,Price=@price,PriceRaw=@priceRaw,ImportPrice=@import,ImportPriceRaw=@importRaw,Vat=@vat,VatRaw=@vatRaw,QuantityForSale=@sale,QuantityInStorage=@storage,DetailsJson=@details,Version=Version+1 WHERE ProductVariantId=@id;",
                    ("@product",productId.Value),("@sort",index),("@price",(object?)DecimalValue(variant,"price")??DBNull.Value),("@priceRaw",(object?)Text(variant,"price")??DBNull.Value),("@import",(object?)DecimalValue(variant,"importPrice")??DBNull.Value),("@importRaw",(object?)Text(variant,"importPrice")??DBNull.Value),("@vat",(object?)DecimalValue(source,"vat")??DBNull.Value),("@vatRaw",(object?)Text(source,"vat")??DBNull.Value),("@sale",(object?)DecimalValue(variant,"quantityForSale")??DBNull.Value),("@storage",(object?)DecimalValue(variant,"quantityInStorage")??DBNull.Value),("@details",ToJsonNode(variant)!.ToJsonString()),("@id",variantId.Value));
                await MappingAsync(sql,tx,run,sourceDatabase,"products",id,$"variant[{variantPublicId}]","ProductVariants",variantId);variantCount++;index++;
            }
            await tx.CommitAsync();count++;
        }catch{await tx.RollbackAsync();throw;}
    }
    await CompleteAsync(sql,run,0);
    Console.WriteLine($"BACKFILL products: processed={count}; variants={variantCount}; purchaseCount={purchaseCount}");
}

static async Task BackfillOrdersAsync(string mongoUri, string sourceDatabase, string sqlConnectionString)
{
    SqlConnectionStringBuilder builder = new(sqlConnectionString);
    if (!string.Equals(builder.InitialCatalog, "TTSmart", StringComparison.Ordinal)) throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoUri); settings.ReadPreference = ReadPreference.SecondaryPreferred;
    IMongoDatabase mongo = new MongoClient(settings).GetDatabase(sourceDatabase);
    await using SqlConnection sql = new(sqlConnectionString); await sql.OpenAsync();
    long sales = await BackfillSalesAsync(mongo, sql, sourceDatabase);
    long inventory = await BackfillInventoryAsync(mongo, sql, sourceDatabase, "iporders", "Import", "quantityRe");
    inventory += await BackfillInventoryAsync(mongo, sql, sourceDatabase, "eporders", "Export", "quantityEx");
    Console.WriteLine($"BACKFILL orders: salesLines={sales}; inventoryLines={inventory}");
}

static async Task<long> BackfillSalesAsync(IMongoDatabase mongo, SqlConnection sql, string sourceDatabase)
{
    Guid run = await RunAsync(sql, sourceDatabase, "orders"); long lines = 0;
    using IAsyncCursor<BsonDocument> cursor = await mongo.GetCollection<BsonDocument>("orders").Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while (await cursor.MoveNextAsync()) foreach (BsonDocument source in cursor.Current)
    {
        string publicId = Key(source); await using SqlTransaction tx = (SqlTransaction)await sql.BeginTransactionAsync();
        try
        {
            Guid? orderId = await FindIdAsync(sql,tx,"SELECT SalesOrderId FROM dbo.SalesOrders WHERE PublicId=@id;",publicId); if(orderId is null)throw new InvalidOperationException("Sales order target is missing.");
            await Exec(sql,tx,"UPDATE dbo.SalesOrders SET CompletedAtUtc=@completed,SourceCreatedAtUtc=@created,SourceUpdatedAtUtc=@updated,ImagesJson=@images,Version=Version+1 WHERE SalesOrderId=@id;",("@completed",(object?)Date(source,"completedAt")??DBNull.Value),("@created",(object?)Date(source,"createdAt")??DBNull.Value),("@updated",(object?)Date(source,"updatedAt")??DBNull.Value),("@images",ToJsonForField(source,"images","[]")),("@id",orderId.Value));
            await MappingAsync(sql,tx,run,sourceDatabase,"orders",publicId,"","SalesOrders",orderId);
            int index=0;foreach(BsonValue raw in Values(source,"cartItems")){if(!raw.IsBsonDocument){index++;continue;}BsonDocument item=raw.AsBsonDocument;string itemPublicId=NestedKey(item,index);Guid? itemId=await FindIdAsync(sql,tx,"SELECT SalesOrderItemId FROM dbo.SalesOrderItems WHERE PublicId=@id;",itemPublicId);if(itemId is null)throw new InvalidOperationException("Sales item target is missing.");string sourceProduct=Text(item,"productId")??string.Empty;int? variant=Int(item,"variantIndex");(Guid? product,Guid? productVariant)=await FindProductAsync(sql,tx,sourceProduct,variant);await Exec(sql,tx,"UPDATE dbo.SalesOrderItems SET ProductId=@product,ProductVariantId=@variant,SourceProductId=@source,VariantIndex=@index,Quantity=@quantity,DetailsJson=@details,SortOrder=@sort,Version=Version+1 WHERE SalesOrderItemId=@id;",("@product",(object?)product??DBNull.Value),("@variant",(object?)productVariant??DBNull.Value),("@source",(object?)sourceProduct??DBNull.Value),("@index",(object?)variant??DBNull.Value),("@quantity",(object?)DecimalValue(item,"quantity")??DBNull.Value),("@details",ToJsonNode(item)!.ToJsonString()),("@sort",index),("@id",itemId.Value));await MappingAsync(sql,tx,run,sourceDatabase,"orders",publicId,$"cartItems[{itemPublicId}]","SalesOrderItems",itemId);lines++;index++;}
            await tx.CommitAsync();
        }catch{await tx.RollbackAsync();throw;}
    }
    await CompleteAsync(sql,run,0);return lines;
}

static async Task<long> BackfillInventoryAsync(IMongoDatabase mongo, SqlConnection sql, string sourceDatabase, string collection, string direction, string progressField)
{
    Guid run=await RunAsync(sql,sourceDatabase,collection);long lines=0;using IAsyncCursor<BsonDocument> cursor=await mongo.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while(await cursor.MoveNextAsync())foreach(BsonDocument source in cursor.Current){string publicId=Key(source);await using SqlTransaction tx=(SqlTransaction)await sql.BeginTransactionAsync();try{Guid? orderId=await FindIdAsync(sql,tx,"SELECT InventoryOrderId FROM dbo.InventoryOrders WHERE PublicId=@id;",publicId);if(orderId is null)throw new InvalidOperationException("Inventory order target is missing.");await Exec(sql,tx,"UPDATE dbo.InventoryOrders SET CompletedAtUtc=@completed,SourceCreatedAtUtc=@created,SourceUpdatedAtUtc=@updated,ImagesJson=@images,Version=Version+1 WHERE InventoryOrderId=@id AND Direction=@direction;",("@completed",(object?)Date(source,"completedAt")??DBNull.Value),("@created",(object?)Date(source,"createdAt")??DBNull.Value),("@updated",(object?)Date(source,"updatedAt")??DBNull.Value),("@images",ToJsonForField(source,"images","[]")),("@id",orderId.Value),("@direction",direction));await MappingAsync(sql,tx,run,sourceDatabase,collection,publicId,"","InventoryOrders",orderId);int index=0;foreach(BsonValue raw in Values(source,"productList")){if(!raw.IsBsonDocument){index++;continue;}BsonDocument item=raw.AsBsonDocument;string itemPublicId=NestedKey(item,index);Guid? itemId=await FindIdAsync(sql,tx,"SELECT InventoryOrderItemId FROM dbo.InventoryOrderItems WHERE PublicId=@id;",itemPublicId);if(itemId is null)throw new InvalidOperationException("Inventory item target is missing.");string sourceProduct=Text(item,"productId")??string.Empty;(Guid? product,_)=await FindProductAsync(sql,tx,sourceProduct,null);await Exec(sql,tx,"UPDATE dbo.InventoryOrderItems SET ProductId=@product,ProductVariantId=NULL,SourceProductId=@source,Price=@price,PriceRaw=@priceRaw,Vat=@vat,VatRaw=@vatRaw,Quantity=@quantity,ProgressQuantity=@progress,StockAppliedQuantity=@applied,Unit=@unit,Note=@note,DetailsJson=@details,SortOrder=@sort,Version=Version+1 WHERE InventoryOrderItemId=@id;",("@product",(object?)product??DBNull.Value),("@source",(object?)sourceProduct??DBNull.Value),("@price",(object?)DecimalValue(item,"price")??DBNull.Value),("@priceRaw",(object?)Text(item,"price")??DBNull.Value),("@vat",(object?)DecimalValue(item,"vat")??DBNull.Value),("@vatRaw",(object?)Text(item,"vat")??DBNull.Value),("@quantity",(object?)DecimalValue(item,"quantity")??DBNull.Value),("@progress",(object?)DecimalValue(item,progressField)??DBNull.Value),("@applied",(object?)DecimalValue(item,"stockAppliedQuantity")??DBNull.Value),("@unit",(object?)Text(item,"unit")??DBNull.Value),("@note",(object?)Text(item,"note")??DBNull.Value),("@details",ToJsonNode(item)!.ToJsonString()),("@sort",index),("@id",itemId.Value));await MappingAsync(sql,tx,run,sourceDatabase,collection,publicId,$"productList[{itemPublicId}]","InventoryOrderItems",itemId);lines++;index++;}await tx.CommitAsync();}catch{await tx.RollbackAsync();throw;}}
    await CompleteAsync(sql,run,0);return lines;
}

static async Task BackfillStorageHistoryAsync(string mongoUri,string sourceDatabase,string sqlConnectionString)
{
    MongoClientSettings settings=MongoClientSettings.FromConnectionString(mongoUri);settings.ReadPreference=ReadPreference.SecondaryPreferred;
    await using SqlConnection sql=new(sqlConnectionString);await sql.OpenAsync();Guid run=await RunAsync(sql,sourceDatabase,"storagehistories");long rows=0,linked=0,orphan=0;
    using IAsyncCursor<BsonDocument> cursor=await new MongoClient(settings).GetDatabase(sourceDatabase).GetCollection<BsonDocument>("storagehistories").Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync();
    while(await cursor.MoveNextAsync())foreach(BsonDocument source in cursor.Current){string id=Key(source);await using SqlTransaction tx=(SqlTransaction)await sql.BeginTransactionAsync();try{Guid? operation=await FindIdAsync(sql,tx,"SELECT StockOperationId FROM dbo.StockOperations WHERE PublicId=@id;",id);if(operation is null)throw new InvalidOperationException("Stock operation target is missing.");string productSource=Text(source,"productId")??string.Empty;Guid? product=await FindIdAsync(sql,tx,"SELECT ProductId FROM dbo.Products WHERE PublicId=@id;",productSource);await Exec(sql,tx,"UPDATE dbo.StockOperations SET OccurredAtUtc=@created,SourceCreatedAtUtc=@created,SourceUpdatedAtUtc=@updated,DetailsJson=@details,Version=Version+1 WHERE StockOperationId=@id;",("@created",(object?)Date(source,"createdAt")??DBNull.Value),("@updated",(object?)Date(source,"updatedAt")??DBNull.Value),("@details",ToJsonNode(source)!.ToJsonString()),("@id",operation.Value));string linePublic=Hash("stock-line:"+id)[..24].ToLowerInvariant();Guid? line=await FindIdAsync(sql,tx,"SELECT StockMovementLineId FROM dbo.StockMovementLines WHERE PublicId=@id;",linePublic);if(line is null)throw new InvalidOperationException("Stock line target is missing.");await Exec(sql,tx,"UPDATE dbo.StockMovementLines SET ProductId=@product,SourceProductId=@source,Quantity=@quantity,DetailsJson=@details,Version=Version+1 WHERE StockMovementLineId=@id;",("@product",(object?)product??DBNull.Value),("@source",(object?)productSource??DBNull.Value),("@quantity",(object?)DecimalValue(source,"quantity")??DBNull.Value),("@details",ToJsonNode(source)!.ToJsonString()),("@id",line.Value));await MappingAsync(sql,tx,run,sourceDatabase,"storagehistories",id,"","StockOperations",operation);await MappingAsync(sql,tx,run,sourceDatabase,"storagehistories",id,"line","StockMovementLines",line);await tx.CommitAsync();rows++;if(product is null)orphan++;else linked++;}catch{await tx.RollbackAsync();throw;}}
    await CompleteAsync(sql,run,0);Console.WriteLine($"BACKFILL storagehistories: processed={rows}; linked={linked}; orphan={orphan}");
}

static async Task BackfillIntegrationsAsync(string mongoUri,string sourceDatabase,string sqlConnectionString)
{
    MongoClientSettings settings=MongoClientSettings.FromConnectionString(mongoUri);settings.ReadPreference=ReadPreference.SecondaryPreferred;IMongoDatabase mongo=new MongoClient(settings).GetDatabase(sourceDatabase);
    string keyDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ASP.NET","DataProtection-Keys");IDataProtector protector=DataProtectionProvider.Create(new DirectoryInfo(keyDirectory)).CreateProtector("TTSmartEcom.SqlServer.LocalSecrets.v1");string secretDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TTSmartEcom","secrets");Directory.CreateDirectory(secretDirectory);
    await using SqlConnection sql=new(sqlConnectionString);await sql.OpenAsync();Guid telegramRun=await RunAsync(sql,sourceDatabase,"telegramconfigs"),zaloRun=await RunAsync(sql,sourceDatabase,"zaloconfigs");
    BsonDocument? telegram=await mongo.GetCollection<BsonDocument>("telegramconfigs").Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync();if(telegram is not null){string id=Key(telegram);JsonArray recipients=[];foreach(BsonValue raw in Values(telegram,"recipients")){if(!raw.IsBsonDocument)continue;BsonDocument item=raw.AsBsonDocument;string recipientId=NestedKey(item,recipients.Count);string? chat=Text(item,"chatId");string? reference=string.IsNullOrWhiteSpace(chat)?null:StoreProtectedSecret(secretDirectory,protector,"telegram:"+recipientId,chat);JsonObject recipient=new(){["id"]=recipientId,["label"]=Text(item,"label"),["chatSecretReference"]=reference,["type"]=Text(item,"type")??"personal",["enabled"]=Bool(item,"enabled")??true,["notifyTypes"]=ToJsonNode(item.TryGetValue("notifyTypes",out BsonValue? types)?types:new BsonArray())};recipients.Add(recipient);}JsonObject config=new(){["enabled"]=Bool(telegram,"enabled")??false,["recipients"]=recipients};await using SqlTransaction tx=(SqlTransaction)await sql.BeginTransactionAsync();try{Guid? integration=await FindIdAsync(sql,tx,"SELECT IntegrationId FROM dbo.Integrations WHERE IntegrationType=N'Telegram';",string.Empty);if(integration is null)throw new InvalidOperationException("Telegram integration target is missing.");await Exec(sql,tx,"UPDATE dbo.Integrations SET ConfigurationJson=@json,SecretReference=NULL,Version=Version+1 WHERE IntegrationId=@id;",("@json",config.ToJsonString()),("@id",integration.Value));await MappingAsync(sql,tx,telegramRun,sourceDatabase,"telegramconfigs",id,"","Integrations",integration);await LegacyAsync(sql,tx,telegramRun,sourceDatabase,"telegramconfigs",id,RedactedJson(telegram));await tx.CommitAsync();}catch{await tx.RollbackAsync();throw;}}
    BsonDocument? zalo=await mongo.GetCollection<BsonDocument>("zaloconfigs").Find(FilterDefinition<BsonDocument>.Empty).FirstOrDefaultAsync();if(zalo is not null){string id=Key(zalo);JsonObject config=new(){["appId"]=Text(zalo,"appId"),["oaId"]=Text(zalo,"oaId"),["recipientUserId"]=Text(zalo,"recipientUserId"),["secretKeyReference"]=null,["accessTokenReference"]=null,["refreshTokenReference"]=null,["expiresAt"]=null};await using SqlTransaction tx=(SqlTransaction)await sql.BeginTransactionAsync();try{Guid? integration=await FindIdAsync(sql,tx,"SELECT IntegrationId FROM dbo.Integrations WHERE IntegrationType=N'Zalo';",string.Empty);if(integration is null)throw new InvalidOperationException("Zalo integration target is missing.");await Exec(sql,tx,"UPDATE dbo.Integrations SET ConfigurationJson=@json,SecretReference=NULL,Version=Version+1 WHERE IntegrationId=@id;",("@json",config.ToJsonString()),("@id",integration.Value));await MappingAsync(sql,tx,zaloRun,sourceDatabase,"zaloconfigs",id,"","Integrations",integration);await LegacyAsync(sql,tx,zaloRun,sourceDatabase,"zaloconfigs",id,RedactedJson(zalo));await tx.CommitAsync();}catch{await tx.RollbackAsync();throw;}}
    await CompleteAsync(sql,telegramRun,0);await CompleteAsync(sql,zaloRun,0);Console.WriteLine("BACKFILL integrations: telegram=1; zalo=1");
}

static string StoreProtectedSecret(string directory,IDataProtector protector,string scope,string value)
{
    string reference="sql-"+Hash(scope)[..32].ToLowerInvariant();string path=Path.Combine(directory,reference+".secret");if(File.Exists(path)){try{if(string.Equals(protector.Unprotect(File.ReadAllText(path,Encoding.UTF8)),value,StringComparison.Ordinal))return reference;}catch{}}
    File.WriteAllText(path,protector.Protect(value),Encoding.UTF8);return reference;
}

static async Task<Guid?> FindIdAsync(SqlConnection connection, SqlTransaction transaction, string query, string publicId)
{
    await using SqlCommand command = new(query, connection, transaction);
    command.Parameters.AddWithValue("@id", publicId);
    object? result = await command.ExecuteScalarAsync();
    return result is Guid id ? id : null;
}

static async Task<(Guid? ProductId, Guid? VariantId)> FindProductAsync(SqlConnection connection, SqlTransaction transaction, string sourceProductId, int? variantIndex)
{
    if (string.IsNullOrWhiteSpace(sourceProductId)) return (null, null);
    Guid? productId = await FindIdAsync(connection, transaction, "SELECT ProductId FROM dbo.Products WHERE PublicId=@id;", sourceProductId);
    if (productId is null || variantIndex is null) return (productId, null);
    await using SqlCommand command = new("SELECT ProductVariantId FROM dbo.ProductVariants WHERE ProductId=@product AND SortOrder=@index;", connection, transaction);
    command.Parameters.AddWithValue("@product", productId.Value);
    command.Parameters.AddWithValue("@index", variantIndex.Value);
    object? result = await command.ExecuteScalarAsync();
    return (productId, result is Guid variantId ? variantId : null);
}

static IEnumerable<BsonValue> Values(BsonDocument document, string field) =>
    document.TryGetValue(field, out BsonValue? value) && value.IsBsonArray ? value.AsBsonArray : [];

static string[] ReadIdentifiers(BsonDocument document, string field)
{
    if (!document.TryGetValue(field, out BsonValue? value) || value.IsBsonNull) return [];
    return value.IsBsonArray
        ? value.AsBsonArray.Where(static item => !item.IsBsonNull).Select(static item => item.ToString() ?? string.Empty).Where(static item => item.Length == 24).ToArray()
        : value.ToString() is { Length: 24 } id ? [id] : [];
}

static string NestedKey(BsonValue value, int fallback) =>
    value.IsBsonDocument && value.AsBsonDocument.TryGetValue("_id", out BsonValue? id) && !id.IsBsonNull
        ? id.ToString() ?? fallback.ToString(CultureInfo.InvariantCulture) : fallback.ToString(CultureInfo.InvariantCulture);

static string ToJsonForField(BsonDocument document, string field, string fallback) =>
    document.TryGetValue(field, out BsonValue? value) && !value.IsBsonNull ? ToJsonNode(value)?.ToJsonString() ?? fallback : fallback;

static string JsonArray(IEnumerable<string> values) => new JsonArray(values.Select(static value => System.Text.Json.Nodes.JsonValue.Create(value)).ToArray()).ToJsonString();

static JsonNode? ToJsonNode(BsonValue value)
{
    if (value.IsBsonNull) return null;
    if (value.IsObjectId) return System.Text.Json.Nodes.JsonValue.Create(value.AsObjectId.ToString());
    if (value.IsString) return System.Text.Json.Nodes.JsonValue.Create(value.AsString);
    if (value.IsBoolean) return System.Text.Json.Nodes.JsonValue.Create(value.AsBoolean);
    if (value.IsInt32) return System.Text.Json.Nodes.JsonValue.Create(value.AsInt32);
    if (value.IsInt64) return System.Text.Json.Nodes.JsonValue.Create(value.AsInt64);
    if (value.IsDouble) return System.Text.Json.Nodes.JsonValue.Create(value.AsDouble);
    if (value.IsDecimal128) return System.Text.Json.Nodes.JsonValue.Create(value.AsDecimal128.ToString());
    if (value.IsBsonDateTime) return System.Text.Json.Nodes.JsonValue.Create(value.ToUniversalTime());
    if (value.IsBsonArray) return new JsonArray(value.AsBsonArray.Select(ToJsonNode).ToArray());
    if (value.IsBsonDocument)
    {
        JsonObject result = [];
        foreach (BsonElement element in value.AsBsonDocument.Elements) result[element.Name] = ToJsonNode(element.Value);
        return result;
    }
    return System.Text.Json.Nodes.JsonValue.Create(value.ToString());
}

static void CollectFieldPaths(BsonValue value, string prefix, ISet<string> fields)
{
    if (value.IsBsonDocument)
    {
        foreach (BsonElement element in value.AsBsonDocument.Elements)
        {
            string path = string.IsNullOrEmpty(prefix) ? element.Name : $"{prefix}.{element.Name}";
            fields.Add(path);
            CollectFieldPaths(element.Value, path, fields);
        }
    }
    else if (value.IsBsonArray)
    {
        foreach (BsonValue item in value.AsBsonArray) CollectFieldPaths(item, $"{prefix}[]", fields);
    }
}
static async Task Exec(SqlConnection c,SqlTransaction? t,string q,params (string,object?)[] p)
{
    q = AddNoOpUpdateGuard(q);
    await using var x=new SqlCommand(q,c,t);
    foreach(var(a,b) in p){if(b is DateTime value){SqlParameter parameter=x.Parameters.Add(a,SqlDbType.DateTime2);parameter.Scale=7;parameter.Value=DateTime.SpecifyKind(value,DateTimeKind.Utc);continue;}x.Parameters.AddWithValue(a,b??DBNull.Value);}
    await x.ExecuteNonQueryAsync();
}

static string AddNoOpUpdateGuard(string sql)
{
    // Backfill child projections have no source version.  A migration must never
    // manufacture a new version just because it re-read the same Mongo document.
    sql = sql.Replace("Version=Version+1", "Version=Version", StringComparison.OrdinalIgnoreCase);
    const string prefix = "UPDATE dbo.";
    if (!sql.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || sql.Contains(" EXISTS (SELECT ", StringComparison.OrdinalIgnoreCase)) return sql;
    int set = sql.IndexOf(" SET ", StringComparison.OrdinalIgnoreCase);
    int where = sql.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
    if (set < 0 || where < 0 || where <= set) return sql;
    string assignments = sql[(set + 5)..where];
    string[] parts = assignments.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var columns = new List<string>(); var values = new List<string>();
    foreach (string part in parts)
    {
        int equals = part.IndexOf('=');
        if (equals <= 0) return sql;
        string column = part[..equals].Trim(); string value = part[(equals + 1)..].Trim();
        if (value.Equals(column + "+1", StringComparison.OrdinalIgnoreCase)) return sql;
        columns.Add(column); values.Add(value);
    }
    string predicate = sql[(where + 7)..].TrimEnd(';');
    string table = sql[prefix.Length..set].Trim();
    return sql[..(where + 7)] + "(" + predicate + ") AND EXISTS (SELECT " + string.Join(',', values) + " EXCEPT SELECT " + string.Join(',', columns) + " FROM dbo." + table + " WHERE " + predicate + ");";
}
static string Key(BsonDocument d)=>d.TryGetValue("_id",out var x)?x.ToString()??Hash(d.ToJson()):Hash(d.ToJson()); static string? Text(BsonDocument d,string n)=>d.TryGetValue(n,out var v)&&!v.IsBsonNull?v.ToString():null; static string? Json(BsonDocument d,string n)=>d.TryGetValue(n,out var v)&&!v.IsBsonNull?v.ToJson():null; static long Long(BsonDocument d,string n)=>d.TryGetValue(n,out var v)&&v.IsNumeric?v.ToInt64():0; static int? Int(BsonDocument d,string n)=>d.TryGetValue(n,out var v)&&v.IsNumeric&&v.ToInt32() is var x?x:null; static decimal? Decimal(BsonDocument d,string n)=>decimal.TryParse(Text(d,n),out var x)?x:null; static decimal? DecimalValue(BsonDocument d,string n){if(!d.TryGetValue(n,out var v)||v.IsBsonNull)return null;if(v.IsNumeric)return v.ToDecimal();string raw=v.ToString()?.Trim()??string.Empty;raw=raw.EndsWith('%')?raw[..^1].Trim():raw;return decimal.TryParse(raw,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.TryParse(raw,NumberStyles.Number,CultureInfo.CurrentCulture,out value)?value:null;} static bool? Bool(BsonDocument d,string n)=>d.TryGetValue(n,out var v)&&v.IsBoolean?v.AsBoolean:null; static DateTime? Date(BsonDocument d,string n){if(!d.TryGetValue(n,out var v)||v.IsBsonNull)return null;if(v.BsonType==BsonType.DateTime)return v.ToUniversalTime();return v.IsString&&DateTimeOffset.TryParse(v.AsString,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var parsed)?parsed.UtcDateTime:null;}
static string RedactedJson(BsonDocument d){var c=d.DeepClone().AsBsonDocument; Redact(c); return c.ToJson(new JsonWriterSettings{OutputMode=JsonOutputMode.CanonicalExtendedJson});} static void Redact(BsonValue v){if(v.IsBsonDocument)foreach(var e in v.AsBsonDocument.Elements.ToList()){if(SensitiveName(e.Name))v.AsBsonDocument[e.Name]="[REDACTED]";else Redact(e.Value);}else if(v.IsBsonArray)foreach(var x in v.AsBsonArray)Redact(x);} static bool SensitiveName(string name)=>name.Equals("password",StringComparison.OrdinalIgnoreCase)||name.Equals("passwordHash",StringComparison.OrdinalIgnoreCase)||name.Contains("token",StringComparison.OrdinalIgnoreCase)||name.Contains("secret",StringComparison.OrdinalIgnoreCase)||name.Contains("otp",StringComparison.OrdinalIgnoreCase)||name.Equals("logInString",StringComparison.OrdinalIgnoreCase)||name.Equals("chatId",StringComparison.OrdinalIgnoreCase); static string Hash(string x)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x))); static Guid GuidFrom(string x)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(x))[..16]);

static async Task MigrateFilesAsync(string sqlConnectionString, string sourceUploadRoot, string storageRoot, string sourceDatabase)
{
    var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");

    string sourceRoot = RequireDirectory(sourceUploadRoot, "Source upload root");
    string targetRoot = Path.GetFullPath(storageRoot);
    Directory.CreateDirectory(targetRoot);
    if ((new DirectoryInfo(targetRoot).Attributes & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException("Storage root must not be a reparse point.");

    await using var sql = new SqlConnection(sqlConnectionString);
    await sql.OpenAsync();
    if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", sql).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
        throw new InvalidOperationException("SQL target must be the allowlisted database TTSmart.");

    Guid runId = await FileRunAsync(sql, sourceDatabase);
    long total = 0, mapped = 0, errors = 0;
    foreach (var (sourceDirectory, storageDirectory, requestPath) in FileRoots())
    {
        string categorySourceRoot = RequireDirectory(Path.Combine(sourceRoot, sourceDirectory), $"Source upload directory {sourceDirectory}");
        var files = EnumerateRegularFiles(categorySourceRoot).OrderBy(file => file.FullName, StringComparer.Ordinal).ToArray();
        long categoryMapped = 0, categoryErrors = 0;
        var manifestEntries = new List<string>(files.Length);
        foreach (FileInfo sourceFile in files)
        {
            total++;
            string relative = RequireRelativePath(categorySourceRoot, sourceFile.FullName);
            string storageKey = $"{storageDirectory}/{relative}";
            string sourceKey = $"{sourceDirectory}/{relative}";
            try
            {
                string sourceHash = await Sha256Async(sourceFile.FullName);
                manifestEntries.Add($"{relative}|{sourceFile.Length}|{sourceHash}");
                string destination = ResolveContainedPath(targetRoot, storageKey);
                await CopyIfMatchingAsync(sourceFile.FullName, destination, sourceHash);
                await using var transaction = (SqlTransaction)await sql.BeginTransactionAsync();
                try
                {
                    Guid fileId = GuidFrom($"legacy-file:{sourceDatabase}:{sourceKey}");
                    await UpsertFileAsync(sql, transaction, fileId, storageKey, sourceFile.Name, MimeType(sourceFile.Extension), sourceFile.Length, sourceHash, PublicUrl(requestPath, relative));
                    await FileMappingAsync(sql, transaction, runId, sourceDatabase, sourceKey, fileId);
                    await transaction.CommitAsync();
                    mapped++;
                    categoryMapped++;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                errors++;
                categoryErrors++;
                await FileIssueAsync(sql, runId, sourceKey, "FileMigrationError", exception.GetType().Name);
            }
        }

        await FileManifestAsync(sql, runId, sourceDatabase, $"files-{sourceDirectory}", files.Length, categoryMapped, categoryErrors, Hash(string.Join("\n", manifestEntries)));
    }

    await CompleteAsync(sql, runId, errors);
    Console.WriteLine($"FILES: source={total}; mapped={mapped}; blocked=0; skipped=0; errors={errors}");
}

static async Task VerifyFilesAsync(string sqlConnectionString, string storageRoot)
{
    var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    string root = RequireDirectory(storageRoot, "Storage root");
    await using var sql = new SqlConnection(sqlConnectionString);
    await sql.OpenAsync();
    if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", sql).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
        throw new InvalidOperationException("SQL target must be the allowlisted database TTSmart.");

    var rows = new List<(string StorageKey, long ByteLength, string Sha256)>();
    await using (var command = new SqlCommand("SELECT StorageKey,ByteLength,Sha256 FROM dbo.Files WHERE StorageKey IS NOT NULL", sql))
    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(1) || reader.IsDBNull(2)) throw new InvalidOperationException("File metadata is incomplete.");
            rows.Add((reader.GetString(0), reader.GetInt64(1), reader.GetString(2)));
        }
    }

    long physical = 0, missing = 0, lengthMismatch = 0, checksumMismatch = 0;
    foreach ((string storageKey, long byteLength, string sha256) in rows)
    {
        string path = ResolveContainedPath(root, storageKey);
        var info = new FileInfo(path);
        if (!info.Exists) { missing++; continue; }
        if (info.Length != byteLength) { lengthMismatch++; continue; }
        if (!string.Equals(await Sha256Async(path), sha256, StringComparison.Ordinal)) { checksumMismatch++; continue; }
        physical++;
    }

    long tableCount = await ScalarLongAsync(sql, "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name<>N'sysdiagrams';");
    long mappingCount = await ScalarLongAsync(sql, "SELECT COUNT(*) FROM dbo.MigrationMappings WHERE SourceSystem=N'LegacyFileSystem';");
    long manifestFileCount = await ScalarLongAsync(sql, "SELECT COALESCE(SUM(FileCount),0) FROM dbo.MigrationManifests m INNER JOIN dbo.MigrationRuns r ON r.MigrationRunId=m.MigrationRunId WHERE r.SourceSystem=N'LegacyFileSystem';");
    long issueCount = await ScalarLongAsync(sql, "SELECT COUNT(*) FROM dbo.MigrationIssues i INNER JOIN dbo.MigrationRuns r ON r.MigrationRunId=i.MigrationRunId WHERE r.SourceSystem=N'LegacyFileSystem' AND i.Status=N'Open';");
    Console.WriteLine($"FILES VERIFIED: metadata={rows.Count}; physical={physical}; missing={missing}; lengthMismatch={lengthMismatch}; checksumMismatch={checksumMismatch}; legacyMappings={mappingCount}; manifestFiles={manifestFileCount}; openIssues={issueCount}; tables={tableCount}");
    if (physical != rows.Count || mappingCount != manifestFileCount || issueCount != 0) throw new InvalidOperationException("File migration reconciliation failed.");
}

static async Task RecoverFilesAsync(string sqlConnectionString, string fallbackRoot, string storageRoot)
{
    var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    string sourceRoot = RequireDirectory(fallbackRoot, "Fallback file root");
    string targetRoot = RequireDirectory(storageRoot, "Storage root");
    await using var sql = new SqlConnection(sqlConnectionString);
    await sql.OpenAsync();
    if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", sql).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
        throw new InvalidOperationException("SQL target must be the allowlisted database TTSmart.");

    var candidates = new Dictionary<(long Length, string Sha256), string>();
    foreach (FileInfo candidate in EnumerateRegularFiles(sourceRoot))
    {
        string checksum = await Sha256Async(candidate.FullName);
        candidates.TryAdd((candidate.Length, checksum), candidate.FullName);
    }

    var missingRows = new List<(string StorageKey, long ByteLength, string Sha256)>();
    await using (var command = new SqlCommand("SELECT StorageKey,ByteLength,Sha256 FROM dbo.Files WHERE StorageKey IS NOT NULL AND ByteLength IS NOT NULL AND Sha256 IS NOT NULL", sql))
    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            string storageKey = reader.GetString(0);
            if (!File.Exists(ResolveContainedPath(targetRoot, storageKey))) missingRows.Add((storageKey, reader.GetInt64(1), reader.GetString(2)));
        }
    }

    long recovered = 0, unresolved = 0;
    foreach ((string storageKey, long byteLength, string sha256) in missingRows)
    {
        if (!candidates.TryGetValue((byteLength, sha256), out string? source)) { unresolved++; continue; }
        string destination = ResolveContainedPath(targetRoot, storageKey);
        await CopyIfMatchingAsync(source, destination, sha256);
        recovered++;
    }
    Console.WriteLine($"FILES RECOVERED: candidates={candidates.Count}; missing={missingRows.Count}; recovered={recovered}; unresolved={unresolved}");
    if (unresolved != 0) throw new InvalidOperationException("Some file metadata could not be recovered from the approved local fallback root.");
}

static async Task PruneMissingFileMetadataAsync(string sqlConnectionString, string storageRoot)
{
    var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);
    if (!string.Equals(sqlBuilder.InitialCatalog, "TTSmart", StringComparison.Ordinal))
        throw new ArgumentException("SQL target must be the allowlisted database TTSmart.");
    string root = RequireDirectory(storageRoot, "Storage root");
    await using var sql = new SqlConnection(sqlConnectionString);
    await sql.OpenAsync();
    if (!string.Equals((string?)await new SqlCommand("SELECT DB_NAME();", sql).ExecuteScalarAsync(), "TTSmart", StringComparison.Ordinal))
        throw new InvalidOperationException("SQL target must be the allowlisted database TTSmart.");

    var candidates = new List<Guid>();
    await using (var command = new SqlCommand("SELECT f.FileId,f.StorageKey FROM dbo.Files f WHERE f.StorageKey IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.MigrationMappings m WHERE m.TargetId=f.FileId AND m.SourceSystem=N'LegacyFileSystem')", sql))
    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            string path = ResolveContainedPath(root, reader.GetString(1));
            if (!File.Exists(path)) candidates.Add(reader.GetGuid(0));
        }
    }

    await using var transaction = (SqlTransaction)await sql.BeginTransactionAsync();
    try
    {
        foreach (Guid fileId in candidates)
            await Exec(sql, transaction, "DELETE FROM dbo.Files WHERE FileId=@id AND NOT EXISTS(SELECT 1 FROM dbo.MigrationMappings WHERE TargetId=@id AND SourceSystem=N'LegacyFileSystem');", ("@id", fileId));
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
    Console.WriteLine($"FILES METADATA PRUNED: orphaned={candidates.Count}");
}

static async Task<long> ScalarLongAsync(SqlConnection connection, string query)
{
    await using var command = new SqlCommand(query, connection);
    return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static (string SourceDirectory, string StorageDirectory, string RequestPath)[] FileRoots() =>
[
    ("images", "images", "images"),
    ("documents", "documents", "documents"),
    ("invoices", "invoices", "invoice-images"),
    ("sections", "sections", "section-images"),
    ("stations", "stations", "station"),
];

static string RequireDirectory(string path, string name)
{
    string fullPath = Path.GetFullPath(path);
    if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"{name} does not exist.");
    if ((new DirectoryInfo(fullPath).Attributes & FileAttributes.ReparsePoint) != 0)
        throw new InvalidOperationException($"{name} must not be a reparse point.");
    return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

static IEnumerable<FileInfo> EnumerateRegularFiles(string root)
{
    var pending = new Stack<DirectoryInfo>();
    pending.Push(new DirectoryInfo(root));
    while (pending.Count > 0)
    {
        DirectoryInfo current = pending.Pop();
        foreach (DirectoryInfo directory in current.EnumerateDirectories())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(directory);
        }
        foreach (FileInfo file in current.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) == 0) yield return file;
        }
    }
}

static string RequireRelativePath(string root, string candidate)
{
    string relative = Path.GetRelativePath(root, candidate);
    if (relative.Length == 0 || Path.IsPathFullyQualified(relative) || relative.Equals("..", StringComparison.Ordinal) || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        throw new InvalidOperationException("File path escapes its configured root.");
    return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}

static string ResolveContainedPath(string root, string storageKey)
{
    if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(storageKey))
        throw new InvalidOperationException("Storage key is invalid.");
    string path = Path.GetFullPath(Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar)));
    string relative = Path.GetRelativePath(root, path);
    if (Path.IsPathFullyQualified(relative) || relative.Equals("..", StringComparison.Ordinal) || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        throw new InvalidOperationException("Storage path escapes its configured root.");
    return path;
}

static async Task CopyIfMatchingAsync(string source, string destination, string sourceHash)
{
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    if (File.Exists(destination))
    {
        if (!string.Equals(sourceHash, await Sha256Async(destination), StringComparison.Ordinal))
            throw new IOException("Destination checksum does not match source checksum.");
        return;
    }

    string temporary = destination + ".partial-" + Guid.NewGuid().ToString("N");
    try
    {
        await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous))
        await using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
        {
            await input.CopyToAsync(output);
            await output.FlushAsync();
        }
        if (!string.Equals(sourceHash, await Sha256Async(temporary), StringComparison.Ordinal))
            throw new IOException("Copied file checksum does not match source checksum.");
        File.Move(temporary, destination);
    }
    finally
    {
        if (File.Exists(temporary)) File.Delete(temporary);
    }
}

static async Task<string> Sha256Async(string path)
{
    await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream));
}

static string PublicUrl(string requestPath, string relative) => "/" + requestPath.Trim('/') + "/" + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
static string? MimeType(string extension) => extension.ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".pdf" => "application/pdf", ".webm" => "audio/webm", ".mp3" => "audio/mpeg", ".wav" => "audio/wav", ".ogg" => "audio/ogg", ".m4a" => "audio/mp4", _ => "application/octet-stream" };

static async Task<Guid> FileRunAsync(SqlConnection connection, string sourceDatabase)
{
    Guid id = GuidFrom($"file-run:{sourceDatabase}");
    await Exec(connection, null, "IF NOT EXISTS(SELECT 1 FROM dbo.MigrationRuns WHERE MigrationRunId=@id) INSERT dbo.MigrationRuns(MigrationRunId,SourceSystem,SourceDatabase,SourceCollection,Status,StartedAtUtc) VALUES(@id,N'LegacyFileSystem',@db,N'files',N'Running',SYSUTCDATETIME()); ELSE UPDATE dbo.MigrationRuns SET Status=N'Running',FinishedAtUtc=NULL WHERE MigrationRunId=@id;", ("@id", id), ("@db", sourceDatabase));
    return id;
}

static async Task UpsertFileAsync(SqlConnection connection, SqlTransaction transaction, Guid fileId, string storageKey, string fileName, string? mimeType, long length, string sha256, string sourceUrl)
{
    await Exec(connection, transaction, "MERGE dbo.Files WITH(HOLDLOCK) AS target USING(SELECT @key AS StorageKey) AS source ON target.StorageKey=source.StorageKey WHEN MATCHED AND (target.FileName<>@name OR ISNULL(target.MimeType,N'')<>ISNULL(@mime,N'') OR target.ByteLength<>@length OR target.Sha256<>@sha OR target.SourceUrl<>@url) THEN UPDATE SET FileName=@name,MimeType=@mime,ByteLength=@length,Sha256=@sha,SourceUrl=@url,Version=target.Version+1 WHEN NOT MATCHED THEN INSERT(FileId,PublicId,StorageKey,FileName,MimeType,ByteLength,Sha256,SourceUrl,Version) VALUES(@id,@publicId,@key,@name,@mime,@length,@sha,@url,0);", ("@id", fileId), ("@publicId", Hash("file:" + storageKey)[..24].ToLowerInvariant()), ("@key", storageKey), ("@name", fileName), ("@mime", (object?)mimeType ?? DBNull.Value), ("@length", length), ("@sha", sha256), ("@url", sourceUrl));
}

static async Task FileMappingAsync(SqlConnection connection, SqlTransaction transaction, Guid runId, string sourceDatabase, string sourceKey, Guid fileId)
{
    string fingerprint = Hash("file:" + sourceKey);
    await Exec(connection, transaction, "IF NOT EXISTS(SELECT 1 FROM dbo.MigrationMappings WHERE MappingFingerprint=@f) INSERT dbo.MigrationMappings(MigrationMappingId,MigrationRunId,SourceSystem,SourceDatabase,SourceCollection,SourceKey,SourceKeyType,SourcePath,MappingFingerprint,TargetTable,TargetId) VALUES(NEWID(),@r,N'LegacyFileSystem',@db,N'files',@key,N'RelativePath',N'',@f,N'Files',@id);", ("@r", runId), ("@db", sourceDatabase), ("@key", sourceKey), ("@f", fingerprint), ("@id", fileId));
}

static async Task FileIssueAsync(SqlConnection connection, Guid runId, string sourcePath, string code, string detail)
{
    await Exec(connection, null, "IF NOT EXISTS(SELECT 1 FROM dbo.MigrationIssues WHERE MigrationRunId=@r AND SourcePath=@p AND IssueCode=@c AND Status=N'Open') INSERT dbo.MigrationIssues(MigrationIssueId,MigrationRunId,SourcePath,IssueCode,Severity,Status,SafeDetail) VALUES(NEWID(),@r,@p,@c,N'Error',N'Open',@d);", ("@r", runId), ("@p", sourcePath), ("@c", code), ("@d", detail));
}

static async Task FileManifestAsync(SqlConnection connection, Guid runId, string sourceDatabase, string collection, long count, long mapped, long errors, string checksum)
{
    await Exec(connection, null, "MERGE dbo.MigrationManifests AS target USING(SELECT @r AS RunId,@c AS Collection) AS source ON target.MigrationRunId=source.RunId AND target.SourceCollection=source.Collection WHEN MATCHED THEN UPDATE SET DocumentCount=@count,MappedCount=@mapped,OwnerExcludedCount=0,BlockedCount=0,SkippedCount=0,ErrorCount=@errors,FileCount=@mapped,ManifestChecksum=@checksum,ProfiledAtUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(MigrationManifestId,MigrationRunId,SourceDatabase,SourceCollection,DocumentCount,MappedCount,OwnerExcludedCount,BlockedCount,SkippedCount,ErrorCount,FileCount,ManifestChecksum,ProfiledAtUtc) VALUES(NEWID(),@r,@db,@c,@count,@mapped,0,0,0,@errors,@mapped,@checksum,SYSUTCDATETIME());", ("@r", runId), ("@db", sourceDatabase), ("@c", collection), ("@count", count), ("@mapped", mapped), ("@errors", errors), ("@checksum", checksum));
}

file static class MigrationExclusions
{
    public static readonly HashSet<string> ExcludedProductIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "685f986fcc1b1c0b55f004b1",
        "6976c2a88a5d6183b8eb7b72",
        "696b439215a39231fa16d880",
        "68ef0e13b60d69e822bb144e",
        "683d4efafa198d7e0cc1ecb1",
        "690c01a8e1878440f2af568f"
    };
}
