const STORAGE_KEY = "ttsmart-admin-scope";
const CHANGE_EVENT = "ttsmart-admin-scope-change";

const parseScope = (value) => {
  if (!value) return { companyId: "", branchId: "" };
  try {
    const parsed = JSON.parse(value);
    return {
      companyId: typeof parsed?.companyId === "string" ? parsed.companyId : "",
      branchId: typeof parsed?.branchId === "string" ? parsed.branchId : "",
    };
  } catch {
    return { companyId: "", branchId: "" };
  }
};

export const getAdminScope = () => {
  if (typeof window === "undefined") return { companyId: "", branchId: "" };
  return parseScope(window.localStorage.getItem(STORAGE_KEY));
};

export const setAdminScope = ({ companyId = "", branchId = "" } = {}) => {
  const scope = {
    companyId: typeof companyId === "string" ? companyId : "",
    branchId: typeof branchId === "string" ? branchId : "",
  };
  if (typeof window !== "undefined") {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(scope));
    window.dispatchEvent(new CustomEvent(CHANGE_EVENT, { detail: scope }));
  }
  return scope;
};

export const clearAdminScope = () => {
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(STORAGE_KEY);
    window.dispatchEvent(new CustomEvent(CHANGE_EVENT, { detail: { companyId: "", branchId: "" } }));
  }
};

export const getAdminScopeChangeEvent = () => CHANGE_EVENT;

export const addScopeHeaders = (headers) => {
  const scope = getAdminScope();
  if (scope.companyId) headers.set("X-Company-Id", scope.companyId);
  else headers.delete("X-Company-Id");
  if (scope.branchId) headers.set("X-Branch-Id", scope.branchId);
  else headers.delete("X-Branch-Id");
  return headers;
};
