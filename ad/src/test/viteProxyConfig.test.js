// @vitest-environment node

import { describe, expect, it } from "vitest";
import viteConfig from "../../vite.config";

describe("Vite development proxy", () => {
  it("forwards Control Plane requests to the backend", () => {
    expect(viteConfig.server.proxy["/control-plane"]).toMatchObject({
      changeOrigin: true,
    });
  });
});
