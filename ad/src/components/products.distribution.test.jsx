import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

const permissionState = vi.hoisted(() => ({
  permissions: new Set(),
  companyPermissions: [],
  scope: { companyId: "", branchId: "" },
  isSuperadmin: false,
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock("react-router-dom", () => ({ useNavigate: () => vi.fn() }));
vi.mock("../context/permissioncontext", () => ({
  usePermissions: () => ({
    can: (permission) => permissionState.permissions.has(permission),
    scope: permissionState.scope,
    isSuperadmin: permissionState.isSuperadmin,
    profile: {
      activeCompanyId: permissionState.scope.companyId,
      activeBranchId: permissionState.scope.branchId,
      companyMemberships: permissionState.scope.companyId
        ? [{ companyId: permissionState.scope.companyId, permissions: permissionState.companyPermissions }]
        : [],
    },
  }),
}));
vi.mock("react-hot-toast", () => ({ default: toast }));

import Products from "./products";

const COMPANY_ID = "11111111-1111-1111-1111-111111111111";
const BRANCH_ID = "22222222-2222-2222-2222-222222222222";
const PRODUCT_A = "507f191e810c19729de860ea";
const PRODUCT_B = "507f191e810c19729de860eb";

class ResizeObserverMock {
  observe() {}
  disconnect() {}
}

const products = [
  { _id: PRODUCT_A, name: "Sản phẩm A", code: "A", display: true, variant: [{}] },
  { _id: PRODUCT_B, name: "Sản phẩm B", code: "B", display: true, variant: [{}] },
];

const setupFetch = ({ assignFails = false } = {}) => {
  globalThis.fetch = vi.fn(async (url, options = {}) => {
    const value = String(url);
    if (value.includes("/products?")) {
      return { ok: true, json: async () => ({ products, total: products.length }) };
    }
    if (value.endsWith("/products/distribution/branches")) {
      return {
        ok: true,
        json: async () => ({
          branches: [{ branchId: BRANCH_ID, companyId: COMPANY_ID, branchCode: "HN", name: "Chi nhánh Hà Nội" }],
        }),
      };
    }
    if (value.endsWith("/products/distribution/assign")) {
      return assignFails
        ? { ok: false, json: async () => ({ message: "Không thể phân phối lúc này" }) }
        : { ok: true, json: async () => ({ message: "Phân phối sản phẩm thành công" }) };
    }
    if (value.endsWith("/products/distribution/revoke")) {
      return { ok: true, json: async () => ({ message: "Thu hồi phân phối sản phẩm thành công" }) };
    }
    if (value.includes("/bulk-delete")) {
      return { ok: true, json: async () => ({ message: "Đã xóa" }) };
    }
    return { ok: true, json: async () => [] };
  });
};

const selectProduct = async (name = "Sản phẩm A") => {
  const row = (await screen.findByText(name)).closest("tr");
  fireEvent.click(within(row).getAllByRole("checkbox")[0]);
};

describe("Products distribution", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    globalThis.ResizeObserver = ResizeObserverMock;
    permissionState.permissions = new Set(["product.edit"]);
    permissionState.companyPermissions = ["product.edit"];
    permissionState.scope = { companyId: COMPANY_ID, branchId: "" };
    localStorage.setItem("ttsmart-admin-scope", JSON.stringify(permissionState.scope));
    setupFetch();
  });

  it("chọn nhiều sản phẩm và checkbox tiêu đề chỉ chọn trang hiện tại", async () => {
    render(<Products />);
    await screen.findByText("Sản phẩm A");

    const headerCheckbox = screen.getAllByRole("checkbox")[0];
    fireEvent.click(headerCheckbox);

    expect(screen.getByRole("button", { name: "Phân phối (2)" })).toBeInTheDocument();
    expect(within(screen.getByText("Sản phẩm A").closest("tr")).getAllByRole("checkbox")[0]).toBeChecked();
    expect(within(screen.getByText("Sản phẩm B").closest("tr")).getAllByRole("checkbox")[0]).toBeChecked();
  });

  it("product.edit cấp Company được phân phối/thu hồi nhưng không được xóa", async () => {
    render(<Products />);
    await selectProduct();

    expect(screen.getByRole("button", { name: "Phân phối (1)" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Thu hồi (1)" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Xóa (1)" })).not.toBeInTheDocument();
  });

  it("product.delete không mặc nhiên được phân phối", async () => {
    permissionState.permissions = new Set(["product.delete"]);
    permissionState.companyPermissions = [];
    render(<Products />);
    await selectProduct();

    expect(screen.getByRole("button", { name: "Xóa (1)" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Phân phối (1)" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Thu hồi (1)" })).not.toBeInTheDocument();
  });

  it("ẩn phân phối và thu hồi trong Branch scope", async () => {
    permissionState.scope = { companyId: COMPANY_ID, branchId: BRANCH_ID };
    localStorage.setItem("ttsmart-admin-scope", JSON.stringify(permissionState.scope));
    render(<Products />);
    await selectProduct();

    expect(screen.queryByRole("button", { name: "Phân phối (1)" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Thu hồi (1)" })).not.toBeInTheDocument();
  });

  it("gửi đúng productIds, branchIds và chỉ header Company khi phân phối", async () => {
    render(<Products />);
    await selectProduct();
    fireEvent.click(screen.getByRole("button", { name: "Phân phối (1)" }));
    fireEvent.click(await screen.findByLabelText("Chi nhánh Hà Nội (HN)"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Phân phối" }));

    await waitFor(() => {
      const request = globalThis.fetch.mock.calls.find(([url]) =>
        String(url).endsWith("/products/distribution/assign"));
      expect(request).toBeDefined();
      expect(JSON.parse(request[1].body)).toEqual({ productIds: [PRODUCT_A], branchIds: [BRANCH_ID] });
      expect(request[1].headers.get("X-Company-Id")).toBe(COMPANY_ID);
      expect(request[1].headers.has("X-Branch-Id")).toBe(false);
    });
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(toast.success).toHaveBeenCalledWith("Phân phối sản phẩm thành công");
  });

  it("xác nhận thu hồi và nêu rõ lịch sử, chứng từ cũ không bị xóa", async () => {
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(true);
    render(<Products />);
    await selectProduct();
    fireEvent.click(screen.getByRole("button", { name: "Thu hồi (1)" }));

    expect(await screen.findByText(/lịch sử và chứng từ cũ không bị xóa/i)).toBeInTheDocument();
    fireEvent.click(screen.getByLabelText("Chi nhánh Hà Nội (HN)"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Thu hồi" }));

    await waitFor(() => expect(confirm).toHaveBeenCalledWith(expect.stringMatching(/Lịch sử và chứng từ cũ không bị xóa/i)));
    expect(globalThis.fetch.mock.calls.some(([url]) => String(url).endsWith("/products/distribution/revoke"))).toBe(true);
  });

  it("giữ lựa chọn và dialog khi API lỗi để người dùng thử lại", async () => {
    setupFetch({ assignFails: true });
    render(<Products />);
    await selectProduct();
    fireEvent.click(screen.getByRole("button", { name: "Phân phối (1)" }));
    fireEvent.click(await screen.findByLabelText("Chi nhánh Hà Nội (HN)"));
    fireEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Phân phối" }));

    expect(await screen.findByText("Không thể phân phối lúc này")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Phân phối (1)", hidden: true })).toBeInTheDocument();
    expect(document.querySelector('.product-data input[type="checkbox"]')).toBeChecked();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("cảnh báo rõ xóa Product Master ảnh hưởng toàn bộ chi nhánh", async () => {
    permissionState.permissions = new Set(["product.delete"]);
    permissionState.companyPermissions = [];
    const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
    render(<Products />);
    await selectProduct();
    fireEvent.click(screen.getByRole("button", { name: "Xóa (1)" }));

    expect(confirm).toHaveBeenCalledWith(expect.stringMatching(/Product Master.*toàn bộ chi nhánh/i));
  });
});
