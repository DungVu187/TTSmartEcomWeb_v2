import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";

const permissionState = {
  profile: null,
  isLoading: false,
};

vi.mock("../context/permissioncontext", () => ({
  usePermissions: () => permissionState,
}));

import SystemWorkspaceGuard from "./systemworkspaceguard";

const renderGuard = () => render(
  <MemoryRouter initialEntries={["/system"]}>
    <Routes>
      <Route
        path="/system"
        element={<SystemWorkspaceGuard><div>Quản trị hệ thống</div></SystemWorkspaceGuard>}
      />
      <Route path="/product" element={<div>Vận hành</div>} />
    </Routes>
  </MemoryRouter>,
);

describe("SystemWorkspaceGuard", () => {
  beforeEach(() => {
    permissionState.profile = null;
    permissionState.isLoading = false;
  });

  it("cho phép Platform SuperAdmin truy cập workspace hệ thống", () => {
    permissionState.profile = { isPlatformSuperAdmin: true };

    renderGuard();

    expect(screen.getByText("Quản trị hệ thống")).toBeInTheDocument();
  });

  it("chuyển người dùng khác về workspace vận hành", () => {
    permissionState.profile = { role: "admin", isPlatformSuperAdmin: false };

    renderGuard();

    expect(screen.getByText("Vận hành")).toBeInTheDocument();
    expect(screen.queryByText("Quản trị hệ thống")).not.toBeInTheDocument();
  });

  it("giữ màn hình chờ trong lúc profile đang tải", () => {
    permissionState.isLoading = true;

    const { container } = renderGuard();

    expect(container.querySelector(".MuiCircularProgress-root")).toBeInTheDocument();
  });
});
