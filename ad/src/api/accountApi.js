import { apiFetch } from "./httpClient";

const getErrorMessage = async (response, fallback) => {
  try {
    const errorData = await response.json();
    return typeof errorData?.message === "string" && errorData.message.trim()
      ? errorData.message
      : fallback;
  } catch {
    return fallback;
  }
};

export const getAccountUsers = async () => {
  const response = await apiFetch("/users/all-users", {
    method: "GET",
    headers: { "Content-Type": "application/json" },
  });

  if (!response.ok) {
    throw new Error(
      await getErrorMessage(
        response,
        `Lỗi ${response.status}: Lấy danh sách người dùng thất bại`,
      ),
    );
  }

  return response.json();
};

export const getAccountPermissionCatalog = async () => {
  const response = await apiFetch("/users/permission-catalog", {
    method: "GET",
  });

  if (!response.ok) return null;
  return response.json();
};

export const saveAccountUser = async ({ userId, user }) => {
  const response = await apiFetch(
    userId ? `/users/${userId}/permissions` : "/users/admin-create",
    {
      method: userId ? "PUT" : "POST",
      json: user,
    },
  );

  if (!response.ok) {
    throw new Error(
      await getErrorMessage(
        response,
        `Lỗi ${response.status}: Thao tác thất bại`,
      ),
    );
  }

  return response.json();
};

export const deleteAccountUser = async (userId) => {
  const response = await apiFetch(`/users/${userId}`, {
    method: "DELETE",
  });

  if (!response.ok) {
    throw new Error(
      await getErrorMessage(
        response,
        `Lỗi ${response.status}: Xóa tài khoản thất bại`,
      ),
    );
  }
};

export const getCompanyAccounts = async (companyId) => {
  const response = await apiFetch(
    `/control-plane/companies/${encodeURIComponent(companyId)}/accounts`,
  );
  if (!response.ok) {
    throw new Error(
      await getErrorMessage(response, `Lỗi ${response.status}: Không thể tải tài khoản Company`),
    );
  }
  return response.json();
};

export const getCompanyRoles = async (companyId) => {
  const response = await apiFetch(
    `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/roles`,
  );
  if (!response.ok) {
    throw new Error(
      await getErrorMessage(response, `Lỗi ${response.status}: Không thể tải role cấp Company`),
    );
  }
  return response.json();
};

export const saveCompanyMembership = async ({ companyId, userId, userType, roleId }) => {
  const response = await apiFetch(
    `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/membership`,
    {
      method: "PUT",
      json: { userType, roleId },
    },
  );
  if (!response.ok) {
    throw new Error(
      await getErrorMessage(response, `Lỗi ${response.status}: Không thể cập nhật phạm vi Company`),
    );
  }
  return response.json();
};

export const revokeCompanyMembership = async ({ companyId, userId }) => {
  const response = await apiFetch(
    `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/membership`,
    { method: "DELETE" },
  );
  if (!response.ok) {
    throw new Error(
      await getErrorMessage(response, `Lỗi ${response.status}: Không thể thu hồi phạm vi Company`),
    );
  }
  return response.json();
};

export const setCompanyMembershipStatus = async ({ companyId, userId, isActive }) => {
  const response = await apiFetch(
    `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/status`,
    { method: "PUT", json: { isActive } },
  );
  if (!response.ok) throw new Error(await getErrorMessage(response, "Không thể cập nhật trạng thái truy cập"));
  return response.json();
};

const requestJson = async (path, options, fallback) => {
  const response = await apiFetch(path, options);
  if (!response.ok) throw new Error(await getErrorMessage(response, fallback));
  return response.json();
};

export const getPlatformCompanies = () => requestJson(
  "/control-plane/companies", undefined, "Không thể tải danh sách công ty",
);

export const getPlatformBranches = (companyId) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/branches`,
  undefined,
  "Không thể tải danh sách chi nhánh",
);

export const getFeatureSettings = ({ companyId, branchId = "" }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/features${branchId ? `?branchId=${encodeURIComponent(branchId)}` : ""}`,
  undefined,
  "Không thể tải chức năng",
);

export const setFeatureSetting = ({ companyId, branchId = "", featureId, isEnabled }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/features/${encodeURIComponent(featureId)}${branchId ? `/branches/${encodeURIComponent(branchId)}` : ""}`,
  { method: "PUT", json: { isEnabled } },
  "Không thể cập nhật chức năng",
);

export const searchPlatformUsers = (query) => requestJson(
  `/control-plane/users/search?query=${encodeURIComponent(query)}`,
  undefined,
  "Không thể tìm người dùng",
);

export const lookupCompanyUser = (identifier) => requestJson(
  `/control-plane/users/lookup?identifier=${encodeURIComponent(identifier)}`,
  undefined,
  "Không tìm thấy người dùng phù hợp",
);

export const getCompanyPermissions = (companyId) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/permissions`,
  undefined,
  "Không thể tải danh mục quyền",
);

export const saveCompanyRole = ({ companyId, roleId, name, description, scopeType, permissionIds }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/roles${roleId ? `/${encodeURIComponent(roleId)}` : ""}`,
  { method: roleId ? "PUT" : "POST", json: { name, description, scopeType, permissionIds } },
  "Không thể lưu vai trò",
);

export const getUserBranches = ({ companyId, userId }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/branches`,
  undefined,
  "Không thể tải quyền truy cập chi nhánh",
);

export const saveUserBranch = ({ companyId, userId, branchId, roleId, isPrimary = false }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/branches/${encodeURIComponent(branchId)}`,
  { method: "PUT", json: { roleId, isPrimary } },
  "Không thể cấp quyền truy cập chi nhánh",
);

export const revokeUserBranch = ({ companyId, userId, branchId }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/${encodeURIComponent(userId)}/branches/${encodeURIComponent(branchId)}`,
  { method: "DELETE" },
  "Không thể ngừng quyền truy cập chi nhánh",
);

export const getBranchUsers = ({ companyId, branchId }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/branches/${encodeURIComponent(branchId)}/users`,
  undefined,
  "Không thể tải người dùng chi nhánh",
);

export const getBranchRoles = ({ companyId, branchId }) => requestJson(
  `/control-plane/companies/${encodeURIComponent(companyId)}/accounts/branches/${encodeURIComponent(branchId)}/roles`,
  undefined,
  "Không thể tải vai trò chi nhánh",
);
