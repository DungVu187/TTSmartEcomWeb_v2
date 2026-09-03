import { fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";

const setAdminScope = vi.fn();

vi.mock("../api/adminScope", () => ({
  setAdminScope: (...args) => setAdminScope(...args),
}));

import WorkspaceSelector from "./workspaceSelector";

const profile = {
  isPlatformSuperAdmin: true,
  activeCompanyId: "",
  activeBranchId: "",
  companyMemberships: [
    { companyId: "company-1", companyCode: "TTSmart", name: "TTSmart" },
  ],
  branchMemberships: [
    {
      branchId: "branch-main",
      companyId: "company-1",
      branchCode: "MAIN",
      name: "Chi nhánh chính",
    },
  ],
};

describe("WorkspaceSelector", () => {
  it("yêu cầu SuperAdmin chọn lần lượt không gian, công ty và chi nhánh", () => {
    render(
      <MemoryRouter initialEntries={["/system"]}>
        <Routes>
          <Route
            path="/system"
            element={<WorkspaceSelector profile={profile} open onClose={vi.fn()} />}
          />
          <Route path="/product" element={<div>Workspace vận hành</div>} />
        </Routes>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByText("Vận hành doanh nghiệp"));

    const accessCompanyButton = screen.getByRole("button", { name: "Truy cập công ty" });
    expect(accessCompanyButton).toBeDisabled();
    expect(setAdminScope).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText("TTSmart", { selector: "p" }));

    expect(screen.getByRole("button", { name: "Truy cập công ty" })).toBeEnabled();
    expect(screen.getByText("Chi nhánh chính")).toBeInTheDocument();
    expect(setAdminScope).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText("Chi nhánh chính"));
    fireEvent.click(screen.getByRole("button", { name: "Truy cập chi nhánh" }));

    expect(setAdminScope).toHaveBeenCalledWith({
      companyId: "company-1",
      branchId: "branch-main",
    });
    expect(screen.getByText("Workspace vận hành")).toBeInTheDocument();
  });

  it("chọn Company workspace thì lưu branchId rỗng để chỉ gửi X-Company-Id", () => {
    render(
      <MemoryRouter initialEntries={["/system"]}>
        <Routes>
          <Route
            path="/system"
            element={<WorkspaceSelector profile={profile} open onClose={vi.fn()} />}
          />
          <Route path="/product" element={<div>Workspace vận hành</div>} />
        </Routes>
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByText("Vận hành doanh nghiệp"));
    fireEvent.click(screen.getByText("TTSmart", { selector: "p" }));
    fireEvent.click(screen.getByRole("button", { name: "Truy cập công ty" }));

    expect(setAdminScope).toHaveBeenCalledWith({ companyId: "company-1", branchId: "" });
  });
});
