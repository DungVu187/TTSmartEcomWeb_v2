import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { CssBaseline, ThemeProvider } from "@mui/material";
import { beforeEach, describe, expect, it, vi } from "vitest";
import theme from "../theme";
import Products from "./products";

vi.mock("react-router-dom", () => ({
  useNavigate: () => vi.fn(),
}));

vi.mock("../context/permissioncontext", () => ({
  usePermissions: () => ({ can: () => true }),
}));

vi.mock("react-hot-toast", () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

class ResizeObserverMock {
  observe() {}
  disconnect() {}
}

describe("Products runtime", () => {
  beforeEach(() => {
    globalThis.ResizeObserver = ResizeObserverMock;
    sessionStorage.clear();
    globalThis.fetch = vi.fn(async (url) => {
      if (String(url).includes("/products?")) {
        return { ok: true, json: async () => ({ products: [], total: 0 }) };
      }
      return { ok: true, json: async () => [] };
    });
  });

  it("renders the product page inside the enterprise theme", async () => {
    render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <Products />
      </ThemeProvider>,
    );

    expect(screen.getByRole("heading", { name: "Danh mục sản phẩm" })).toBeInTheDocument();
    await waitFor(() => expect(globalThis.fetch).toHaveBeenCalled());
  });

  it("shows Vietnamese messages for required product fields", async () => {
    render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <Products />
      </ThemeProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Thêm sản phẩm" }));

    const requiredFields = [
      ["name", "Vui lòng nhập tên sản phẩm."],
      ["code", "Vui lòng nhập mã sản phẩm."],
      ["vat", "Vui lòng nhập VAT."],
    ];

    requiredFields.forEach(([name, message]) => {
      const input = document.querySelector(`input[name="${name}"]`);
      fireEvent.invalid(input);
      expect(input.validationMessage).toBe(message);
      fireEvent.input(input, { target: { value: "Giá trị hợp lệ" } });
      expect(input.validationMessage).toBe("");
    });
  });

  it("creates pricing fields with blank earn using the 25 percent default", async () => {
    globalThis.fetch = vi.fn(async (url, options = {}) => {
      const requestUrl = String(url);
      if (requestUrl.includes("/products?")) {
        return { ok: true, json: async () => ({ products: [], total: 0 }) };
      }
      if (requestUrl.endsWith("/products/create") && options.method === "POST") {
        return {
          ok: true,
          status: 201,
          json: async () => ({ message: "Product created successfully" }),
        };
      }
      return { ok: true, json: async () => [] };
    });

    render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <Products />
      </ThemeProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Thêm sản phẩm" }));
    expect(screen.getByText("* là bắt buộc")).toBeInTheDocument();
    expect(screen.getByLabelText(/Thiết bị/)).toBeRequired();
    fireEvent.change(screen.getByLabelText("Giá nhập"), {
      target: { value: "100000" },
    });
    expect(screen.getByLabelText("% Lợi nhuận (Mặc định 25%)")).toHaveValue(null);
    fireEvent.click(screen.getByRole("button", { name: "Thêm" }));

    await waitFor(() => {
      const createCall = globalThis.fetch.mock.calls.find(([url]) =>
        String(url).endsWith("/products/create"),
      );
      expect(createCall).toBeDefined();
      const payload = JSON.parse(createCall[1].body);
      expect(payload.variant[0]).toMatchObject({
        importPrice: "100000",
        earn: "",
        price: "125000",
      });
    });
  });

  it("switches an existing name to update mode and submits the selected icon", async () => {
    globalThis.fetch = vi.fn(async (url, options = {}) => {
      const requestUrl = String(url);
      if (requestUrl.includes("/products?")) {
        return { ok: true, json: async () => ({ products: [], total: 0 }) };
      }
      if (requestUrl.endsWith("/products/types") && !options.method) {
        return {
          ok: true,
          json: async () => [{ _id: "type-plc", Type: "PLC", icon: "ri-tb-cpu" }],
        };
      }
      if (requestUrl.endsWith("/products/types/type-plc") && options.method === "PUT") {
        return {
          ok: true,
          json: async () => ({ _id: "type-plc", Type: "PLC", icon: "ri-tb-robot" }),
        };
      }
      return { ok: true, json: async () => [] };
    });

    render(
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <Products />
      </ThemeProvider>,
    );

    fireEvent.click(await screen.findByRole("button", { name: "Quản lý loại sản phẩm" }));
    expect(screen.getByText("Đang hiển thị 82/82 biểu tượng")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Tên loại sản phẩm"), {
      target: { value: "PLC" },
    });

    const updateButton = await screen.findByRole("button", {
      name: "Cập nhật loại sản phẩm",
    });
    fireEvent.click(screen.getByRole("button", { name: "Robot" }));
    fireEvent.click(updateButton);

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        expect.stringContaining("/products/types/type-plc"),
        expect.objectContaining({
          method: "PUT",
          body: JSON.stringify({ Type: "PLC", icon: "ri-tb-robot" }),
        }),
      );
    });
  }, 20000);
});
