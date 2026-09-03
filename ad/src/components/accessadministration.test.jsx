import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const state = vi.hoisted(() => ({ profile: null, scope: { companyId: "", branchId: "" } }));
const api = vi.hoisted(() => ({
  getPlatformCompanies: vi.fn(), searchPlatformUsers: vi.fn(), lookupCompanyUser: vi.fn(),
  getCompanyAccounts: vi.fn(), getCompanyRoles: vi.fn(), getCompanyPermissions: vi.fn(),
  getUserBranches: vi.fn(), saveCompanyMembership: vi.fn(), revokeCompanyMembership: vi.fn(),
  saveCompanyRole: vi.fn(), saveUserBranch: vi.fn(), revokeUserBranch: vi.fn(),
  getBranchUsers: vi.fn(), getBranchRoles: vi.fn(), setCompanyMembershipStatus: vi.fn(),
}));

vi.mock("../context/permissioncontext", () => ({ usePermissions: () => state }));
vi.mock("../api/accountApi", () => api);
vi.mock("react-hot-toast", () => ({ default: { success: vi.fn(), error: vi.fn() } }));

import AccessAdministration from "./accessadministration";

const COMPANY_ID = "11111111-1111-1111-1111-111111111111";
const BRANCH_ID = "22222222-2222-2222-2222-222222222222";

describe("AccessAdministration workspace boundaries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    state.profile = { isPlatformSuperAdmin: true, companyMemberships: [] };
    state.scope = { companyId: "", branchId: "" };
    api.getPlatformCompanies.mockResolvedValue({ companies: [{ companyId: COMPANY_ID, companyCode: "TTS", name: "TTSmart" }] });
    api.getCompanyAccounts.mockResolvedValue({ accounts: [] });
    api.getCompanyRoles.mockResolvedValue({ roles: [] });
    api.getCompanyPermissions.mockResolvedValue({ permissions: [] });
    api.searchPlatformUsers.mockResolvedValue({ users: [{ userId: "33333333-3333-3333-3333-333333333333", displayName: "Nguyễn Văn A", phone: "0900000000" }] });
    api.getBranchUsers.mockResolvedValue({ users: [] });
    api.getBranchRoles.mockResolvedValue({ roles: [] });
  });

  it("SuperAdmin uses company selection and user search without showing identifiers", async () => {
    render(<AccessAdministration platform />);
    expect(screen.getByText("Vai trò: Quản trị nền tảng")).toBeInTheDocument();
    expect(screen.getByText("Phạm vi: Toàn hệ thống")).toBeInTheDocument();
    expect(screen.getByText("Quyền: Toàn quyền")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Tìm theo tên, số điện thoại hoặc email"), { target: { value: "Nguyễn" } });
    fireEvent.click(screen.getByRole("button", { name: "Tìm người dùng" }));
    expect(await screen.findByText(/Nguyễn Văn A/)).toBeInTheDocument();
    expect(screen.queryByText(/11111111-1111/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Control Plane|membership|Company scope/i)).not.toBeInTheDocument();
  });

  it("Company workspace has separate user and role tabs and hides permission codes", async () => {
    state.profile = { isPlatformSuperAdmin: false, activeCompanyId: COMPANY_ID, companyMemberships: [{ companyId: COMPANY_ID, userType: 1 }] };
    state.scope = { companyId: COMPANY_ID, branchId: "" };
    api.getCompanyRoles.mockResolvedValue({ roles: [{ roleId: "r1", name: "Nhân viên bán hàng", scopeType: 1, permissions: ["product.view"] }] });
    api.getCompanyPermissions.mockResolvedValue({ permissions: [{ permissionId: "p1", permissionCode: "product.view", name: "Xem sản phẩm", featureName: "Sản phẩm" }] });
    render(<AccessAdministration />);
    expect(await screen.findByRole("tab", { name: "Người dùng" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: "Vai trò" }));
    expect(await screen.findByText("Tạo vai trò nội bộ")).toBeInTheDocument();
    expect(screen.getAllByText("Xem sản phẩm").length).toBeGreaterThan(0);
    expect(screen.queryByText("product.view")).not.toBeInTheDocument();
  });

  it("Branch workspace shows only branch users and no company administration", async () => {
    state.profile = { isPlatformSuperAdmin: false, activeCompanyId: COMPANY_ID, activeBranchId: BRANCH_ID, companyMemberships: [] };
    state.scope = { companyId: COMPANY_ID, branchId: BRANCH_ID };
    render(<AccessAdministration />);
    expect(await screen.findByText("Người dùng chi nhánh")).toBeInTheDocument();
    await waitFor(() => expect(api.getBranchUsers).toHaveBeenCalledWith({ companyId: COMPANY_ID, branchId: BRANCH_ID }));
    expect(screen.queryByText("Cấp quyền truy cập công ty")).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Vai trò" })).not.toBeInTheDocument();
  });
});
