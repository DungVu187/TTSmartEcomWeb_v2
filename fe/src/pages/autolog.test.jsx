import { vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { toast } from "react-hot-toast";
import AutoLog from "./autolog";
import { LanguageProvider } from "../context/languagecontext.jsx";

let mockCode = "secure-token";
const mockNavigate = vi.fn();

vi.mock("react-router-dom", () => ({
  useParams: () => ({ code: mockCode }),
  useNavigate: () => mockNavigate,
}), { virtual: true });

vi.mock("react-hot-toast", () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

const responseOf = ({ ok = true, data = {} } = {}) => ({
  ok,
  json: vi.fn().mockResolvedValue(data),
});

const renderAutoLog = () => render(
  <LanguageProvider>
    <AutoLog />
  </LanguageProvider>
);

describe("customer automatic login", () => {
  const originalLocation = window.location;
  let assignedHref;

  beforeAll(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      value: {
        pathname: "/secure-token",
        search: "",
        get href() {
          return assignedHref;
        },
        set href(value) {
          assignedHref = value;
        },
      },
    });
  });

  afterAll(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      value: originalLocation,
    });
  });

  beforeEach(() => {
    import.meta.env.VITE_BACK_END = "http://backend.test";
    global.fetch = vi.fn();
    mockCode = "secure-token";
    assignedHref = "";
    window.location.search = "";
    toast.error.mockClear();
    toast.success.mockClear();
    mockNavigate.mockClear();
    vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test("posts the one-time token using cookie credentials", async () => {
    fetch.mockResolvedValueOnce(responseOf());

    renderAutoLog();

    expect(screen.getByText("Đang đăng nhập tự động...")).toBeInTheDocument();
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    expect(fetch).toHaveBeenCalledWith(
      "http://backend.test/users/autologin",
      expect.objectContaining({
        method: "POST",
        credentials: "include",
        body: JSON.stringify({ token: "secure-token" }),
      })
    );
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith("Đăng nhập tự động thành công!"));
  });

  test("allows a relative station redirect after successful automatic login", async () => {
    window.location.search = "?redirect=%2Fstation%2FHN-01%2Fsensors";
    fetch.mockResolvedValueOnce(responseOf());

    renderAutoLog();

    await waitFor(() => expect(assignedHref).toBe("/station/HN-01/sensors"));
  });

  test("rejects an external redirect and falls back to the station page", async () => {
    window.location.search = "?redirect=https%3A%2F%2Fevil.example%2Fsteal";
    fetch.mockResolvedValueOnce(responseOf());

    renderAutoLog();

    await waitFor(() => expect(assignedHref).toBe("/station"));
  });

  test("shows the localized error and does not redirect when the token is rejected", async () => {
    fetch.mockResolvedValueOnce(responseOf({
      ok: false,
      data: { message: "Mã đăng nhập đã hết hạn" },
    }));

    renderAutoLog();

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith("Đăng nhập tự động thất bại.");
    });
    expect(toast.success).not.toHaveBeenCalled();
    expect(assignedHref).toBe("");
  });

  test("does not call the API when the route has no token", async () => {
    mockCode = "";

    renderAutoLog();

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Không có mã đăng nhập."));
    expect(fetch).not.toHaveBeenCalled();
    expect(assignedHref).toBe("");
  });
});
