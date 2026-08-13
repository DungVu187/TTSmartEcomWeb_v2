import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import VoiceSearchFAB from "./VoiceSearchFAB";

const queryProductsByVoiceTextMock = vi.hoisted(() => vi.fn());

vi.mock("../api/voiceApi", () => ({
  queryProductsByVoice: vi.fn(),
  queryProductsByVoiceText: queryProductsByVoiceTextMock,
}));

vi.mock("../context/permissioncontext", () => ({
  usePermissions: () => ({ can: () => true }),
}));

vi.mock("react-hot-toast", () => ({
  default: {
    loading: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const LocationProbe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
};

describe("VoiceSearchFAB history export", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.clearAllMocks();
  });

  it("routes an import-history Excel command and stores its date preset", async () => {
    queryProductsByVoiceTextMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        success: 1,
        transcript: "xuất excel lịch sử nhập đơn hôm nay",
        intent: "export_history",
        historyExport: { direction: "import", datePreset: "today" },
        filters: { brand: null, type: null, code: null },
      }),
    });

    render(
      <MemoryRouter initialEntries={["/product"]}>
        <VoiceSearchFAB />
        <LocationProbe />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Chuyển chế độ nhập chữ" }));
    fireEvent.change(screen.getByRole("textbox"), {
      target: { value: "xuất excel lịch sử nhập đơn hôm nay" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Tìm kiếm" }));

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/history/import");
    });

    expect(JSON.parse(sessionStorage.getItem("voiceHistoryExport"))).toMatchObject({
      direction: "import",
      datePreset: "today",
    });
  });

  it("stores a specific date range for an export-history command", async () => {
    queryProductsByVoiceTextMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        success: 1,
        transcript: "xuất excel lịch sử xuất kho từ ngày 01/08/2026 tới ngày 05/08/2026",
        intent: "export_history",
        historyExport: {
          direction: "export",
          datePreset: "custom",
          startDate: "2026-08-01",
          endDate: "2026-08-05",
        },
        filters: { brand: null, type: null, code: null },
      }),
    });

    render(
      <MemoryRouter initialEntries={["/product"]}>
        <VoiceSearchFAB />
        <LocationProbe />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Chuyển chế độ nhập chữ" }));
    fireEvent.change(screen.getByRole("textbox"), {
      target: { value: "xuất excel lịch sử xuất kho từ ngày 01/08/2026 tới ngày 05/08/2026" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Tìm kiếm" }));

    await waitFor(() => {
      expect(screen.getByTestId("location")).toHaveTextContent("/history/export");
    });

    expect(JSON.parse(sessionStorage.getItem("voiceHistoryExport"))).toMatchObject({
      direction: "export",
      datePreset: "custom",
      startDate: "2026-08-01",
      endDate: "2026-08-05",
    });
  });
});
