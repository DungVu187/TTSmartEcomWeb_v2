import { describe, expect, it } from "vitest";
import { getPostLoginDestination } from "./workspaceRouting";

describe("getPostLoginDestination", () => {
  it("routes Platform SuperAdmin directly to system administration", () => {
    expect(getPostLoginDestination({ isPlatformSuperAdmin: true })).toBe("/admin/system");
  });

  it("keeps the operational landing page for other users", () => {
    expect(getPostLoginDestination({ role: "admin" }, "/admin/product")).toBe("/admin/product");
  });
});
