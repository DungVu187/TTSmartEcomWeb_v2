import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { afterAll, beforeEach, describe, expect, it, vi } from "vitest";

const apiFetchMock = vi.hoisted(() => vi.fn());
const resolveApiUrlMock = vi.hoisted(() =>
  vi.fn((pathValue) => `https://api.test${pathValue}`),
);

vi.mock("./httpClient", () => ({
  apiFetch: apiFetchMock,
  resolveApiUrl: resolveApiUrlMock,
}));

import {
  addStationToCustomer,
  createStation,
  deleteCustomer,
  deleteStation,
  getCustomerUsers,
  getStationByCode,
  getStationAdminList,
  getStationImportOrders,
  getStationOptions,
  getStationProducts,
  getStationProductsByCodes,
  registerCustomer,
  replaceStationImage,
  replaceCustomerStations,
  rotateCustomerAutoLoginToken,
  searchStationProducts,
  updateStationDetails,
  updateStationProducts,
  updateCustomer,
} from "./stationAdministrationApi";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const originalFetch = globalThis.fetch;

const createResponse = ({ ok = true, status = 200, data }) => ({
  ok,
  status,
  json: vi.fn().mockResolvedValue(data),
});

describe("stationAdministrationApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    globalThis.fetch = vi.fn();
  });

  afterAll(() => {
    globalThis.fetch = originalFetch;
  });

  it("maps station display order, product and detail reads", async () => {
    apiFetchMock
      .mockResolvedValueOnce(createResponse({ data: { orders: [{ _id: "order-1" }] } }))
      .mockResolvedValueOnce(createResponse({ data: { products: [{ _id: "p1" }] } }))
      .mockResolvedValueOnce(createResponse({ data: { _id: "station-1" } }))
      .mockResolvedValueOnce(createResponse({ data: { products: [{ _id: "p2" }] } }));
    globalThis.fetch.mockResolvedValueOnce(createResponse({
      data: { products: [{ _id: "p3" }] },
    }));

    await expect(
      getStationImportOrders({ type: "ep", search: "Order 1" }),
    ).resolves.toEqual([{ _id: "order-1" }]);
    await expect(
      searchStationProducts({ name: "PLC", code: "P1" }),
    ).resolves.toEqual({ products: [{ _id: "p1" }] });
    await expect(getStationByCode("S1")).resolves.toEqual({ _id: "station-1" });
    await expect(getStationProductsByCodes(["P1"])).resolves.toEqual({
      products: [{ _id: "p2" }],
    });
    await expect(getStationProducts(["p3"])).resolves.toEqual({
      products: [{ _id: "p3" }],
    });

    expect(apiFetchMock).toHaveBeenNthCalledWith(
      1,
      "/eporders/orders?limit=50&orderName=Order+1",
    );
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      2,
      "/products?search=PLC&code=P1",
    );
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/stations/code/S1");
    expect(apiFetchMock).toHaveBeenNthCalledWith(4, "/products/by-codes", {
      method: "POST",
      json: { codes: ["P1"] },
    });
    expect(globalThis.fetch).toHaveBeenCalledWith(
      "https://api.test/products/fetch-by-ids",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ids: ["p3"] }),
      },
    );
  });

  it("preserves station detail and product update status policies", async () => {
    const guardedFailure = createResponse({
      ok: false,
      status: 500,
      data: { error: "ignored" },
    });
    apiFetchMock
      .mockResolvedValueOnce(createResponse({ data: { _id: "station-1" } }))
      .mockResolvedValueOnce(createResponse({
        ok: false,
        status: 400,
        data: { error: "legacy response" },
      }))
      .mockResolvedValueOnce(guardedFailure);

    await expect(
      updateStationDetails("station-1", { stationName: "Station 1" }),
    ).resolves.toEqual({ _id: "station-1" });
    await expect(
      updateStationProducts("station-1", ["p1"]),
    ).resolves.toEqual({ error: "legacy response" });
    await expect(
      updateStationProducts("station-1", [], {
        failureMessage: "Không thể xóa sản phẩm",
      }),
    ).rejects.toThrow("Không thể xóa sản phẩm");
    expect(guardedFailure.json).not.toHaveBeenCalled();

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/stations/station-1", {
      method: "PUT",
      json: { stationName: "Station 1" },
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      2,
      "/stations/station-1/products",
      { method: "PUT", json: { productId: ["p1"] } },
    );
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      3,
      "/stations/station-1/products",
      { method: "PUT", json: { productId: [] } },
    );
  });

  it("preserves station image replacement transport", async () => {
    apiFetchMock
      .mockResolvedValueOnce(createResponse({ ok: false, data: {} }))
      .mockResolvedValueOnce(createResponse({
        data: { station: { _id: "station-1", imgUrl: "/station/new.webp" } },
      }))
      .mockResolvedValueOnce(createResponse({
        ok: false,
        status: 400,
        data: { error: "Image rejected" },
      }));

    const file = new File(["image"], "station.webp", { type: "image/webp" });
    await expect(replaceStationImage("station-1", true, file)).resolves.toEqual({
      station: { _id: "station-1", imgUrl: "/station/new.webp" },
    });
    await expect(replaceStationImage("station-1", false, file)).rejects.toThrow(
      "Image rejected",
    );

    expect(apiFetchMock).toHaveBeenNthCalledWith(
      1,
      "/stations/station-1/remove-image",
      { method: "DELETE" },
    );
    const uploadOptions = apiFetchMock.mock.calls[1][1];
    expect(apiFetchMock.mock.calls[1][0]).toBe(
      "/stations/station-1/upload-image",
    );
    expect(uploadOptions.method).toBe("POST");
    expect(uploadOptions.body).toBeInstanceOf(FormData);
    expect(uploadOptions.body.get("station")).toBe(file);
  });

  it("preserves the two station-list response contracts", async () => {
    const customers = [{ _id: "user-1" }];
    const stations = [{ _id: "station-1" }];
    apiFetchMock
      .mockResolvedValueOnce(createResponse({ ok: false, status: 403, data: customers }))
      .mockResolvedValueOnce(createResponse({ ok: false, status: 403, data: stations }))
      .mockResolvedValueOnce(createResponse({ ok: false, status: 500, data: {} }));

    await expect(getCustomerUsers()).resolves.toEqual(customers);
    await expect(getStationOptions()).resolves.toEqual(stations);
    await expect(getStationAdminList()).rejects.toThrow("Lỗi khi gọi API");

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/users/customers");
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/stations");
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/stations/");
  });

  it("maps customer creation and station assignment payloads", async () => {
    apiFetchMock.mockResolvedValue(createResponse({ data: { success: true } }));
    const customer = { name: "Customer", phone: "0900000001", password: "123456" };

    await registerCustomer(customer);
    await addStationToCustomer("user-1", "station-1");
    await replaceCustomerStations("0900000001", ["station-2"]);

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/users/register", {
      method: "POST",
      json: customer,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/users/user-1/stations", {
      method: "POST",
      json: { stationId: "station-1" },
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/users/stations", {
      method: "PUT",
      json: { phone: "0900000001", stations: ["station-2"] },
    });
  });

  it("maps customer delete, update and token rotation without changing payloads", async () => {
    apiFetchMock.mockResolvedValue(createResponse({ data: { logInString: "token" } }));
    const update = { name: "Updated" };

    await deleteCustomer("user-1");
    await updateCustomer("user-2", update);
    await rotateCustomerAutoLoginToken("user-3");

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/users/user-1", {
      method: "DELETE",
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/users/user-2/permissions", {
      method: "PUT",
      json: update,
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(
      3,
      "/users/user-3/rotate-autologin-token",
      { method: "POST" },
    );
  });

  it("preserves backend message errors and caller-specific update fallbacks", async () => {
    apiFetchMock
      .mockResolvedValueOnce(createResponse({
        ok: false,
        status: 400,
        data: { message: "Số điện thoại đã tồn tại" },
      }))
      .mockResolvedValueOnce(createResponse({
        ok: false,
        status: 500,
        data: {},
      }));

    await expect(registerCustomer({})).rejects.toThrow("Số điện thoại đã tồn tại");
    await expect(
      updateCustomer("user-1", { password: "123456" }, "Reset thất bại"),
    ).rejects.toThrow("Reset thất bại");
  });

  it("keeps station mutations on the backend error field", async () => {
    const successfulCreate = createResponse({ data: { ignored: true } });
    apiFetchMock
      .mockResolvedValueOnce(successfulCreate)
      .mockResolvedValueOnce(createResponse({
        ok: false,
        status: 409,
        data: { error: "Mã trạm đã tồn tại" },
      }));
    const station = { stationCode: "S1", stationName: "Station 1", location: "HN" };

    await expect(createStation(station)).resolves.toBeUndefined();
    expect(successfulCreate.json).not.toHaveBeenCalled();
    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/stations/", {
      method: "POST",
      json: station,
    });
    await expect(deleteStation("station-1")).rejects.toThrow("Mã trạm đã tồn tại");
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/stations/station-1", {
      method: "DELETE",
    });
  });

  it("keeps station administration components free of direct transport code", () => {
    for (const componentName of [
      "stationuser.jsx",
      "station.jsx",
      "stationdisplay.jsx",
    ]) {
      const source = fs.readFileSync(
        path.join(currentDirectory, "..", "components", componentName),
        "utf8",
      );
      expect(source).toContain("stationAdministrationApi");
      expect(source).not.toContain("fetch(");
      expect(source).not.toContain("VITE_API_URL");
      expect(source).not.toContain("new FormData");
    }
  });
});
