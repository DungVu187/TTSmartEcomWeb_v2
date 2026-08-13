import { vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { toast } from "react-hot-toast";
import LogIn from "./login";

vi.mock("react-hot-toast", () => ({
  toast: {
    error: vi.fn(),
    success: vi.fn(),
  },
}));

vi.mock("../context/language.js", () => ({
  useLanguage: () => ({
    t: (key, fallback) => {
      const translations = {
        login: "Đăng nhập",
        register: "Đăng ký",
        full_name: "Họ và tên",
        phone_number: "Số điện thoại",
        processing: "Đang xử lý",
      };
      return fallback || translations[key] || key;
    },
  }),
}));

const responseOf = ({ ok = true, data = {} } = {}) => ({
  ok,
  json: vi.fn().mockResolvedValue(data),
});

const submitLogin = () => {
  fireEvent.click(screen.getAllByRole("button", { name: "Đăng nhập" })[0]);
};

const openRegistration = () => {
  fireEvent.click(screen.getAllByRole("button", { name: "Đăng ký" })[1]);
};

const fillRegistration = ({
  name = "Nguyễn Văn A",
  phone = "0912345678",
  email = "customer@example.com",
  password = "password123",
  confirmPassword = "password123",
} = {}) => {
  openRegistration();

  fireEvent.change(screen.getByPlaceholderText("Họ và tên"), { target: { value: name } });
  fireEvent.change(screen.getByPlaceholderText("Số điện thoại"), { target: { value: phone } });
  fireEvent.change(screen.getByPlaceholderText("Địa chỉ email"), { target: { value: email } });
  fireEvent.change(screen.getAllByPlaceholderText("Mật khẩu")[0], { target: { value: password } });
  fireEvent.change(screen.getByPlaceholderText("Xác nhận mật khẩu"), {
    target: { value: confirmPassword },
  });
};

describe("customer login and registration", () => {
  beforeEach(() => {
    import.meta.env.VITE_BACK_END = "http://backend.test";
    global.fetch = vi.fn();
    window.history.pushState({}, "", "/login");
    sessionStorage.clear();
    toast.error.mockClear();
    toast.success.mockClear();
  });

  test("blocks an empty login before calling the API", () => {
    render(<LogIn />);

    submitLogin();

    expect(fetch).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith("Vui lòng nhập số điện thoại hoặc email");
  });

  test("blocks an identifier that is neither a Vietnamese phone nor an email", () => {
    render(<LogIn />);

    fireEvent.change(screen.getByPlaceholderText("Số điện thoại hoặc Email"), {
      target: { value: "invalid-account" },
    });
    submitLogin();

    expect(fetch).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith(
      "Vui lòng nhập đúng số điện thoại (10 số) hoặc địa chỉ email"
    );
  });

  test("logs in by phone using cookie credentials and the active station code", async () => {
    sessionStorage.setItem("activeStationCode", "STATION-01");
    fetch.mockResolvedValueOnce(responseOf({ ok: false }));
    render(<LogIn />);

    fireEvent.change(screen.getByPlaceholderText("Số điện thoại hoặc Email"), {
      target: { value: "0912345678" },
    });
    fireEvent.change(screen.getAllByPlaceholderText("Mật khẩu")[1], {
      target: { value: "password123" },
    });
    submitLogin();

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    expect(fetch).toHaveBeenCalledWith(
      "http://backend.test/users/login",
      expect.objectContaining({
        method: "POST",
        credentials: "include",
        body: JSON.stringify({
          phone: "0912345678",
          password: "password123",
          inviteCode: "STATION-01",
        }),
      })
    );
  });

  test("logs in by email and prioritizes the station from the redirect URL", async () => {
    sessionStorage.setItem("activeStationCode", "OLD-STATION");
    window.history.pushState({}, "", "/login?redirect=/station/NEW-STATION/products");
    fetch.mockResolvedValueOnce(responseOf({ ok: false }));
    render(<LogIn />);

    fireEvent.change(screen.getByPlaceholderText("Số điện thoại hoặc Email"), {
      target: { value: "customer@example.com" },
    });
    fireEvent.change(screen.getAllByPlaceholderText("Mật khẩu")[1], {
      target: { value: "password123" },
    });
    submitLogin();

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    expect(JSON.parse(fetch.mock.calls[0][1].body)).toEqual({
      email: "customer@example.com",
      password: "password123",
      inviteCode: "NEW-STATION",
    });
  });

  test("validates registration data before calling the API", () => {
    render(<LogIn />);
    fillRegistration({ confirmPassword: "different-password" });

    fireEvent.click(screen.getAllByRole("button", { name: "Đăng ký" })[0]);

    expect(fetch).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith("Mật khẩu không khớp");
  });

  test("registers a customer with the station invite code from redirect", async () => {
    window.history.pushState({}, "", "/login?redirect=/station/HN-01");
    fetch.mockResolvedValueOnce(responseOf({
      data: { success: true },
    }));
    render(<LogIn />);
    fillRegistration();

    fireEvent.click(screen.getAllByRole("button", { name: "Đăng ký" })[0]);

    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    expect(fetch).toHaveBeenCalledWith(
      "http://backend.test/users/register",
      expect.objectContaining({
        method: "POST",
        credentials: "include",
        body: JSON.stringify({
          name: "Nguyễn Văn A",
          email: "customer@example.com",
          phone: "0912345678",
          password: "password123",
          inviteCode: "HN-01",
        }),
      })
    );
    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith("Đăng ký thành công");
    });
  });
});
