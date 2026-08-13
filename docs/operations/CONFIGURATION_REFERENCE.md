# Tham chiếu cấu hình

Tài liệu này chỉ ghi tên key và hành vi đã đối chiếu từ source V2; không chứa giá trị bí mật. Khi dùng biến môi trường .NET, thay dấu `:` bằng `__`, ví dụ `MongoDb__ConnectionString`. Không sao chép `.env` hoặc giá trị production từ legacy.

## Cấu hình bắt buộc và tương thích legacy

| Key target | Alias legacy được V2 đọc | Mục đích và ràng buộc | Secret? |
|---|---|---|---:|
| `Jwt:Secret` | `JWT_SECRET` | Ký/xác minh cookie JWT `authToken`; tối thiểu 32 ký tự. Placeholder development phải được ghi đè. | Có |
| `Jwt:SessionHours` | Không | Thời lượng phiên; mặc định 12 giờ, hợp lệ 1–168. | Không |
| `Jwt:ClockSkewSeconds` | Không | Sai số kiểm tra thời gian JWT; mặc định 0, hợp lệ 0–300 giây. | Không |
| `MongoDb:ConnectionString` | `MONGODB_URI` | Connection string MongoDB. Key target có ưu tiên cao hơn alias. | Có |
| `MongoDb:DatabaseName` | `DB_NAME` | Tên database; chỉ nhận chữ, số, `_`, `-`. Nếu thiếu, V2 thử lấy từ connection string. | Không |
| `LegacyCompatibility:PublicSignupEnabled` | `PUBLIC_SIGNUP_ENABLED` | Bật/tắt đăng ký public; mặc định `false`. | Không |
| `ExternalServices:PublicAddress` | `ADDRESS` | Origin public của V2, dùng để tạo callback Zalo và liên kết Admin. Production phải là HTTPS hợp lệ. | Không, phụ thuộc môi trường |
| `ExternalServices:FrontendUrl` | `FRONTEND_URL` | Origin frontend dùng sau callback Zalo; production phải là HTTPS hợp lệ. | Không, phụ thuộc môi trường |
| `ExternalServices:GeminiApiKey` | `GEMINI_API_KEY` | Credential cho scan hóa đơn và voice Gemini. | Có |
| `ExternalServices:TelegramBotToken` | `TELEGRAM_BOT_TOKEN` | Credential Telegram Bot API. | Có |
| `ExternalServices:GmailUser` | `GMAIL_USER` | Tài khoản SMTP cho OTP và email thông báo đơn. | Có dữ liệu vận hành nhạy cảm |
| `ExternalServices:GmailAppPassword` | `GMAIL_APP_PASSWORD` | Credential SMTP. | Có |
| `ExternalServices:AdminNotifyEmail` | `ADMIN_NOTIFY_EMAIL` | Người nhận email đơn hàng mới. | Có dữ liệu vận hành nhạy cảm |
| `ZaloOAuth:StateSecret` | `ZALO_OAUTH_STATE_SECRET` | Khóa ký state OAuth; tối thiểu 32 byte để OAuth khả dụng. | Có |

Alias chỉ được áp dụng khi có giá trị không rỗng. Các tên legacy `AES_KEY`, `NODE_ENV`, `PORT`, `RATE_LIMIT_MAX`, `RATE_LIMIT_WINDOW_MS` và `ZALO_DEMO_MODE` không được tự động ánh xạ bởi V2; không giả định chúng có hiệu lực. Dùng cấu hình host ASP.NET Core, ví dụ `ASPNETCORE_URLS`, cho địa chỉ lắng nghe.

## HTTP, bảo mật và khả năng tương thích

| Key | Mặc định source | Ghi chú vận hành |
|---|---:|---|
| `Cors:AllowedOrigins` | Bốn origin localhost development | Allowlist chính xác cho CORS, Socket.IO và middleware chống CSRF theo origin. Không dùng wildcard khi gửi cookie. Cấu hình reverse proxy/origin thật phải được kiểm thử để đóng `SEC-H-001`. |
| `LegacyCompatibility:AdminFullAccess` | `true` | Giữ hành vi admin legacy; là quyết định tương thích cần phê duyệt, không phải mặc định bảo mật mong muốn. |
| `LegacyCompatibility:EnableApiPrefixAlias` | `true` | Duy trì cả URL gốc và alias `/api`. |
| `LegacyCompatibility:PublicSignupEnabled` | `false` | Chỉ bật sau khi chính sách đăng ký được phê duyệt. |
| `ReverseProxy:Enabled` | `false` | Chỉ bật khi API thực sự chạy sau reverse proxy đã biết. Khi bật, startup yêu cầu ít nhất một `KnownProxies` hoặc `KnownNetworks`. |
| `ReverseProxy:ForwardLimit` | `1` | Số hop forwarded-header được xử lý; hợp lệ 1–10. Giữ đúng số proxy trong topology. |
| `ReverseProxy:KnownProxies` | Mảng rỗng | Allowlist địa chỉ IP proxy chính xác. Không dùng địa chỉ client hoặc wildcard. |
| `ReverseProxy:KnownNetworks` | Mảng rỗng | Allowlist mạng proxy ở dạng CIDR; V2 dùng `KnownIPNetworks` của .NET 10. |

## Static FE/AD và upload

| Key | Mặc định source | Ghi chú vận hành |
|---|---:|---|
| `FrontendHosting:Enabled` | `true` | Cho phép API host hai bundle tĩnh. |
| `FrontendHosting:CustomerDistPath` | `../../../fe/dist` | Thư mục chứa `index.html` của FE, được phục vụ tại `/`. Có thể dùng đường dẫn tuyệt đối trong artifact. |
| `FrontendHosting:AdminDistPath` | `../../../ad/dist` | Thư mục chứa `index.html` của AD, được phục vụ dưới `/admin`. |
| `Uploads:RootPath` | `uploads` | Root upload; phải nằm trên volume bền vững, có backup và quyền tối thiểu. |
| `Uploads:ProductImageMegabytes` | `4` | Giới hạn ảnh sản phẩm. |
| `Uploads:ProductDocumentMegabytes` | `20` | Giới hạn tài liệu sản phẩm và cũng là trần multipart dùng chung hiện tại. |
| `Uploads:InvoiceMegabytes` | `5` | Giới hạn ảnh hóa đơn. |
| `Uploads:VoiceMegabytes` | `10` | Giới hạn âm thanh voice. |

Nếu bundle không tồn tại hoặc thiếu `index.html`, fallback SPA tương ứng trả 404; API không được âm thầm trả `index.html`. Invoice được phục vụ qua ranh giới có authorization, còn các root upload public phải được kiểm tra content type và quyền filesystem trong staging.

## Provider ngoài

| Key | Mặc định source | Ghi chú vận hành |
|---|---:|---|
| `ExternalServices:GeminiTimeoutSeconds` | `25` | V2 giới hạn thực tế 5–60 giây. Lỗi provider sau khi đã gọi được ánh xạ 503 có chủ đích tại boundary AI. |
| `ExternalServices:GmailSmtpHost` | `smtp.gmail.com` | Host SMTP. |
| `ExternalServices:GmailSmtpPort` | `587` | Port SMTP với TLS. |
| `ExternalServices:GmailTimeoutSeconds` | `15` | V2 giới hạn thực tế 5–60 giây. |
| `ZaloOAuth:StateLifetimeSeconds` | `300` | Hợp lệ 60–900 giây; state dùng một lần. |
| `ZaloOAuth:MaxProviderResponseBytes` | `65536` | Trần response OAuth. |
| `ZaloOAuth:MaxPendingStates` | `2048` | Trần state đang chờ trong tiến trình. |
| `ZaloOAuth:AuthorizationEndpoint` | Endpoint OAuth HTTPS của Zalo | Chỉ chấp nhận URI tuyệt đối HTTPS khi chạy production. |
| `ZaloOAuth:TokenEndpoint` | Endpoint token HTTPS của Zalo | Chỉ chấp nhận URI tuyệt đối HTTPS khi chạy production. |

Zalo OAuth, Gemini, SMTP, Telegram và pipeline notification đã có code cùng test bằng fake/boundary. Chưa provider thật nào được xác minh trong môi trường staging biệt lập; việc có key không phải bằng chứng tích hợp đã hoạt động.

## Socket.IO

Section `Realtime:SocketIo` được validate khi startup.

| Key con | Mặc định |
|---|---:|
| `PingIntervalMilliseconds` | `25000` |
| `PingTimeoutMilliseconds` | `20000` |
| `ConnectTimeoutMilliseconds` | `45000` |
| `UpgradeTimeoutMilliseconds` | `10000` |
| `SendTimeoutMilliseconds` | `5000` |
| `MaxPayloadBytes` | `1000000` |
| `MaxPacketsPerPayload` | `64` |
| `MaxQueuedPacketsPerSession` | `128` |
| `MaxQueuedBytesPerSession` | `4000000` |
| `MaxSessions` | `2048` |

V2 mount Engine.IO v4/Socket.IO v5 tại cả `/socket.io` và `/api/socket.io`. Origin được kiểm tra theo `Cors:AllowedOrigins`; reverse proxy phải hỗ trợ WebSocket upgrade, polling GET/POST và không cache transport response.

## Quy tắc cấp cấu hình

- Cấp secret qua secret manager hoặc biến môi trường của platform; không ghi vào Git, command history, log hoặc tài liệu incident.
- Xác minh sự hiện diện/độ dài của key mà không in giá trị.
- Tách database, upload root, callback và credential theo môi trường.
- Không dùng placeholder trong `appsettings.json` cho staging/production.
- Sau thay đổi cấu hình, chạy health, static FE/AD, auth/CSRF, Socket.IO và provider smoke test bằng tài khoản/dữ liệu tổng hợp.

Xem [Runbook triển khai](DEPLOYMENT_RUNBOOK.md) và [Danh mục lỗi](ERROR_CATALOG.md) trước khi bật một tích hợp.
