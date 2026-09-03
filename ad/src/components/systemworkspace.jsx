import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Avatar,
  Box,
  Chip,
  InputAdornment,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import AppsOutlinedIcon from "@mui/icons-material/AppsOutlined";
import AssessmentOutlinedIcon from "@mui/icons-material/AssessmentOutlined";
import BusinessOutlinedIcon from "@mui/icons-material/BusinessOutlined";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import DashboardOutlinedIcon from "@mui/icons-material/DashboardOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import HealthAndSafetyOutlinedIcon from "@mui/icons-material/HealthAndSafetyOutlined";
import ManageAccountsOutlinedIcon from "@mui/icons-material/ManageAccountsOutlined";
import PolicyOutlinedIcon from "@mui/icons-material/PolicyOutlined";
import ReceiptLongOutlinedIcon from "@mui/icons-material/ReceiptLongOutlined";
import SearchIcon from "@mui/icons-material/Search";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import ShieldOutlinedIcon from "@mui/icons-material/ShieldOutlined";
import { clearAdminScope } from "../api/adminScope";
import { usePermissions } from "../context/permissioncontext";

export const systemModules = [
  { key: "overview", title: "Tổng quan hệ thống", description: "Theo dõi trạng thái chung của nền tảng", path: "/system", icon: DashboardOutlinedIcon, color: "#1473e6", surface: "#eaf3ff" },
  { key: "organizations", title: "Công ty & Chi nhánh", description: "Quản lý phạm vi công ty và chi nhánh", path: "/system/organizations", icon: BusinessOutlinedIcon, color: "#0f9f6e", surface: "#e9f8f2" },
  { key: "users", title: "Người dùng & Vai trò", description: "Quản lý tài khoản và vai trò nền tảng", path: "/system/users", icon: ManageAccountsOutlinedIcon, color: "#7c3aed", surface: "#f1eafe" },
  { key: "permissions", title: "Nhóm quyền", description: "Tổ chức quyền truy cập theo nhóm", path: "/system/permissions", icon: PolicyOutlinedIcon, color: "#db6b18", surface: "#fff2e8" },
  { key: "applications", title: "Ứng dụng & Dịch vụ", description: "Danh mục ứng dụng và gói dịch vụ", path: "/system/applications", icon: AppsOutlinedIcon, color: "#2563eb", surface: "#eaf0ff" },
  { key: "approvals", title: "Yêu cầu phê duyệt", description: "Theo dõi các yêu cầu cần xử lý", path: "/system/approvals", icon: FactCheckOutlinedIcon, color: "#d97706", surface: "#fff5db" },
  { key: "logs", title: "Nhật ký hệ thống", description: "Tra cứu hoạt động quản trị nền tảng", path: "/system/logs", icon: ReceiptLongOutlinedIcon, color: "#475569", surface: "#eef2f6" },
  { key: "health", title: "Giám sát & Sức khỏe", description: "Theo dõi dịch vụ và database", path: "/system/health", icon: HealthAndSafetyOutlinedIcon, color: "#dc2626", surface: "#ffeded" },
  { key: "settings", title: "Cấu hình hệ thống", description: "Thiết lập các chính sách nền tảng", path: "/system/settings", icon: SettingsOutlinedIcon, color: "#334155", surface: "#edf1f5" },
  { key: "reports", title: "Báo cáo", description: "Không gian báo cáo cấp hệ thống", path: "/system/reports", icon: AssessmentOutlinedIcon, color: "#0891b2", surface: "#e8f8fb" },
];

const sectionDescriptions = Object.fromEntries(systemModules.map((item) => [item.key, item]));

const metricCards = [
  { label: "Công ty đang quản lý", icon: BusinessOutlinedIcon, color: "#1473e6", surface: "#eaf3ff" },
  { label: "Chi nhánh vận hành", icon: DashboardOutlinedIcon, color: "#7c3aed", surface: "#f1eafe" },
  { label: "Người dùng nội bộ", icon: GroupsOutlinedIcon, color: "#0f9f6e", surface: "#e9f8f2" },
  { label: "Yêu cầu cần xử lý", icon: FactCheckOutlinedIcon, color: "#db6b18", surface: "#fff2e8" },
];

const SystemWorkspace = ({ section = "overview" }) => {
  const navigate = useNavigate();
  const { profile, scope } = usePermissions();
  const [search, setSearch] = useState("");
  const current = sectionDescriptions[section] || sectionDescriptions.overview;

  useEffect(() => {
    if (scope.companyId || scope.branchId) clearAdminScope();
  }, [scope.companyId, scope.branchId]);

  const visibleModules = useMemo(() => {
    const needle = search.trim().toLocaleLowerCase("vi");
    if (!needle) return systemModules;
    return systemModules.filter((item) => `${item.title} ${item.description}`.toLocaleLowerCase("vi").includes(needle));
  }, [search]);

  return (
    <Box sx={{ maxWidth: 1540, mx: "auto", pb: 4 }} data-testid="system-workspace">
      <Stack direction={{ xs: "column", lg: "row" }} justifyContent="space-between" alignItems={{ xs: "stretch", lg: "center" }} spacing={2} sx={{ mb: 2.25 }}>
        <Box>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 0.55 }}>
            <Chip label="Không gian nền tảng" size="small" sx={{ bgcolor: "#eaf3ff", color: "#0e63c7" }} />
            <Typography variant="caption" color="text.secondary">Dành cho SuperAdmin</Typography>
          </Stack>
          <Typography variant="h4" sx={{ fontSize: { xs: 24, md: 28 }, fontWeight: 750, color: "#172b4d" }}>
            {current.title}
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 0.55, fontSize: 14 }}>
            {current.description}. Các chức năng nghiệp vụ sẽ được bổ sung ở giai đoạn tiếp theo.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1.25} alignItems="center">
          <TextField
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Tìm trong quản trị hệ thống..."
            sx={{ width: { xs: "100%", sm: 330 } }}
            slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }}
          />
          <Avatar sx={{ width: 40, height: 40, bgcolor: "#183b56", fontSize: 14, fontWeight: 700 }}>
            {(profile?.name || "SA").split(" ").map((part) => part[0]).slice(-2).join("").toUpperCase()}
          </Avatar>
        </Stack>
      </Stack>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", xl: "repeat(4, 1fr)" }, gap: 1.5, mb: 2 }}>
        {metricCards.map(({ label, icon: Icon, color, surface }) => (
          <Paper key={label} sx={{ p: 2, border: "1px solid #e5eaf0", borderRadius: 2.25, boxShadow: "0 2px 10px rgba(16,42,67,.045)" }}>
            <Stack direction="row" alignItems="center" spacing={1.5}>
              <Box sx={{ width: 44, height: 44, borderRadius: 2, display: "grid", placeItems: "center", bgcolor: surface, color }}><Icon /></Box>
              <Box>
                <Typography color="text.secondary" sx={{ fontSize: 13 }}>{label}</Typography>
                <Typography sx={{ fontSize: 23, fontWeight: 750, lineHeight: 1.2 }}>—</Typography>
              </Box>
            </Stack>
            <Typography variant="caption" sx={{ color: "#8795a8", display: "block", mt: 1.35 }}>Chưa kết nối dữ liệu thống kê</Typography>
          </Paper>
        ))}
      </Box>

      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", xl: "minmax(0, 1fr) 330px" }, gap: 2 }}>
        <Paper sx={{ border: "1px solid #e5eaf0", borderRadius: 2.25, overflow: "hidden" }}>
          <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ xs: "flex-start", sm: "center" }} spacing={1} sx={{ px: 2.25, py: 1.8, borderBottom: "1px solid #e9edf2" }}>
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 16 }}>Danh mục quản trị</Typography>
              <Typography variant="body2" color="text.secondary">Khung điều hướng cơ bản của workspace SuperAdmin</Typography>
            </Box>
            <Chip label={`${visibleModules.length} phân hệ`} variant="outlined" size="small" />
          </Stack>
          <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "repeat(2, 1fr)" } }}>
            {visibleModules.map(({ key, title, description, path, icon: Icon, color, surface }) => {
              const active = key === section;
              return (
                <Box
                  key={key}
                  component="button"
                  type="button"
                  onClick={() => navigate(path)}
                  sx={{
                    appearance: "none", border: 0, borderBottom: "1px solid #edf0f4", borderRight: { md: "1px solid #edf0f4" }, bgcolor: active ? "#f7faff" : "#fff",
                    p: 2, textAlign: "left", cursor: "pointer", display: "flex", alignItems: "center", gap: 1.5, color: "inherit",
                    "&:hover": { bgcolor: "#f7faff" }, "&:focus-visible": { outline: "3px solid rgba(20,115,230,.18)", outlineOffset: -3 },
                  }}
                >
                  <Box sx={{ width: 42, height: 42, borderRadius: 1.75, display: "grid", placeItems: "center", bgcolor: surface, color, flexShrink: 0 }}><Icon fontSize="small" /></Box>
                  <Box sx={{ flex: 1, minWidth: 0 }}>
                    <Typography sx={{ fontWeight: 680, fontSize: 14.5 }}>{title}</Typography>
                    <Typography variant="body2" color="text.secondary" noWrap>{description}</Typography>
                  </Box>
                  <ChevronRightIcon sx={{ color: active ? "#1473e6" : "#a2adba" }} fontSize="small" />
                </Box>
              );
            })}
            {visibleModules.length === 0 && <Typography color="text.secondary" sx={{ p: 3 }}>Không tìm thấy phân hệ phù hợp.</Typography>}
          </Box>
        </Paper>

        <Stack spacing={2}>
          <Paper sx={{ p: 2.25, border: "1px solid #e5eaf0", borderRadius: 2.25 }}>
            <Stack direction="row" spacing={1.25} alignItems="center" sx={{ mb: 1.5 }}>
              <Box sx={{ width: 38, height: 38, borderRadius: 1.75, display: "grid", placeItems: "center", bgcolor: "#e9f8f2", color: "#0f9f6e" }}><ShieldOutlinedIcon fontSize="small" /></Box>
              <Box>
                <Typography sx={{ fontWeight: 700 }}>Trạng thái workspace</Typography>
                <Typography variant="caption" color="text.secondary">Khung giao diện đã sẵn sàng</Typography>
              </Box>
            </Stack>
            <Stack spacing={1.2}>
              {["Phạm vi Platform SuperAdmin", "Điều hướng hệ thống riêng", "Không thay đổi workspace vận hành"].map((text) => (
                <Stack key={text} direction="row" spacing={1} alignItems="center">
                  <Box sx={{ width: 7, height: 7, borderRadius: "50%", bgcolor: "#20a66a" }} />
                  <Typography variant="body2">{text}</Typography>
                </Stack>
              ))}
            </Stack>
          </Paper>
          <Paper sx={{ p: 2.25, border: "1px solid #e5eaf0", borderRadius: 2.25, bgcolor: "#fbfcfe" }}>
            <Typography sx={{ fontWeight: 700, mb: 0.75 }}>Giai đoạn hiện tại</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ lineHeight: 1.65 }}>
              Đây là workspace giao diện cơ bản. Menu, số liệu và thao tác quản trị chưa kết nối API để tránh tạo dữ liệu hoặc hành vi giả.
            </Typography>
          </Paper>
        </Stack>
      </Box>
    </Box>
  );
};

export default SystemWorkspace;
