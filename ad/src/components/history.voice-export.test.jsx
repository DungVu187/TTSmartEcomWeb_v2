import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import moment from "moment";
import { HistoryImport } from "./history";

const apiMocks = vi.hoisted(() => ({
  getStorageHistory: vi.fn(),
  getStorageHistoryExport: vi.fn(),
  getStorageHistoryFilterOptions: vi.fn(),
}));
const excelMocks = vi.hoisted(() => ({
  addRows: vi.fn(),
  saveAs: vi.fn(),
  writeBuffer: vi.fn(),
}));

vi.mock("../api/adminAuditApi", () => apiMocks);

vi.mock("exceljs", () => ({
  default: {
    Workbook: class WorkbookMock {
      constructor() {
        this.xlsx = { writeBuffer: excelMocks.writeBuffer };
      }

      addWorksheet() {
        return {
          addRows: excelMocks.addRows,
          eachRow: vi.fn(),
          getRow: () => ({
            eachCell: (callback) => callback({}),
          }),
        };
      }
    },
  },
}));

vi.mock("file-saver", () => ({ saveAs: excelMocks.saveAs }));

vi.mock("react-hot-toast", () => ({
  default: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

class ResizeObserverMock {
  observe() {}
  disconnect() {}
}

describe("history Voice export execution", () => {
  let today;
  let historyCreatedAt;

  beforeEach(() => {
    globalThis.ResizeObserver = ResizeObserverMock;
    sessionStorage.clear();
    vi.clearAllMocks();
    excelMocks.writeBuffer.mockResolvedValue(new ArrayBuffer(8));

    today = moment().format("YYYY-MM-DD");
    historyCreatedAt = `${today}T02:30:00.000Z`;
    const historyRow = {
      _id: "history-1",
      userName: "Admin",
      productName: "Đèn",
      orderId: "IP-001",
      orderName: "Đơn nhập hôm nay",
      quantity: 10,
      source: "order_line_manual",
      createdAt: historyCreatedAt,
    };
    apiMocks.getStorageHistory.mockResolvedValue({
      ok: true,
      json: async () => ({ history: [historyRow], totalPages: 1 }),
    });
    apiMocks.getStorageHistoryFilterOptions.mockResolvedValue({
      ok: true,
      json: async () => ({ userNames: [], orderNames: [] }),
    });
    apiMocks.getStorageHistoryExport.mockResolvedValue({
      ok: true,
      json: async () => ({ history: [historyRow], total: 1 }),
    });
  });

  it("exports today's import history after receiving a Voice command", async () => {
    sessionStorage.setItem("voiceHistoryExport", JSON.stringify({
      direction: "import",
      datePreset: "today",
      requestedAt: Date.now(),
    }));

    render(
      <MemoryRouter>
        <HistoryImport />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(apiMocks.getStorageHistoryExport).toHaveBeenCalledWith({
        direction: "import",
        startDate: today,
        endDate: today,
      });
    });
    await waitFor(() => expect(excelMocks.saveAs).toHaveBeenCalledTimes(1));

    expect(excelMocks.addRows).toHaveBeenCalledWith([
      expect.objectContaining({
        userName: "Admin",
        productName: "Đèn",
        orderName: "Đơn nhập hôm nay",
        quantity: 10,
        createdAt: moment(historyCreatedAt).format("DD/MM/YYYY HH:mm"),
      }),
    ]);
    expect(sessionStorage.getItem("voiceHistoryExport")).toBeNull();
  });

  it("uses the exact date range supplied by a Voice command", async () => {
    sessionStorage.setItem("voiceHistoryExport", JSON.stringify({
      direction: "import",
      datePreset: "custom",
      startDate: "2026-08-01",
      endDate: "2026-08-05",
      requestedAt: Date.now(),
    }));

    render(
      <MemoryRouter>
        <HistoryImport />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(apiMocks.getStorageHistoryExport).toHaveBeenCalledWith({
        direction: "import",
        startDate: "2026-08-01",
        endDate: "2026-08-05",
      });
    });
    await waitFor(() => expect(excelMocks.saveAs).toHaveBeenCalledTimes(1));
  });
});
