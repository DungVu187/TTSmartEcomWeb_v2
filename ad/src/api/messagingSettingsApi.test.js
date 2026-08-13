import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { beforeEach, describe, expect, it, vi } from "vitest";

const apiFetchMock = vi.hoisted(() => vi.fn());

vi.mock("./httpClient", () => ({
  apiFetch: apiFetchMock,
}));

import {
  createTelegramRecipient,
  deleteTelegramRecipient,
  getTelegramSettings,
  getZaloAuthUrl,
  getZaloSettings,
  saveZaloSettings,
  sendTelegramTestMessage,
  updateTelegramRecipient,
  updateTelegramSettings,
} from "./messagingSettingsApi";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const jsonHeaders = { "Content-Type": "application/json" };
const authHeaders = {
  "auth-token": "legacy-token",
  "Content-Type": "application/json",
};

describe("messagingSettingsApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiFetchMock.mockResolvedValue({ ok: true, status: 200 });
  });

  it("maps Telegram settings and recipient contracts", async () => {
    const recipient = {
      label: "HN",
      chatId: "-1001",
      type: "group",
      enabled: true,
      notifyTypes: ["new_order"],
    };

    await getTelegramSettings();
    await updateTelegramSettings(true);
    await createTelegramRecipient(recipient);
    await updateTelegramRecipient("recipient-1", recipient);
    await deleteTelegramRecipient("recipient-1");
    await sendTelegramTestMessage("-1001");

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/telegram/settings", {
      headers: jsonHeaders,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/telegram/settings", {
      method: "PUT",
      headers: jsonHeaders,
      json: { enabled: true },
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/telegram/recipients", {
      method: "POST",
      headers: jsonHeaders,
      json: recipient,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      4,
      "/telegram/recipients/recipient-1",
      { method: "PUT", headers: jsonHeaders, json: recipient },
    );
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      5,
      "/telegram/recipients/recipient-1",
      { method: "DELETE", headers: jsonHeaders },
    );
    expect(apiFetchMock).toHaveBeenNthCalledWith(6, "/telegram/test", {
      method: "POST",
      headers: jsonHeaders,
      json: { chatId: "-1001" },
    });
  });

  it("keeps legacy Zalo auth header and JSON payload contracts", async () => {
    const settings = { appId: "app-1", oaId: "oa-1", secretKey: "secret" };

    await getZaloSettings("legacy-token");
    await saveZaloSettings("legacy-token", settings);
    await getZaloAuthUrl("legacy-token");

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/zalo/settings", {
      headers: authHeaders,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/zalo/settings", {
      method: "POST",
      headers: authHeaders,
      json: settings,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/zalo/auth-url", {
      headers: authHeaders,
    });
  });

  it("keeps active messaging settings components free of direct transport", () => {
    for (const componentName of ["TelegramSettings.jsx", "ZaloSettings.jsx"]) {
      const source = fs.readFileSync(
        path.join(currentDirectory, "..", "components", componentName),
        "utf8",
      );
      expect(source).toContain("messagingSettingsApi");
      expect(source).not.toContain("fetch(");
      expect(source).not.toContain("VITE_API_URL");
      expect(source).not.toContain("JSON.stringify");
    }
  });
});
