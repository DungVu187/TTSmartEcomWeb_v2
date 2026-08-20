# Profile MongoDB Ecom cho migration SQL Server

Báo cáo chỉ chứa tên collection/path, kiểu BSON và số đếm; không chứa giá trị document, PII, token hoặc connection string.

Nguồn đọc-only: database `Ecom`.

## `activitylogs`

- Document: 383; rỗng: 0; `_id` ObjectId hợp lệ: 383; không phải ObjectId/missing: 0.
- Phần tử mảng: 444; reference candidate: 1126; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 383 | 383 | 0 | 0 |
| `_id` | ObjectId | 383 | 383 | 0 | 0 |
| `action` | String | 383 | 383 | 0 | 0 |
| `createdAt` | DateTime | 383 | 383 | 0 | 0 |
| `details` | Array | 383 | 383 | 0 | 0 |
| `details[]` | Document | 444 | 383 | 0 | 0 |
| `details[]._id` | ObjectId | 444 | 383 | 0 | 0 |
| `details[].field` | String | 444 | 383 | 0 | 0 |
| `details[].newValue` | String | 444 | 383 | 0 | 0 |
| `details[].oldValue` | String | 444 | 383 | 0 | 0 |
| `productId` | ObjectId | 299 | 299 | 0 | 84 |
| `productName` | String | 383 | 383 | 0 | 0 |
| `updatedAt` | DateTime | 383 | 383 | 0 | 0 |
| `userName` | String | 383 | 383 | 0 | 0 |

## `autologintokens`

- Document: 0; rỗng: 0; `_id` ObjectId hợp lệ: 0; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 0; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|

## `brands`

- Document: 29; rỗng: 0; `_id` ObjectId hợp lệ: 29; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 29; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `Brand` | String | 29 | 29 | 0 | 0 |
| `__v` | Int32 | 29 | 29 | 0 | 0 |
| `_id` | ObjectId | 29 | 29 | 0 | 0 |

## `chatmessages`

- Document: 3; rỗng: 0; `_id` ObjectId hợp lệ: 3; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 6; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 3 | 3 | 0 | 0 |
| `_id` | ObjectId | 3 | 3 | 0 | 0 |
| `createdAt` | DateTime | 3 | 3 | 0 | 0 |
| `message` | String | 3 | 3 | 0 | 0 |
| `senderName` | String | 3 | 3 | 0 | 0 |
| `senderPhone` | String | 3 | 3 | 0 | 0 |
| `senderRole` | String | 3 | 3 | 0 | 0 |
| `sessionId` | String | 3 | 3 | 0 | 0 |
| `updatedAt` | DateTime | 3 | 3 | 0 | 0 |

## `chips`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 1; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `ButtonCount` | Array | 1 | 1 | 0 | 0 |
| `Color` | Array | 1 | 1 | 0 | 0 |
| `Frames` | Array | 1 | 1 | 0 | 0 |
| `Shapes` | Array | 1 | 1 | 0 | 0 |
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |

## `counters`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 2; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |
| `id` | String | 1 | 1 | 0 | 0 |
| `seq` | Int32 | 1 | 1 | 0 | 0 |

## `eporders`

- Document: 24; rỗng: 0; `_id` ObjectId hợp lệ: 24; không phải ObjectId/missing: 0.
- Phần tử mảng: 277; reference candidate: 578; file/URL candidate: 12.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 24 | 24 | 0 | 0 |
| `_id` | ObjectId | 24 | 24 | 0 | 0 |
| `completedAt` | Null | 8 | 8 | 8 | 16 |
| `createdAt` | DateTime | 24 | 24 | 0 | 0 |
| `images` | Array | 12 | 12 | 0 | 12 |
| `note` | String | 8 | 8 | 0 | 16 |
| `orderName` | String | 24 | 24 | 0 | 0 |
| `productList` | Array | 24 | 24 | 0 | 0 |
| `productList[]` | Document | 277 | 21 | 0 | 3 |
| `productList[]._id` | ObjectId | 277 | 21 | 0 | 3 |
| `productList[].importPriceSnapshot` | String | 7 | 4 | 0 | 20 |
| `productList[].note` | String | 277 | 21 | 0 | 3 |
| `productList[].price` | String | 277 | 21 | 0 | 3 |
| `productList[].productId` | String | 277 | 21 | 0 | 3 |
| `productList[].profitPercent` | Int32 | 7 | 4 | 0 | 20 |
| `productList[].quantity` | Int32 | 277 | 21 | 0 | 3 |
| `productList[].quantityEx` | Int32 | 277 | 21 | 0 | 3 |
| `productList[].status` | Boolean | 277 | 21 | 0 | 3 |
| `productList[].stockAppliedQuantity` | Int32 | 13 | 8 | 0 | 16 |
| `productList[].stockUpdateSkipped` | Boolean | 13 | 8 | 0 | 16 |
| `productList[].unit` | String | 277 | 21 | 0 | 3 |
| `productList[].vat` | String | 14 | 9 | 0 | 15 |
| `status` | Boolean | 24 | 24 | 0 | 0 |
| `total` | String | 24 | 24 | 0 | 0 |
| `updatedAt` | DateTime | 24 | 24 | 0 | 0 |
| `userName` | String | 24 | 24 | 0 | 0 |

## `iporders`

- Document: 124; rỗng: 0; `_id` ObjectId hợp lệ: 124; không phải ObjectId/missing: 0.
- Phần tử mảng: 2088; reference candidate: 4266; file/URL candidate: 15.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 124 | 124 | 0 | 0 |
| `_id` | ObjectId | 124 | 124 | 0 | 0 |
| `completedAt` | DateTime, Null | 14 | 14 | 5 | 110 |
| `createdAt` | DateTime | 124 | 124 | 0 | 0 |
| `images` | Array | 15 | 15 | 0 | 109 |
| `images[]` | String | 17 | 7 | 0 | 117 |
| `note` | String | 10 | 10 | 0 | 114 |
| `orderName` | String | 124 | 124 | 0 | 0 |
| `productList` | Array | 124 | 124 | 0 | 0 |
| `productList[]` | Document | 2071 | 122 | 0 | 2 |
| `productList[]._id` | ObjectId | 2071 | 122 | 0 | 2 |
| `productList[].note` | String | 2071 | 122 | 0 | 2 |
| `productList[].price` | String | 2071 | 122 | 0 | 2 |
| `productList[].productId` | String | 2071 | 122 | 0 | 2 |
| `productList[].quantity` | Int32 | 2071 | 122 | 0 | 2 |
| `productList[].quantityRe` | Int32 | 2071 | 122 | 0 | 2 |
| `productList[].status` | Boolean | 2071 | 122 | 0 | 2 |
| `productList[].stockAppliedQuantity` | Int32 | 31 | 7 | 0 | 117 |
| `productList[].unit` | String | 2071 | 122 | 0 | 2 |
| `productList[].vat` | String | 154 | 16 | 0 | 108 |
| `status` | Boolean | 124 | 124 | 0 | 0 |
| `total` | String | 124 | 124 | 0 | 0 |
| `updatedAt` | DateTime | 124 | 124 | 0 | 0 |
| `userName` | String | 124 | 124 | 0 | 0 |

## `manages`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 121; reference candidate: 21; file/URL candidate: 23.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |
| `displayPartners` | Boolean | 1 | 1 | 0 | 0 |
| `footerContent` | Document | 1 | 1 | 0 | 0 |
| `footerContent.address` | String | 1 | 1 | 0 | 0 |
| `footerContent.description` | String | 1 | 1 | 0 | 0 |
| `footerContent.email` | String | 1 | 1 | 0 | 0 |
| `footerContent.logo` | String | 1 | 1 | 0 | 0 |
| `footerContent.phone` | String | 1 | 1 | 0 | 0 |
| `highestRatingUrl` | String | 1 | 1 | 0 | 0 |
| `homeCategoryConfig` | Document | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.configured` | Boolean | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.items` | Array | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.items[]` | Document | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].icon` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].id` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].image` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].label` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].link` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].showQuick` | Boolean | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].showSidebar` | Boolean | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.items[].type` | String | 9 | 1 | 0 | 0 |
| `homeCategoryConfig.showQuickCategories` | Boolean | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.showSidebar` | Boolean | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.sidebarTitle` | String | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.sidebarTitleTranslations` | Document | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.sidebarTitleTranslations.en` | String | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.sidebarTitleTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `homeCategoryConfig.sidebarTitleTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `introduction` | String | 1 | 1 | 0 | 0 |
| `introductionTranslations` | Document | 1 | 1 | 0 | 0 |
| `introductionTranslations.en` | String | 1 | 1 | 0 | 0 |
| `introductionTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `introductionTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `mainPolicy` | String | 1 | 1 | 0 | 0 |
| `newProductUrl` | String | 1 | 1 | 0 | 0 |
| `overViewImg` | Array | 1 | 1 | 0 | 0 |
| `overViewImg[]` | String | 1 | 1 | 0 | 0 |
| `partners` | Array | 1 | 1 | 0 | 0 |
| `partners[]` | String | 9 | 1 | 0 | 0 |
| `policies` | Array | 1 | 1 | 0 | 0 |
| `policies[]` | Document | 4 | 1 | 0 | 0 |
| `policies[].key` | String | 4 | 1 | 0 | 0 |
| `policies[].sections` | Array | 4 | 1 | 0 | 0 |
| `policies[].sections[]` | Document | 20 | 1 | 0 | 0 |
| `policies[].sections[].content` | String | 20 | 1 | 0 | 0 |
| `policies[].sections[].title` | String | 20 | 1 | 0 | 0 |
| `policies[].summary` | String | 4 | 1 | 0 | 0 |
| `policies[].title` | String | 4 | 1 | 0 | 0 |
| `policies[].translations` | Document | 4 | 1 | 0 | 0 |
| `policies[].translations.en` | Document | 4 | 1 | 0 | 0 |
| `policies[].translations.en.sections` | Array | 4 | 1 | 0 | 0 |
| `policies[].translations.en.sections[]` | Document | 20 | 1 | 0 | 0 |
| `policies[].translations.en.sections[].content` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.en.sections[].title` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.en.summary` | String | 4 | 1 | 0 | 0 |
| `policies[].translations.en.title` | String | 4 | 1 | 0 | 0 |
| `policies[].translations.vi` | Document | 4 | 1 | 0 | 0 |
| `policies[].translations.vi.sections` | Array | 4 | 1 | 0 | 0 |
| `policies[].translations.vi.sections[]` | Document | 20 | 1 | 0 | 0 |
| `policies[].translations.vi.sections[].content` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.vi.sections[].title` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.vi.summary` | String | 4 | 1 | 0 | 0 |
| `policies[].translations.vi.title` | String | 4 | 1 | 0 | 0 |
| `policies[].translations.zh` | Document | 4 | 1 | 0 | 0 |
| `policies[].translations.zh.sections` | Array | 4 | 1 | 0 | 0 |
| `policies[].translations.zh.sections[]` | Document | 20 | 1 | 0 | 0 |
| `policies[].translations.zh.sections[].content` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.zh.sections[].title` | String | 20 | 1 | 0 | 0 |
| `policies[].translations.zh.summary` | String | 4 | 1 | 0 | 0 |
| `policies[].translations.zh.title` | String | 4 | 1 | 0 | 0 |
| `policies[].updatedAt` | DateTime | 4 | 1 | 0 | 0 |
| `section1` | Document | 1 | 1 | 0 | 0 |
| `section1.display` | Boolean | 1 | 1 | 0 | 0 |
| `section1.name` | String | 1 | 1 | 0 | 0 |
| `section1.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section1.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section1.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section1.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section1.productId` | Array | 1 | 1 | 0 | 0 |
| `section1.productId[]` | String | 11 | 1 | 0 | 0 |
| `section10` | Document | 1 | 1 | 0 | 0 |
| `section10.display` | Boolean | 1 | 1 | 0 | 0 |
| `section10.image` | String | 1 | 1 | 0 | 0 |
| `section10.link` | String | 1 | 1 | 0 | 0 |
| `section10.name` | String | 1 | 1 | 0 | 0 |
| `section10.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section10.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section10.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section10.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section10.productId` | Array | 1 | 1 | 0 | 0 |
| `section11` | Document | 1 | 1 | 0 | 0 |
| `section11.display` | Boolean | 1 | 1 | 0 | 0 |
| `section11.image` | String | 1 | 1 | 0 | 0 |
| `section11.link` | String | 1 | 1 | 0 | 0 |
| `section11.name` | String | 1 | 1 | 0 | 0 |
| `section11.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section11.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section11.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section11.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section11.productId` | Array | 1 | 1 | 0 | 0 |
| `section2` | Document | 1 | 1 | 0 | 0 |
| `section2.display` | Boolean | 1 | 1 | 0 | 0 |
| `section2.image` | String | 1 | 1 | 0 | 0 |
| `section2.link` | String | 1 | 1 | 0 | 0 |
| `section2.name` | String | 1 | 1 | 0 | 0 |
| `section2.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section2.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section2.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section2.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section2.productId` | Array | 1 | 1 | 0 | 0 |
| `section2.productId[]` | String | 7 | 1 | 0 | 0 |
| `section3` | Document | 1 | 1 | 0 | 0 |
| `section3.display` | Boolean | 1 | 1 | 0 | 0 |
| `section3.image` | String | 1 | 1 | 0 | 0 |
| `section3.link` | String | 1 | 1 | 0 | 0 |
| `section3.name` | String | 1 | 1 | 0 | 0 |
| `section3.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section3.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section3.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section3.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section3.productId` | Array | 1 | 1 | 0 | 0 |
| `section4` | Document | 1 | 1 | 0 | 0 |
| `section4.display` | Boolean | 1 | 1 | 0 | 0 |
| `section4.image` | String | 1 | 1 | 0 | 0 |
| `section4.link` | String | 1 | 1 | 0 | 0 |
| `section4.name` | String | 1 | 1 | 0 | 0 |
| `section4.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section4.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section4.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section4.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section4.productId` | Array | 1 | 1 | 0 | 0 |
| `section5` | Document | 1 | 1 | 0 | 0 |
| `section5.display` | Boolean | 1 | 1 | 0 | 0 |
| `section5.image` | String | 1 | 1 | 0 | 0 |
| `section5.link` | String | 1 | 1 | 0 | 0 |
| `section5.name` | String | 1 | 1 | 0 | 0 |
| `section5.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section5.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section5.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section5.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section5.productId` | Array | 1 | 1 | 0 | 0 |
| `section6` | Document | 1 | 1 | 0 | 0 |
| `section6.display` | Boolean | 1 | 1 | 0 | 0 |
| `section6.image` | String | 1 | 1 | 0 | 0 |
| `section6.link` | String | 1 | 1 | 0 | 0 |
| `section6.name` | String | 1 | 1 | 0 | 0 |
| `section6.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section6.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section6.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section6.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section6.productId` | Array | 1 | 1 | 0 | 0 |
| `section7` | Document | 1 | 1 | 0 | 0 |
| `section7.display` | Boolean | 1 | 1 | 0 | 0 |
| `section7.image` | String | 1 | 1 | 0 | 0 |
| `section7.link` | String | 1 | 1 | 0 | 0 |
| `section7.name` | String | 1 | 1 | 0 | 0 |
| `section7.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section7.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section7.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section7.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section7.productId` | Array | 1 | 1 | 0 | 0 |
| `section8` | Document | 1 | 1 | 0 | 0 |
| `section8.display` | Boolean | 1 | 1 | 0 | 0 |
| `section8.image` | String | 1 | 1 | 0 | 0 |
| `section8.link` | String | 1 | 1 | 0 | 0 |
| `section8.name` | String | 1 | 1 | 0 | 0 |
| `section8.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section8.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section8.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section8.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section8.productId` | Array | 1 | 1 | 0 | 0 |
| `section9` | Document | 1 | 1 | 0 | 0 |
| `section9.display` | Boolean | 1 | 1 | 0 | 0 |
| `section9.image` | String | 1 | 1 | 0 | 0 |
| `section9.link` | String | 1 | 1 | 0 | 0 |
| `section9.name` | String | 1 | 1 | 0 | 0 |
| `section9.nameTranslations` | Document | 1 | 1 | 0 | 0 |
| `section9.nameTranslations.en` | String | 1 | 1 | 0 | 0 |
| `section9.nameTranslations.vi` | String | 1 | 1 | 0 | 0 |
| `section9.nameTranslations.zh` | String | 1 | 1 | 0 | 0 |
| `section9.productId` | Array | 1 | 1 | 0 | 0 |
| `topPurchaseUrl` | String | 1 | 1 | 0 | 0 |

## `orders`

- Document: 37; rỗng: 0; `_id` ObjectId hợp lệ: 37; không phải ObjectId/missing: 0.
- Phần tử mảng: 52; reference candidate: 141; file/URL candidate: 11.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 37 | 37 | 0 | 0 |
| `_id` | ObjectId | 37 | 37 | 0 | 0 |
| `cartItems` | Array | 37 | 37 | 0 | 0 |
| `cartItems[]` | Document | 52 | 35 | 0 | 2 |
| `cartItems[]._id` | ObjectId | 52 | 35 | 0 | 2 |
| `cartItems[].note` | String | 1 | 1 | 0 | 36 |
| `cartItems[].price` | String | 1 | 1 | 0 | 36 |
| `cartItems[].productId` | String | 52 | 35 | 0 | 2 |
| `cartItems[].quantity` | Int32 | 52 | 35 | 0 | 2 |
| `cartItems[].status` | Boolean | 1 | 1 | 0 | 36 |
| `cartItems[].unit` | String | 1 | 1 | 0 | 36 |
| `cartItems[].variantIndex` | Int32 | 52 | 35 | 0 | 2 |
| `completedAt` | DateTime, Null | 14 | 14 | 10 | 23 |
| `createdAt` | DateTime | 37 | 37 | 0 | 0 |
| `images` | Array | 11 | 11 | 0 | 26 |
| `orderCode` | String | 37 | 37 | 0 | 0 |
| `orderName` | String | 3 | 3 | 0 | 34 |
| `payment` | Boolean | 37 | 37 | 0 | 0 |
| `state` | String | 37 | 37 | 0 | 0 |
| `status` | String | 37 | 37 | 0 | 0 |
| `total` | Int32 | 37 | 37 | 0 | 0 |
| `updatedAt` | DateTime | 37 | 37 | 0 | 0 |
| `userName` | String | 35 | 35 | 0 | 2 |
| `userPhone` | String | 37 | 37 | 0 | 0 |

## `products`

- Document: 316; rỗng: 0; `_id` ObjectId hợp lệ: 316; không phải ObjectId/missing: 0.
- Phần tử mảng: 318; reference candidate: 1229; file/URL candidate: 318.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 316 | 316 | 0 | 0 |
| `_id` | ObjectId | 316 | 316 | 0 | 0 |
| `adjusted` | Boolean | 316 | 316 | 0 | 0 |
| `advantages` | String | 316 | 316 | 0 | 0 |
| `averageReviews` | Int32 | 316 | 316 | 0 | 0 |
| `brand` | String | 316 | 316 | 0 | 0 |
| `code` | String | 301 | 301 | 0 | 15 |
| `createdAt` | DateTime | 316 | 316 | 0 | 0 |
| `description` | String | 316 | 316 | 0 | 0 |
| `display` | Boolean | 316 | 316 | 0 | 0 |
| `documents` | Array | 20 | 20 | 0 | 296 |
| `documents[]` | Document | 2 | 1 | 0 | 315 |
| `documents[]._id` | ObjectId | 2 | 1 | 0 | 315 |
| `documents[].label` | String | 2 | 1 | 0 | 315 |
| `documents[].sourceType` | String | 2 | 1 | 0 | 315 |
| `documents[].url` | String | 2 | 1 | 0 | 315 |
| `features` | String | 316 | 316 | 0 | 0 |
| `infoDoc` | Document | 279 | 279 | 0 | 37 |
| `infoDoc._id` | ObjectId | 279 | 279 | 0 | 37 |
| `infoDoc.catalog` | String | 279 | 279 | 0 | 37 |
| `infoDoc.dataSheet` | String | 279 | 279 | 0 | 37 |
| `infoDoc.manual` | String | 279 | 279 | 0 | 37 |
| `infoDoc.others` | String | 279 | 279 | 0 | 37 |
| `name` | String | 316 | 316 | 0 | 0 |
| `nameUnsigned` | String | 52 | 52 | 0 | 264 |
| `operatingMethod` | String | 316 | 316 | 0 | 0 |
| `purchaseCount` | Int32 | 316 | 316 | 0 | 0 |
| `reviewCount` | Int32 | 316 | 316 | 0 | 0 |
| `reviews` | Array | 316 | 316 | 0 | 0 |
| `section` | String | 316 | 316 | 0 | 0 |
| `solution` | String | 316 | 316 | 0 | 0 |
| `specifications` | String | 316 | 316 | 0 | 0 |
| `totalRating` | Int32 | 316 | 316 | 0 | 0 |
| `type` | String | 316 | 316 | 0 | 0 |
| `updatedAt` | DateTime | 316 | 316 | 0 | 0 |
| `value` | String | 316 | 316 | 0 | 0 |
| `variant` | Array | 316 | 316 | 0 | 0 |
| `variant[]` | Document | 316 | 316 | 0 | 0 |
| `variant[]._id` | ObjectId | 316 | 316 | 0 | 0 |
| `variant[].buttonCount` | String | 316 | 316 | 0 | 0 |
| `variant[].color` | String | 316 | 316 | 0 | 0 |
| `variant[].earn` | Int32 | 316 | 316 | 0 | 0 |
| `variant[].frame` | String | 316 | 316 | 0 | 0 |
| `variant[].imgUrl` | String | 316 | 316 | 0 | 0 |
| `variant[].importPrice` | String | 316 | 316 | 0 | 0 |
| `variant[].note` | String | 316 | 316 | 0 | 0 |
| `variant[].price` | String | 316 | 316 | 0 | 0 |
| `variant[].quantityForSale` | Int32 | 316 | 316 | 0 | 0 |
| `variant[].quantityInStorage` | Int32 | 316 | 316 | 0 | 0 |
| `variant[].shape` | String | 316 | 316 | 0 | 0 |
| `vat` | String | 102 | 102 | 0 | 214 |
| `warranty` | String | 316 | 316 | 0 | 0 |

## `sections`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 31; reference candidate: 9; file/URL candidate: 7.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `Section` | Array | 1 | 1 | 0 | 0 |
| `Section[]` | Document | 8 | 1 | 0 | 0 |
| `Section[]._id` | ObjectId | 8 | 1 | 0 | 0 |
| `Section[].imgUrl` | String | 7 | 1 | 0 | 0 |
| `Section[].name` | String | 8 | 1 | 0 | 0 |
| `Section[].value` | Array | 8 | 1 | 0 | 0 |
| `Section[].value[]` | String | 23 | 1 | 0 | 0 |
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |

## `stations`

- Document: 5; rỗng: 0; `_id` ObjectId hợp lệ: 5; không phải ObjectId/missing: 0.
- Phần tử mảng: 188; reference candidate: 10; file/URL candidate: 4.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 5 | 5 | 0 | 0 |
| `_id` | ObjectId | 5 | 5 | 0 | 0 |
| `allowPublicSignup` | Boolean | 2 | 2 | 0 | 3 |
| `imgUrl` | String | 4 | 4 | 0 | 1 |
| `location` | String | 5 | 5 | 0 | 0 |
| `productId` | Array | 5 | 5 | 0 | 0 |
| `productId[]` | String | 188 | 4 | 0 | 1 |
| `stationCode` | String | 5 | 5 | 0 | 0 |
| `stationName` | String | 5 | 5 | 0 | 0 |

## `storagehistories`

- Document: 528; rỗng: 0; `_id` ObjectId hợp lệ: 528; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 1576; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 528 | 528 | 0 | 0 |
| `_id` | ObjectId | 528 | 528 | 0 | 0 |
| `createdAt` | DateTime | 528 | 528 | 0 | 0 |
| `isAIScan` | Boolean | 247 | 247 | 0 | 281 |
| `note` | String | 127 | 127 | 0 | 401 |
| `orderId` | String | 520 | 520 | 0 | 8 |
| `orderName` | String | 520 | 520 | 0 | 8 |
| `productId` | ObjectId | 528 | 528 | 0 | 0 |
| `productName` | String | 528 | 528 | 0 | 0 |
| `quantity` | Int32 | 528 | 528 | 0 | 0 |
| `source` | String | 13 | 13 | 0 | 515 |
| `updatedAt` | DateTime | 528 | 528 | 0 | 0 |
| `userName` | String | 528 | 528 | 0 | 0 |

## `telegramconfigs`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 2; reference candidate: 3; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |
| `createdAt` | DateTime | 1 | 1 | 0 | 0 |
| `enabled` | Boolean | 1 | 1 | 0 | 0 |
| `recipients` | Array | 1 | 1 | 0 | 0 |
| `recipients[]` | Document | 1 | 1 | 0 | 0 |
| `recipients[]._id` | ObjectId | 1 | 1 | 0 | 0 |
| `recipients[].chatId` | String | 1 | 1 | 0 | 0 |
| `recipients[].enabled` | Boolean | 1 | 1 | 0 | 0 |
| `recipients[].label` | String | 1 | 1 | 0 | 0 |
| `recipients[].notifyTypes` | Array | 1 | 1 | 0 | 0 |
| `recipients[].notifyTypes[]` | String | 1 | 1 | 0 | 0 |
| `recipients[].type` | String | 1 | 1 | 0 | 0 |
| `updatedAt` | DateTime | 1 | 1 | 0 | 0 |

## `types`

- Document: 31; rỗng: 0; `_id` ObjectId hợp lệ: 31; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 31; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `Type` | String | 31 | 31 | 0 | 0 |
| `__v` | Int32 | 31 | 31 | 0 | 0 |
| `_id` | ObjectId | 31 | 31 | 0 | 0 |
| `icon` | String | 1 | 1 | 0 | 30 |
| `updatedAt` | DateTime | 1 | 1 | 0 | 30 |

## `users`

- Document: 16; rỗng: 0; `_id` ObjectId hợp lệ: 16; không phải ObjectId/missing: 0.
- Phần tử mảng: 175; reference candidate: 98; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 16 | 16 | 0 | 0 |
| `_id` | ObjectId | 16 | 16 | 0 | 0 |
| `addresses` | Array | 9 | 9 | 0 | 7 |
| `addresses[]` | Document | 2 | 1 | 0 | 15 |
| `addresses[]._id` | ObjectId | 2 | 1 | 0 | 15 |
| `addresses[].addressDetail` | String | 2 | 1 | 0 | 15 |
| `addresses[].isDefault` | Boolean | 2 | 1 | 0 | 15 |
| `addresses[].label` | String | 2 | 1 | 0 | 15 |
| `addresses[].receiverName` | String | 2 | 1 | 0 | 15 |
| `addresses[].receiverPhone` | String | 2 | 1 | 0 | 15 |
| `cart` | Array | 16 | 16 | 0 | 0 |
| `cart[]` | Document | 1 | 1 | 0 | 15 |
| `cart[]._id` | ObjectId | 1 | 1 | 0 | 15 |
| `cart[].productId` | String | 1 | 1 | 0 | 15 |
| `cart[].quantity` | Int32 | 1 | 1 | 0 | 15 |
| `cart[].status` | Boolean | 1 | 1 | 0 | 15 |
| `cart[].variantIndex` | Int32 | 1 | 1 | 0 | 15 |
| `email` | String | 6 | 6 | 0 | 10 |
| `functions` | Array | 16 | 16 | 0 | 0 |
| `functions[]` | String | 20 | 5 | 0 | 11 |
| `logInString` | String | 9 | 9 | 0 | 7 |
| `name` | String | 16 | 16 | 0 | 0 |
| `orderTemplate` | Array | 16 | 16 | 0 | 0 |
| `orderTemplate[]` | Document | 10 | 2 | 0 | 14 |
| `orderTemplate[]._id` | ObjectId | 10 | 2 | 0 | 14 |
| `orderTemplate[].displayName` | String | 10 | 2 | 0 | 14 |
| `orderTemplate[].note` | String | 4 | 2 | 0 | 14 |
| `orderTemplate[].products` | Array | 10 | 2 | 0 | 14 |
| `orderTemplate[].products[]` | Document | 27 | 2 | 0 | 14 |
| `orderTemplate[].products[]._id` | ObjectId | 27 | 2 | 0 | 14 |
| `orderTemplate[].products[].productId` | String | 27 | 2 | 0 | 14 |
| `orderTemplate[].products[].quantity` | Int32 | 27 | 2 | 0 | 14 |
| `password` | String | 16 | 16 | 0 | 0 |
| `passwordChangedAt` | DateTime | 1 | 1 | 0 | 15 |
| `permissions` | Array | 16 | 16 | 0 | 0 |
| `permissions[]` | String | 109 | 8 | 0 | 8 |
| `phone` | String | 16 | 16 | 0 | 0 |
| `role` | String | 16 | 16 | 0 | 0 |
| `station` | Array | 14 | 14 | 0 | 2 |
| `station[]` | String | 6 | 3 | 0 | 13 |

## `voicevocabs`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 259; reference candidate: 1; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |
| `brandAliases` | Array | 1 | 1 | 0 | 0 |
| `brandAliases[]` | Document | 24 | 1 | 0 | 0 |
| `brandAliases[].aliases` | Array | 24 | 1 | 0 | 0 |
| `brandAliases[].aliases[]` | String | 57 | 1 | 0 | 0 |
| `brandAliases[].name` | String | 24 | 1 | 0 | 0 |
| `brands` | Array | 1 | 1 | 0 | 0 |
| `brands[]` | String | 24 | 1 | 0 | 0 |
| `codeMap` | Array | 1 | 1 | 0 | 0 |
| `codeMap[]` | Document | 5 | 1 | 0 | 0 |
| `codeMap[].brand` | Null, String | 5 | 1 | 1 | 0 |
| `codeMap[].code` | String | 5 | 1 | 0 | 0 |
| `codeMap[].compact` | String | 5 | 1 | 0 | 0 |
| `codeMap[].keyword` | String | 5 | 1 | 0 | 0 |
| `codeMap[].patterns` | Array | 5 | 1 | 0 | 0 |
| `codeMap[].patterns[]` | String | 8 | 1 | 0 | 0 |
| `codeMap[].type` | Null, String | 5 | 1 | 1 | 0 |
| `createdAt` | DateTime | 1 | 1 | 0 | 0 |
| `intentAliases` | Array | 1 | 1 | 0 | 0 |
| `intentAliases[]` | Document | 4 | 1 | 0 | 0 |
| `intentAliases[].aliases` | Array | 4 | 1 | 0 | 0 |
| `intentAliases[].aliases[]` | String | 32 | 1 | 0 | 0 |
| `intentAliases[].intent` | String | 4 | 1 | 0 | 0 |
| `intentAliases[].label` | String | 4 | 1 | 0 | 0 |
| `stopwords` | Array | 1 | 1 | 0 | 0 |
| `stopwords[]` | String | 31 | 1 | 0 | 0 |
| `typeAliases` | Array | 1 | 1 | 0 | 0 |
| `typeAliases[]` | Document | 13 | 1 | 0 | 0 |
| `typeAliases[].aliases` | Array | 13 | 1 | 0 | 0 |
| `typeAliases[].aliases[]` | String | 37 | 1 | 0 | 0 |
| `typeAliases[].keyword` | String | 13 | 1 | 0 | 0 |
| `typeAliases[].type` | String | 13 | 1 | 0 | 0 |
| `types` | Array | 1 | 1 | 0 | 0 |
| `types[]` | String | 24 | 1 | 0 | 0 |
| `updatedAt` | DateTime | 1 | 1 | 0 | 0 |

## `zaloconfigs`

- Document: 1; rỗng: 0; `_id` ObjectId hợp lệ: 1; không phải ObjectId/missing: 0.
- Phần tử mảng: 0; reference candidate: 4; file/URL candidate: 0.
- Reference resolve và file tồn tại/chạy ngoài storage root: chưa xác minh trong profile BSON vì chưa có resolver/root file được phê duyệt.

| Field path | Kiểu BSON | Số giá trị | Số document có path | Null | Missing ở root-document |
|---|---|---:|---:|---:|---:|
| `__v` | Int32 | 1 | 1 | 0 | 0 |
| `_id` | ObjectId | 1 | 1 | 0 | 0 |
| `accessToken` | String | 1 | 1 | 0 | 0 |
| `appId` | String | 1 | 1 | 0 | 0 |
| `createdAt` | DateTime | 1 | 1 | 0 | 0 |
| `expiresAt` | Null | 1 | 1 | 1 | 0 |
| `oaId` | String | 1 | 1 | 0 | 0 |
| `recipientUserId` | String | 1 | 1 | 0 | 0 |
| `refreshToken` | String | 1 | 1 | 0 | 0 |
| `secretKey` | String | 1 | 1 | 0 | 0 |
| `updatedAt` | DateTime | 1 | 1 | 0 | 0 |
