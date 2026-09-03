import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import ApartmentOutlinedIcon from "@mui/icons-material/ApartmentOutlined";
import BusinessOutlinedIcon from "@mui/icons-material/BusinessOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import LanguageIcon from "@mui/icons-material/Language";
import SearchIcon from "@mui/icons-material/Search";
import StorefrontOutlinedIcon from "@mui/icons-material/StorefrontOutlined";
import { setAdminScope } from "../api/adminScope";

const cardSx = (selected) => ({
  p: 1.35,
  cursor: "pointer",
  border: "1.5px solid",
  borderColor: selected ? "#1976d2" : "#e1e7ef",
  borderRadius: 2,
  backgroundColor: selected ? "#f5f9ff" : "#fff",
  boxShadow: selected ? "0 0 0 1px rgba(25,118,210,.08)" : "none",
  transition: "border-color 140ms ease, background-color 140ms ease",
  "&:hover": { borderColor: "#1976d2" },
});

const StepTitle = ({ number, children }) => (
  <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1.8 }}>
    <Box sx={{ width: 28, height: 28, borderRadius: "50%", bgcolor: "#1675e0", color: "#fff", display: "grid", placeItems: "center", fontWeight: 700, fontSize: 14 }}>
      {number}
    </Box>
    <Typography fontWeight={700} fontSize={16}>{children}</Typography>
  </Stack>
);

const WorkspaceSelector = ({ profile, open, required = false, onClose }) => {
  const navigate = useNavigate();
  const companies = useMemo(
    () => (Array.isArray(profile?.companyMemberships) ? profile.companyMemberships : []),
    [profile?.companyMemberships],
  );
  const branches = useMemo(
    () => (Array.isArray(profile?.branchMemberships) ? profile.branchMemberships : []),
    [profile?.branchMemberships],
  );
  const [companyId, setCompanyId] = useState(profile?.activeCompanyId || "");
  const [branchId, setBranchId] = useState(profile?.activeBranchId || "");
  const [search, setSearch] = useState("");
  const [workspaceType, setWorkspaceType] = useState("company");

  useEffect(() => {
    if (!open) return;
    setCompanyId(profile?.activeCompanyId || "");
    setBranchId(profile?.activeBranchId || "");
    setSearch("");
    setWorkspaceType(profile?.isPlatformSuperAdmin && !profile?.activeCompanyId ? "platform" : "company");
  }, [open, profile?.activeCompanyId, profile?.activeBranchId, companies]);

  const filteredCompanies = useMemo(() => {
    const needle = search.trim().toLowerCase();
    if (!needle) return companies;
    return companies.filter((company) =>
      [company.companyCode, company.name].filter(Boolean).join(" ").toLowerCase().includes(needle),
    );
  }, [companies, search]);

  const selectedCompany = companies.find((company) => company.companyId === companyId);
  const visibleBranches = branches.filter((branch) => branch.companyId === companyId);

  const chooseCompany = (id) => {
    setWorkspaceType("company");
    setCompanyId(id);
    setBranchId("");
  };

  const confirm = () => {
    if (workspaceType === "platform") {
      setAdminScope({ companyId: "", branchId: "" });
      onClose?.();
      navigate("/system");
      return;
    }

    setAdminScope({ companyId, branchId });
    onClose?.();
    navigate("/product");
  };

  const choosePlatform = () => {
    setWorkspaceType("platform");
    setCompanyId("");
    setBranchId("");
  };

  const chooseCompanyWorkspace = () => {
    setWorkspaceType("company");
  };

  return (
    <Dialog
      open={open}
      onClose={required ? undefined : onClose}
      fullWidth
      maxWidth="lg"
      aria-labelledby="workspace-selector-title"
      PaperProps={{ sx: { borderRadius: 3, overflow: "hidden", maxWidth: 1080 } }}
    >
      <DialogTitle id="workspace-selector-title" sx={{ px: 3, pt: 2.4, pb: 1.8, fontWeight: 750, fontSize: 20 }}>
        Chuyển không gian quản lý
        <Typography component="div" variant="body2" color="text.secondary" sx={{ mt: 0.35 }}>
          Chọn công ty và chi nhánh cần truy cập
        </Typography>
      </DialogTitle>
      <DialogContent dividers sx={{ p: 0, borderColor: "#e7ebf0" }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "0.93fr 1fr 1fr" } }}>
          <Box sx={{ p: 3 }}>
            <StepTitle number="1">Không gian</StepTitle>
            {profile?.isPlatformSuperAdmin && (
              <Paper onClick={choosePlatform} sx={cardSx(workspaceType === "platform")} elevation={0}>
                <Stack direction="row" alignItems="center" spacing={1.25}>
                  <Box sx={{ p: 1, borderRadius: 1.5, bgcolor: "#e7f0ff", color: "#1675e0", display: "grid" }}><LanguageIcon /></Box>
                  <Box sx={{ flex: 1 }}>
                    <Typography fontWeight={700}>Quản trị hệ thống</Typography>
                    <Typography variant="caption" color="text.secondary">Quản lý toàn bộ nền tảng</Typography>
                  </Box>
                  {workspaceType === "platform" && <CheckCircleIcon sx={{ color: "#1675e0" }} fontSize="small" />}
                </Stack>
              </Paper>
            )}
            <Paper onClick={chooseCompanyWorkspace} sx={{ ...cardSx(workspaceType === "company"), mt: profile?.isPlatformSuperAdmin ? 1.25 : 0 }} elevation={0}>
              <Stack direction="row" alignItems="center" spacing={1.25}>
                <Box sx={{ p: 1, borderRadius: 1.5, bgcolor: "#f0f3f7", color: "#53667e", display: "grid" }}><BusinessOutlinedIcon /></Box>
                <Box sx={{ flex: 1 }}>
                  <Typography fontWeight={700}>Vận hành doanh nghiệp</Typography>
                  <Typography variant="caption" color="text.secondary">Truy cập dữ liệu công ty</Typography>
                </Box>
                {workspaceType === "company" && <CheckCircleIcon sx={{ color: "#1675e0" }} fontSize="small" />}
              </Stack>
            </Paper>
          </Box>
          <Box sx={{ p: 3, borderLeft: { md: "1px solid" }, borderColor: "#e7ebf0", opacity: workspaceType === "company" ? 1 : 0.46, pointerEvents: workspaceType === "company" ? "auto" : "none" }}>
            <StepTitle number="2">Công ty</StepTitle>
            <TextField
              size="small"
              fullWidth
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Tìm công ty..."
              sx={{ mb: 1.4, "& .MuiOutlinedInput-root": { borderRadius: 1.5 } }}
              slotProps={{ input: { startAdornment: <SearchIcon sx={{ mr: 1, color: "text.disabled", fontSize: 20 }} /> } }}
            />
            <Stack spacing={1} sx={{ maxHeight: 310, overflowY: "auto", pr: 0.5 }}>
              {filteredCompanies.map((company) => (
                <Paper key={company.companyId} onClick={() => chooseCompany(company.companyId)} sx={cardSx(company.companyId === companyId)} elevation={0}>
                  <Stack direction="row" alignItems="center" spacing={1.25}>
                    <Box sx={{ p: 0.8, borderRadius: 1.25, bgcolor: company.companyId === companyId ? "#e7f0ff" : "#f0f3f7", color: company.companyId === companyId ? "#1675e0" : "#53667e", display: "grid" }}><ApartmentOutlinedIcon fontSize="small" /></Box>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography fontWeight={600} noWrap>{company.name || company.companyCode}</Typography>
                      <Typography variant="caption" color="text.secondary">{company.companyCode}{branches.filter((branch) => branch.companyId === company.companyId).length ? ` · ${branches.filter((branch) => branch.companyId === company.companyId).length} chi nhánh` : ""}</Typography>
                    </Box>
                    {company.companyId === companyId ? <CheckCircleIcon sx={{ color: "#1675e0" }} fontSize="small" /> : <ChevronRightIcon color="action" />}
                  </Stack>
                </Paper>
              ))}
              {filteredCompanies.length === 0 && <Typography variant="body2" color="text.secondary">Không có công ty phù hợp.</Typography>}
            </Stack>
          </Box>
          <Box sx={{ p: 3, borderLeft: { md: "1px solid" }, borderColor: "#e7ebf0", opacity: workspaceType === "company" && companyId ? 1 : 0.46, pointerEvents: workspaceType === "company" && companyId ? "auto" : "none" }}>
            <StepTitle number="3">Chi nhánh</StepTitle>
            <Stack spacing={1} sx={{ maxHeight: 360, overflowY: "auto", pr: 0.5 }}>
              {visibleBranches.map((branch) => (
                <Paper key={branch.branchId} onClick={() => setBranchId(branch.branchId)} sx={cardSx(branch.branchId === branchId)} elevation={0}>
                  <Stack direction="row" alignItems="center" spacing={1.25}>
                    <Box sx={{ p: 0.8, borderRadius: 1.25, bgcolor: branch.branchId === branchId ? "#e7f0ff" : "#f0f3f7", color: branch.branchId === branchId ? "#1675e0" : "#53667e", display: "grid" }}><StorefrontOutlinedIcon fontSize="small" /></Box>
                    <Box sx={{ flex: 1, minWidth: 0 }}>
                      <Typography fontWeight={600} noWrap>{branch.name || branch.branchCode}</Typography>
                      <Typography variant="caption" color="text.secondary">{branch.branchCode} · Đang hoạt động</Typography>
                    </Box>
                    {branch.branchId === branchId && <CheckCircleIcon sx={{ color: "#1675e0" }} fontSize="small" />}
                  </Stack>
                </Paper>
              ))}
              {companyId && visibleBranches.length === 0 && <Typography variant="body2" color="text.secondary">Công ty chưa có chi nhánh được gán.</Typography>}
              {!companyId && <Typography variant="body2" color="text.secondary">Chọn một công ty để xem chi nhánh.</Typography>}
            </Stack>
            {selectedCompany && <Chip size="small" label={`Công ty: ${selectedCompany.name || selectedCompany.companyCode}`} sx={{ mt: 1.5, bgcolor: "#eef5ff", color: "#1269c8" }} />}
          </Box>
        </Box>
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 1.5, justifyContent: "space-between" }}>
        <Typography variant="body2" color="text.secondary" sx={{ display: { xs: "none", sm: "block" } }}>
          Bạn sẽ truy cập: <b>{workspaceType === "platform" ? "Quản trị hệ thống" : selectedCompany?.name || "Chưa chọn công ty"}</b>
        </Typography>
        <Stack direction="row" spacing={1}>
          {!required && <Button onClick={onClose} color="inherit">Hủy</Button>}
          <Button variant="contained" onClick={confirm} disabled={workspaceType === "company" && !companyId}>
            {workspaceType === "platform" ? "Truy cập hệ thống" : `Truy cập ${branchId ? "chi nhánh" : "công ty"}`}
          </Button>
        </Stack>
      </DialogActions>
    </Dialog>
  );
};

export default WorkspaceSelector;
