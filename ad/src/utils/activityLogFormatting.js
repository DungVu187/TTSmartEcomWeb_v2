export const ACTIVITY_ACTION_LABELS = {
  create_product: "Tạo sản phẩm",
  update_product: "Sửa sản phẩm",
  delete_product: "Xóa sản phẩm",
  update_variant: "Sửa biến thể",
  update_earn: "Sửa % lợi nhuận",
  update_import_price: "Sửa giá nhập",
  toggle_display: "Ẩn/Hiện sản phẩm",
  add_variant: "Thêm biến thể",
  delete_variant: "Xóa biến thể",
  create_user: "Tạo tài khoản",
  update_user: "Sửa tài khoản",
  delete_user: "Xóa tài khoản",
  update_user_permissions: "Sửa quyền tài khoản",
  assign_user_stations: "Phân trạm cho tài khoản",
  rotate_autologin_token: "Xoay mã đăng nhập tự động",
  create_station: "Tạo trạm trộn",
  update_station: "Sửa trạm trộn",
  update_station_products: "Cập nhật sản phẩm trạm",
  delete_station: "Xóa trạm trộn",
  add_chip_attr: "Thêm thuộc tính sản phẩm",
  remove_chip_attr: "Xóa thuộc tính sản phẩm",
  create_brand: "Thêm thương hiệu",
  delete_brand: "Xóa thương hiệu",
  create_type: "Thêm loại sản phẩm",
  update_type: "Cập nhật loại sản phẩm",
  delete_type: "Xóa loại sản phẩm",
  create_section: "Thêm phân loại",
  update_section: "Sửa phân loại",
  delete_section: "Xóa phân loại",
  create_section_value: "Thêm giá trị phân loại",
  update_section_value: "Sửa giá trị phân loại",
  delete_section_value: "Xóa giá trị phân loại",
  update_settings: "Cập nhật cấu hình chung",
  update_introduction: "Sửa trang giới thiệu",
  update_policy: "Sửa trang chính sách",
  update_policies: "Cập nhật trang chính sách",
  update_homepage_section: "Sửa phần trang chủ",
  update_home_categories: "Cập nhật danh mục trang chủ",
  update_zalo_settings: "Cập nhật cấu hình Zalo OA",
  update_telegram_settings: "Cập nhật cấu hình Telegram",
  create_telegram_recipient: "Thêm người/nhóm nhận Telegram",
  update_telegram_recipient: "Sửa người/nhóm nhận Telegram",
  delete_telegram_recipient: "Xóa người/nhóm nhận Telegram",
  create_voice_vocab: "Thêm từ vựng tìm kiếm giọng nói",
  update_voice_vocab: "Sửa từ vựng tìm kiếm giọng nói",
  delete_voice_vocab: "Xóa từ vựng tìm kiếm giọng nói",
};

const VOICE_GROUP_LABELS = {
  brands: "Thương hiệu",
  types: "Loại sản phẩm",
  stopwords: "Từ dẫn (bỏ khi tìm)",
  brandAliases: "Cách đọc hãng",
  typeAliases: "Cách gọi loại",
  intentAliases: "Ý định (intent)",
  codeMap: "Mã model",
};

const FIELD_LABELS = {
  name: "Tên",
  code: "Mã sản phẩm",
  brand: "Thương hiệu",
  type: "Loại sản phẩm",
  section: "Phân loại",
  value: "Giá trị",
  warranty: "Bảo hành",
  vat: "VAT",
  solution: "Giải pháp",
  description: "Mô tả",
  features: "Tính năng",
  operatingMethod: "Phương thức hoạt động",
  advantages: "Ưu điểm",
  specifications: "Thông số kỹ thuật",
  display: "Hiển thị",
  price: "Giá bán",
  importPrice: "Giá nhập",
  earn: "% Lợi nhuận",
  note: "Ghi chú",
  color: "Màu sắc",
  shape: "Hình dạng",
  buttonCount: "Số nút",
  frame: "Khung",
  email: "Email",
  phone: "Số điện thoại",
  role: "Vai trò",
  functions: "Nhóm chức năng",
  permissions: "Quyền hạn",
  station: "Danh sách trạm",
  stationName: "Tên trạm",
  stationCode: "Mã trạm",
  allowPublicSignup: "Cho phép đăng ký",
  location: "Địa điểm",
  productId: "Danh sách sản phẩm",
  configured: "Dùng cấu hình thủ công",
  sidebarTitle: "Tiêu đề menu bên trái",
  sidebarTitleTranslations: "Tiêu đề menu bên trái",
  showSidebar: "Hiện menu bên trái",
  showQuickCategories: "Hiện danh mục ngang",
  items: "Danh sách danh mục",
  logInString: "Mã đăng nhập tự động",
  mainPolicy: "Chính sách chính",
  policies: "Danh sách chính sách",
  homeCategoryConfig: "Danh mục trang chủ",
  enabled: "Bật thông báo",
  recipients: "Danh sách người/nhóm nhận",
  "Zalo Config": "Cấu hình Zalo OA",
  Type: "Loại sản phẩm",
  ...VOICE_GROUP_LABELS,
};

const DEFAULT_PERMISSION_LABELS = {
  "product.view": "Sản phẩm - Xem",
  "product.create": "Sản phẩm - Thêm",
  "product.edit": "Sản phẩm - Sửa",
  "product.delete": "Sản phẩm - Xóa",
  "order.view": "Đơn bán hàng - Xem",
  "order.create": "Đơn bán hàng - Thêm",
  "order.edit": "Đơn bán hàng - Sửa",
  "order.delete": "Đơn bán hàng - Xóa",
  "order.excel": "Đơn bán hàng - Excel (mẫu/nhập/xuất)",
  "order.scan_ai": "Đơn bán hàng - Quét hóa đơn AI",
  "iporder.view": "Đơn nhập hàng - Xem",
  "iporder.create": "Đơn nhập hàng - Thêm",
  "iporder.edit": "Đơn nhập hàng - Sửa",
  "iporder.delete": "Đơn nhập hàng - Xóa",
  "iporder.excel": "Đơn nhập hàng - Excel (nhập/xuất)",
  "iporder.scan_ai": "Đơn nhập hàng - Quét hóa đơn AI",
  "eporder.view": "Đơn xuất hàng - Xem",
  "eporder.create": "Đơn xuất hàng - Thêm",
  "eporder.edit": "Đơn xuất hàng - Sửa",
  "eporder.delete": "Đơn xuất hàng - Xóa",
  "eporder.excel": "Đơn xuất hàng - Excel (nhập/xuất)",
  "eporder.scan_ai": "Đơn xuất hàng - Quét hóa đơn AI",
  "station.view": "Trạm - Xem",
  "station.create": "Trạm - Thêm",
  "station.edit": "Trạm - Sửa",
  "station.delete": "Trạm - Xóa",
  "customer.view": "Khách hàng - Xem",
  "customer.create": "Khách hàng - Thêm",
  "customer.edit": "Khách hàng - Sửa",
  "customer.delete": "Khách hàng - Xóa",
  "customer.assign_station": "Khách hàng - Gán trạm",
  "storefront.manage": "Giao diện ngoài (banner + hiển thị sản phẩm) - Quản lý",
  "voice.manage": "Từ vựng Voice - Quản lý",
  "account.manage": "Phân quyền - Quản lý",
  "zalo.manage": "Cấu hình Zalo - Quản lý",
  "history_import.view": "Lịch sử nhập kho - Xem",
  "history_export.view": "Lịch sử xuất kho - Xem",
  "activitylog.view": "Lịch sử hoạt động - Xem",
};

const LEGACY_FUNCTION_LABELS = {
  product_management: "Sản phẩm",
  order_management: "Đơn bán hàng",
  iporder_management: "Đơn nhập hàng",
  eporder_management: "Đơn xuất hàng",
};

const LEGACY_PERMISSION_ACTIONS = {
  read: "Xem",
  create: "Thêm",
  update: "Sửa",
  delete: "Xóa",
};

const LEGACY_PERMISSION_MODULES = {
  product: "Sản phẩm",
  order: "Đơn bán hàng",
  iporder: "Đơn nhập hàng",
  eporder: "Đơn xuất hàng",
  station: "Trạm",
  customer: "Khách hàng",
};

const ROLE_LABELS = {
  superadmin: "Super Admin",
  admin: "Admin",
  staff: "Nhân viên",
  customer: "Khách hàng",
};

const FIELD_LABELS_BY_LOWERCASE = Object.fromEntries(
  Object.entries(FIELD_LABELS).map(([key, label]) => [key.toLowerCase(), label]),
);

const TECHNICAL_LABELS_BY_LOWERCASE = Object.fromEntries(
  Object.entries({
    ...FIELD_LABELS,
    ...VOICE_GROUP_LABELS,
  }).map(([key, label]) => [key.toLowerCase(), label]),
);

const formatLegacyPermission = (key) => {
  const match = String(key).match(/^(read|create|update|delete)_(.+)$/i);
  if (!match) return null;

  const actionLabel = LEGACY_PERMISSION_ACTIONS[match[1].toLowerCase()];
  const moduleLabel = LEGACY_PERMISSION_MODULES[match[2].toLowerCase()];
  return actionLabel && moduleLabel ? `${moduleLabel} - ${actionLabel}` : null;
};

export const buildActivityPermissionLabels = (catalog = []) => {
  const labels = { ...DEFAULT_PERMISSION_LABELS };

  catalog.forEach((moduleItem) => {
    (moduleItem.actions || []).forEach((action) => {
      labels[action.key] = `${moduleItem.label} - ${action.label}`;
    });
  });

  return labels;
};

const formatList = (value, formatter) =>
  String(value)
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean)
    .map(formatter)
    .join(", ");

const replaceReferenceIds = (value, references) =>
  String(value).replace(/\b[0-9a-f]{24}\b/gi, (id) => (
    references.products?.[id]
    || references.products?.[id.toLowerCase()]
    || references.stations?.[id]
    || references.stations?.[id.toLowerCase()]
    || id
  ));

const replaceTechnicalLabels = (value, permissionLabels) =>
  String(value).replace(/[A-Za-z][A-Za-z0-9_.]*/g, (token) => (
    permissionLabels[token]
    || permissionLabels[token.toLowerCase()]
    || TECHNICAL_LABELS_BY_LOWERCASE[token.toLowerCase()]
    || LEGACY_FUNCTION_LABELS[token.toLowerCase()]
    || formatLegacyPermission(token)
    || token
  ));

const replaceVoiceTerminology = (value, fieldName) => {
  const normalizedField = String(fieldName || "").toLowerCase();
  const replacement = normalizedField === "brandaliases"
    ? "cách đọc"
    : normalizedField === "typealiases"
      ? "cách gọi"
      : normalizedField === "intentaliases"
        ? "từ đồng nghĩa"
        : null;

  if (!replacement) return value;
  return String(value).replace(/\balias(?:es)?\b/gi, replacement);
};

export const getActivityActionLabel = (action, actionLabels = {}) => {
  if (!action) return "Thao tác hệ thống";
  if (actionLabels[action]) return actionLabels[action];
  if (ACTIVITY_ACTION_LABELS[action]) return ACTIVITY_ACTION_LABELS[action];
  if (action.startsWith("create_") || action.startsWith("add_")) return "Thêm dữ liệu";
  if (action.startsWith("delete_") || action.startsWith("remove_")) return "Xóa dữ liệu";
  if (action.startsWith("update_") || action.startsWith("toggle_")) return "Cập nhật dữ liệu";
  return "Thao tác hệ thống";
};

export const getActivityFieldLabel = (fieldName) => {
  if (!fieldName) return "Thông tin";

  const variantMatch = fieldName.match(/^variant\[(\d+)\]\.(.+)$/i);
  if (variantMatch) {
    const subField = variantMatch[2];
    const label = FIELD_LABELS_BY_LOWERCASE[subField.toLowerCase()] || subField;
    return `Biến thể [${variantMatch[1]}] - ${label}`;
  }

  const variantIndexMatch = fieldName.match(/^variant\[(\d+)\]$/i);
  if (variantIndexMatch) return `Biến thể [${variantIndexMatch[1]}]`;

  return FIELD_LABELS[fieldName]
    || FIELD_LABELS_BY_LOWERCASE[String(fieldName).toLowerCase()]
    || fieldName;
};

export const formatActivityValue = (
  value,
  {
    fieldName = "",
    permissionLabels = DEFAULT_PERMISSION_LABELS,
    references = { products: {}, stations: {} },
  } = {},
) => {
  if (value === undefined || value === null || value === "") return "";

  const normalizedField = String(fieldName).toLowerCase();
  let formatted = String(value);

  if (normalizedField === "permissions") {
    formatted = formatList(formatted, (key) => (
      permissionLabels[key] || formatLegacyPermission(key) || key
    ));
  } else if (normalizedField === "functions") {
    formatted = formatList(formatted, (key) => LEGACY_FUNCTION_LABELS[key] || key);
  } else if (normalizedField === "role") {
    formatted = ROLE_LABELS[formatted.toLowerCase()] || formatted;
  }

  if (formatted === "true") formatted = "Bật";
  if (formatted === "false") formatted = "Tắt";

  formatted = replaceReferenceIds(formatted, references);
  formatted = replaceTechnicalLabels(formatted, permissionLabels);
  formatted = replaceVoiceTerminology(formatted, fieldName);
  return formatted;
};

export const formatActivityTarget = (value) => {
  if (!value) return "";
  const withVietnamesePrefix = String(value).replace(
    /^Từ vựng voice:\s*/i,
    "Từ vựng tìm kiếm giọng nói: ",
  );
  return replaceTechnicalLabels(withVietnamesePrefix, DEFAULT_PERMISSION_LABELS);
};
