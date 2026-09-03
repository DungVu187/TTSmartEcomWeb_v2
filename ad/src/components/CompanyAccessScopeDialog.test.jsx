import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("react-hot-toast", () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

import CompanyAccessScopeDialog from "./CompanyAccessScopeDialog";

const COMPANY_ID = "11111111-1111-1111-1111-111111111111";
const USER_ID = "22222222-2222-2222-2222-222222222222";
const MEMBER_ROLE_ID = "33333333-3333-3333-3333-333333333333";
const ADMIN_ROLE_ID = "44444444-4444-4444-4444-444444444444";

const profile = {
  activeCompanyId: COMPANY_ID,
  companyMemberships: [
    { companyId: COMPANY_ID, companyCode: "TTS", name: "TTSmart" },
  ],
};

const account = {
  userId: USER_ID,
  displayName: "Nhân viên A",
  userType: 3,
  roles: [{ roleId: MEMBER_ROLE_ID, name: "Thành viên", scopeType: 1 }],
};

describe("CompanyAccessScopeDialog", () => {
  let saveFails;

  beforeEach(() => {
    vi.clearAllMocks();
    saveFails = false;
    globalThis.fetch = vi.fn(async (url, options = {}) => {
      const value = String(url);
      if (value.endsWith("/accounts/roles")) {
        return {
          ok: true,
          json: async () => ({
            roles: [
              { roleId: MEMBER_ROLE_ID, name: "Thành viên", scopeType: 1 },
              { roleId: ADMIN_ROLE_ID, name: "Quản trị Company", scopeType: 1 },
              { roleId: "branch-role", name: "Role Branch", scopeType: 2 },
            ],
          }),
        };
      }
      if (value.endsWith("/accounts")) {
        return { ok: true, json: async () => ({ accounts: [account] }) };
      }
      if (options.method === "PUT") {
        return saveFails
          ? { ok: false, status: 409, json: async () => ({ message: "Tài khoản legacy Operational chưa phải Control Plane identity." }) }
          : { ok: true, json: async () => ({ message: "Đã cập nhật", account }) };
      }
      if (options.method === "DELETE") {
        return { ok: true, json: async () => ({ message: "Đã thu hồi", changed: true }) };
      }
      return { ok: false, status: 404, json: async () => ({}) };
    });
  });

  it("hiển thị Company và role hiện tại, rồi gửi đúng loại thành viên và Company role", async () => {
    const onClose = vi.fn();
    const onChanged = vi.fn();
    render(
      <CompanyAccessScopeDialog
        open
        user={{ userId: USER_ID, displayName: "Nhân viên A" }}
        profile={profile}
        onClose={onClose}
        onChanged={onChanged}
      />,
    );

    await waitFor(() => expect(globalThis.fetch.mock.calls.some(([url]) =>
      String(url).endsWith(`/control-plane/companies/${COMPANY_ID}/accounts`))).toBe(true));
    expect(screen.getAllByText("TTSmart").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Thành viên").length).toBeGreaterThan(0);
    expect(screen.queryByText("Role Branch")).not.toBeInTheDocument();

    fireEvent.mouseDown(screen.getByRole("combobox", { name: "Loại thành viên" }));
    fireEvent.click(await screen.findByRole("option", { name: "Quản trị viên" }));
    fireEvent.mouseDown(screen.getByRole("combobox", { name: "Role cấp Company" }));
    fireEvent.click(await screen.findByRole("option", { name: "Quản trị Company" }));
    fireEvent.click(screen.getByRole("button", { name: "Cập nhật" }));

    await waitFor(() => {
      const request = globalThis.fetch.mock.calls.find(([url, options]) =>
        String(url).endsWith(`/accounts/${USER_ID}/membership`) && options.method === "PUT");
      expect(request).toBeDefined();
      expect(JSON.parse(request[1].body)).toEqual({ userType: 2, roleId: ADMIN_ROLE_ID });
    });
    expect(onChanged).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("yêu cầu xác nhận và thu hồi đúng Company membership", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    render(
      <CompanyAccessScopeDialog
        open
        user={{ userId: USER_ID, displayName: "Nhân viên A" }}
        profile={profile}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Thu hồi" }));

    await waitFor(() => expect(globalThis.fetch.mock.calls.some(([url, options]) =>
      String(url).endsWith(`/accounts/${USER_ID}/membership`) && options.method === "DELETE")).toBe(true));
  });

  it("giữ dialog và hiển thị lỗi rõ khi tài khoản chưa phải Control Plane identity", async () => {
    saveFails = true;
    render(
      <CompanyAccessScopeDialog
        open
        user={{ _id: "507f191e810c19729de860ea", name: "Legacy User" }}
        profile={profile}
        onClose={vi.fn()}
        onChanged={vi.fn()}
      />,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Cấp quyền" }));

    expect(await screen.findByText("Tài khoản legacy Operational chưa phải Control Plane identity.")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
