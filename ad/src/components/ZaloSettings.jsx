import { useEffect, useState } from "react";
import {
  Box,
  Card,
  CardContent,
  Typography,
  TextField,
  Button,
  Grid,
  Divider,
  Chip,
  CircularProgress,
} from "@mui/material";
import toast from "react-hot-toast";
import SendIcon from "@mui/icons-material/Send";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import LinkIcon from "@mui/icons-material/Link";
import {
  getZaloAuthUrl,
  getZaloSettings,
  saveZaloSettings,
} from "../api/messagingSettingsApi";

const ZaloSettings = () => {
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [linking, setLinking] = useState(false);
  const [config, setConfig] = useState({
    appId: "",
    secretKey: "",
    oaId: "",
    recipientUserId: "",
    isLinked: false,
    expiresAt: null,
    secretKeyConfigured: false,
  });

  const authToken = sessionStorage.getItem("auth-token");

  // Lấy cấu hình Zalo hiện tại
  const fetchConfig = async () => {
    setLoading(true);
    try {
      const response = await getZaloSettings(authToken);

      if (!response.ok) {
        throw new Error("Không thể tải cấu hình Zalo");
      }

      const resData = await response.json();
      if (resData.success) {
        setConfig((prev) => ({
          ...prev,
          ...resData.data,
          secretKey: resData.data.secretKeyConfigured ? "********" : "", // che giấu secret key đã cấu hình
        }));
      }
    } catch (error) {
      console.error("Lỗi lấy cấu hình:", error);
      toast.error("Lỗi khi tải cấu hình Zalo");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchConfig();

    // Kiểm tra URL xem có param báo liên kết thành công hay không
    const queryParams = new URLSearchParams(window.location.search);
    if (queryParams.get("link") === "success") {
      toast.success("Liên kết tài khoản Zalo OA thành công!");
      // Xóa query param để tránh toast lặp lại khi reload
      const cleanUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
      window.history.replaceState({ path: cleanUrl }, "", cleanUrl);
    }
  }, []);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setConfig((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  // Lưu cấu hình
  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);

    // Chuẩn bị payload gửi lên
    const payload = {
      appId: config.appId,
      oaId: config.oaId,
      recipientUserId: config.recipientUserId,
    };

    // Chỉ gửi secretKey lên nếu admin nhập mới (không phải chuỗi mask ********)
    if (config.secretKey && config.secretKey !== "********") {
      payload.secretKey = config.secretKey;
    }

    try {
      const response = await saveZaloSettings(authToken, payload);

      if (!response.ok) {
        throw new Error("Không thể cập nhật cấu hình Zalo");
      }

      const resData = await response.json();
      if (resData.success) {
        toast.success("Đã lưu cấu hình Zalo thành công!");
        fetchConfig();
      }
    } catch (error) {
      console.error("Lỗi khi lưu cấu hình:", error);
      toast.error("Lỗi: " + error.message);
    } finally {
      setSaving(false);
    }
  };

  // Liên kết OAuth Zalo OA
  const handleLinkOAuth = async () => {
    if (!config.appId) {
      toast.error("Vui lòng nhập và Lưu cấu hình App ID trước khi thực hiện liên kết!");
      return;
    }

    setLinking(true);
    try {
      const response = await getZaloAuthUrl(authToken);

      if (!response.ok) {
        const errData = await response.json();
        throw new Error(errData.message || "Lỗi tạo link liên kết");
      }

      const resData = await response.json();
      if (resData.success && resData.authUrl) {
        toast.loading("Đang chuyển hướng sang cổng xác thực Zalo...");
        window.location.href = resData.authUrl;
      }
    } catch (error) {
      console.error("Lỗi liên kết Zalo OAuth:", error);
      toast.error(error.message);
      setLinking(false);
    }
  };

  return (
    <Box sx={{ p: 4, maxWidth: 800, mx: "auto" }}>
      <Card sx={{ boxShadow: 4, borderRadius: 3 }}>
        <CardContent sx={{ p: 4 }}>
          <Box display="flex" justifyContent="space-between" alignItems="center" mb={2}>
            <Typography variant="h5" fontWeight="bold" color="primary">
              Cấu hình Thông báo Zalo OA
            </Typography>
            {loading ? (
              <CircularProgress size={24} />
            ) : config.isLinked ? (
              <Chip
                icon={<CheckCircleIcon />}
                label="Đã liên kết"
                color="success"
                variant="filled"
                sx={{ px: 1, fontWeight: "bold" }}
              />
            ) : (
              <Chip
                icon={<ErrorIcon />}
                label="Chưa liên kết OAuth"
                color="warning"
                variant="filled"
                sx={{ px: 1, fontWeight: "bold" }}
              />
            )}
          </Box>

          <Typography variant="body2" color="textSecondary" mb={3}>
            Cấu hình ứng dụng Zalo và liên kết Zalo Official Account (OA) để tự động đẩy tin nhắn thông báo cho quản trị viên mỗi khi có khách hàng đặt hàng thành công.
          </Typography>

          <Divider sx={{ my: 3 }} />

          {loading ? (
            <Box py={5} textAlign="center">
              <CircularProgress />
              <Typography mt={2} color="textSecondary">Đang tải cấu hình...</Typography>
            </Box>
          ) : (
            <form onSubmit={handleSave}>
              <Grid container spacing={3}>
                <Grid item xs={12} md={6}>
                  <TextField
                    fullWidth
                    label="Zalo App ID"
                    name="appId"
                    value={config.appId}
                    onChange={handleChange}
                    variant="outlined"
                    placeholder="Nhập App ID từ Zalo Developers"
                    required
                  />
                </Grid>
                <Grid item xs={12} md={6}>
                  <TextField
                    fullWidth
                    label="Zalo Secret Key"
                    name="secretKey"
                    type="password"
                    value={config.secretKey}
                    onChange={handleChange}
                    variant="outlined"
                    placeholder={config.secretKeyConfigured ? "********" : "Nhập Secret Key ứng dụng"}
                    required={!config.secretKeyConfigured}
                  />
                </Grid>
                <Grid item xs={12} md={6}>
                  <TextField
                    fullWidth
                    label="Zalo OA ID"
                    name="oaId"
                    value={config.oaId}
                    onChange={handleChange}
                    variant="outlined"
                    placeholder="Nhập ID trang Zalo OA của doanh nghiệp"
                  />
                </Grid>
                <Grid item xs={12} md={6}>
                  <TextField
                    fullWidth
                    label="Zalo User ID nhận thông báo"
                    name="recipientUserId"
                    value={config.recipientUserId}
                    onChange={handleChange}
                    variant="outlined"
                    placeholder="Nhập Zalo User ID của quản trị viên"
                  />
                  <Typography variant="caption" color="textSecondary" sx={{ display: "block", mt: 0.5, px: 1 }}>
                    * Người nhận cần bấm Quan tâm OA, lấy User ID tương ứng đối với OA này.
                  </Typography>
                </Grid>

                <Grid item xs={12} sx={{ mt: 2 }}>
                  <Box display="flex" justifyContent="space-between" flexWrap="wrap" gap={2}>
                    <Button
                      type="submit"
                      variant="contained"
                      color="primary"
                      disabled={saving}
                      startIcon={saving ? <CircularProgress size={20} color="inherit" /> : <SendIcon />}
                      sx={{ px: 4, py: 1.2, borderRadius: 2, fontWeight: "bold" }}
                    >
                      {saving ? "Đang lưu..." : "Lưu cấu hình"}
                    </Button>

                    <Button
                      variant="outlined"
                      color={config.isLinked ? "success" : "primary"}
                      disabled={linking || !config.appId}
                      onClick={handleLinkOAuth}
                      startIcon={linking ? <CircularProgress size={20} color="inherit" /> : <LinkIcon />}
                      sx={{ px: 4, py: 1.2, borderRadius: 2, fontWeight: "bold" }}
                    >
                      {config.isLinked ? "Liên kết lại Zalo OA" : "Liên kết Zalo OA (OAuth)"}
                    </Button>
                  </Box>
                </Grid>
              </Grid>
            </form>
          )}

          {config.isLinked && config.expiresAt && (
            <Box mt={4} p={2} sx={{ bgcolor: "success.light", borderRadius: 2, opacity: 0.9 }}>
              <Typography variant="caption" color="success.contrastText" sx={{ display: "block", fontWeight: "bold" }}>
                ✓ Hệ thống đã được liên kết OAuth thành công với Zalo OA.
              </Typography>
              <Typography variant="caption" color="success.contrastText">
                Token hết hạn vào: {new Date(config.expiresAt).toLocaleString("vi-VN")} (Hệ thống sẽ tự động làm mới định kỳ).
              </Typography>
            </Box>
          )}
        </CardContent>
      </Card>
    </Box>
  );
};

export default ZaloSettings;
