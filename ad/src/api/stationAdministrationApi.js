import { apiFetch, resolveApiUrl } from "./httpClient";

const publicFetch = (path, options) => {
  const url = resolveApiUrl(path);
  return options === undefined ? fetch(url) : fetch(url, options);
};

export const getCustomerUsers = async () => {
  const response = await apiFetch("/users/customers");
  return response.json();
};

export const getStationOptions = async () => {
  const response = await apiFetch("/stations");
  return response.json();
};

export const getStationAdminList = async () => {
  const response = await apiFetch("/stations/");
  if (!response.ok) throw new Error("Lỗi khi gọi API");
  return response.json();
};

export const registerCustomer = async (customer) => {
  const response = await apiFetch("/users/register", {
    method: "POST",
    json: customer,
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Đăng ký thất bại");
  return data;
};

export const addStationToCustomer = async (userId, stationId) => {
  const response = await apiFetch(`/users/${userId}/stations`, {
    method: "POST",
    json: { stationId },
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Thêm trạm thất bại");
  return data;
};

export const replaceCustomerStations = async (phone, stations) => {
  const response = await apiFetch("/users/stations", {
    method: "PUT",
    json: { phone, stations },
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Không thể xóa trạm");
  return data;
};

export const deleteCustomer = async (userId) => {
  const response = await apiFetch(`/users/${userId}`, {
    method: "DELETE",
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Không thể xóa");
  return data;
};

export const updateCustomer = async (
  userId,
  customer,
  fallbackMessage = "Cập nhật thất bại",
) => {
  const response = await apiFetch(`/users/${userId}/permissions`, {
    method: "PUT",
    json: customer,
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || fallbackMessage);
  return data;
};

export const rotateCustomerAutoLoginToken = async (userId) => {
  const response = await apiFetch(`/users/${userId}/rotate-autologin-token`, {
    method: "POST",
  });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Xoay mã thất bại");
  return data;
};

export const createStation = async (station) => {
  const response = await apiFetch("/stations/", {
    method: "POST",
    json: station,
  });
  if (!response.ok) {
    const errorData = await response.json();
    throw new Error(errorData.error || "Không thể tạo trạm");
  }
};

export const deleteStation = async (stationId) => {
  const response = await apiFetch(`/stations/${stationId}`, {
    method: "DELETE",
  });
  if (!response.ok) {
    const errorData = await response.json();
    throw new Error(errorData.error || "Không thể xóa trạm");
  }
};

export const getStationImportOrders = async ({ type, search }) => {
  const endpoint = type === "ep" ? "eporders" : "iporders";
  const query = new URLSearchParams({ limit: "50" });
  if (search) query.set("orderName", search);

  const response = await apiFetch(`/${endpoint}/orders?${query.toString()}`);
  if (!response.ok) throw new Error("Không thể tải danh sách đơn hàng");
  const data = await response.json();
  return data.orders || [];
};

export const searchStationProducts = async ({ name, code }) => {
  const query = new URLSearchParams();
  if (name) query.set("search", name);
  if (code) query.set("code", code);
  const response = await apiFetch(`/products?${query.toString()}`);
  return response.json();
};

export const getStationByCode = async (code) => {
  const response = await apiFetch(`/stations/code/${code}`);
  if (!response.ok) {
    const errorData = await response.json();
    throw new Error(errorData.error || "Không tìm thấy trạm");
  }
  return response.json();
};

export const getStationProducts = async (ids) => {
  const response = await publicFetch("/products/fetch-by-ids", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ids }),
  });
  return response.json();
};

export const getStationProductsByCodes = async (codes) => {
  const response = await apiFetch("/products/by-codes", {
    method: "POST",
    json: { codes },
  });
  return response.json();
};

export const updateStationDetails = async (stationId, station) => {
  const response = await apiFetch(`/stations/${stationId}`, {
    method: "PUT",
    json: station,
  });
  if (!response.ok) {
    const errorData = await response.json();
    throw new Error(errorData.error || "Cập nhật thất bại");
  }
  return response.json();
};

export const updateStationProducts = async (
  stationId,
  productIds,
  { failureMessage } = {},
) => {
  const response = await apiFetch(`/stations/${stationId}/products`, {
    method: "PUT",
    json: { productId: productIds },
  });
  if (failureMessage && !response.ok) throw new Error(failureMessage);
  return response.json();
};

export const replaceStationImage = async (stationId, hasCurrentImage, file) => {
  if (hasCurrentImage) {
    await apiFetch(`/stations/${stationId}/remove-image`, {
      method: "DELETE",
    });
  }

  const formData = new FormData();
  formData.append("station", file);
  const response = await apiFetch(`/stations/${stationId}/upload-image`, {
    method: "POST",
    body: formData,
  });
  if (!response.ok) {
    const errorData = await response.json();
    throw new Error(errorData.error || "Không thể upload ảnh");
  }
  return response.json();
};
