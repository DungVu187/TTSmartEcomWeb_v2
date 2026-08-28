import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { clearAdminScope, getAdminScope, setAdminScope } from "./adminScope";

describe("adminScope", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    window.localStorage.clear();
  });

  it("stores only company and branch identifiers", () => {
    setAdminScope({ companyId: "company-a", branchId: "branch-hn", ignored: "value" });

    expect(getAdminScope()).toEqual({ companyId: "company-a", branchId: "branch-hn" });
  });

  it("clears the current scope on logout", () => {
    setAdminScope({ companyId: "company-a", branchId: "branch-hn" });
    clearAdminScope();

    expect(getAdminScope()).toEqual({ companyId: "", branchId: "" });
  });
});
