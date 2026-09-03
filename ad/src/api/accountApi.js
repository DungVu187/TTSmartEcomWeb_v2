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
