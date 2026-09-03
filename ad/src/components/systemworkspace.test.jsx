import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

vi.mock("../context/permissioncontext", () => ({
  usePermissions: () => ({
    profile: { name: "Super Admin", isPlatformSuperAdmin: true },
    scope: { companyId: "", branchId: "" },
  }),
}));

vi.mock("../api/adminScope", () => ({ clearAdminScope: vi.fn() }));

import SystemWorkspace from "./systemworkspace";

describe("SystemWorkspace", () => {
  it("renders the basic Platform SuperAdmin workspace without fabricated metrics", () => {
    render(<MemoryRouter><SystemWorkspace /></MemoryRouter>);

    expect(screen.getByTestId("system-workspace")).toBeInTheDocument();
    expect(screen.getByText("Tổng quan hệ thống", { selector: "h1, h2, h3, h4, h5, h6" })).toBeInTheDocument();
    expect(screen.getByText("Công ty & Chi nhánh")).toBeInTheDocument();
    expect(screen.getByText("Người dùng & Vai trò")).toBeInTheDocument();
    expect(screen.getByText("Ứng dụng & Dịch vụ")).toBeInTheDocument();
    expect(screen.getAllByText("—")).toHaveLength(4);
    expect(screen.getAllByText("Chưa kết nối dữ liệu thống kê")).toHaveLength(4);
  });

  it("filters the system module catalogue locally", () => {
    render(<MemoryRouter><SystemWorkspace section="applications" /></MemoryRouter>);

    fireEvent.change(screen.getByPlaceholderText("Tìm trong quản trị hệ thống..."), {
      target: { value: "sức khỏe" },
    });

    expect(screen.getByText("Giám sát & Sức khỏe")).toBeInTheDocument();
    expect(screen.queryByText("Công ty & Chi nhánh")).not.toBeInTheDocument();
  });
});
