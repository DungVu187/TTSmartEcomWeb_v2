import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import toast from "react-hot-toast";
import {
  getCompanyAccounts,
  getCompanyRoles,
  revokeCompanyMembership,
  saveCompanyMembership,
} from "../api/accountApi";

const USER_TYPES = [
  { value: 1, label: "Chủ sở hữu" },
  { value: 2, label: "Quản trị viên" },
  { value: 3, label: "Thành viên" },
];

const isGuid = (value) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
const isLegacyObjectId = (value) => /^[0-9a-f]{24}$/i.test(value);

const CompanyAccessScopeDialog = ({ open, user, profile, onClose, onChanged }) => {
  const companies = useMemo(
    () => (Array.isArray(profile?.companyMemberships) ? profile.companyMemberships : []),
    [profile?.companyMemberships],
  );
  const [companyId, setCompanyId] = useState("");
  const [targetUserId, setTargetUserId] = useState("");
  const [userType, setUserType] = useState(3);
  const [roleId, setRoleId] = useState("");
  const [roles, setRoles] = useState([]);
  const [membership, setMembership] = useState(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const presetUserId = user?.userId || user?._id || "";

  useEffect(() => {
    if (!open) return;
    const preferred = companies.some((company) => company.companyId === profile?.activeCompanyId)
      ? profile.activeCompanyId
      : companies[0]?.companyId || "";
    setCompanyId(preferred);
    setTargetUserId(presetUserId);
    setMembership(null);
    setUserType(3);
    setRoleId("");
    setError("");
  }, [open, companies, profile?.activeCompanyId, presetUserId]);

  useEffect(() => {
    if (!open || !isGuid(companyId) || (!isGuid(targetUserId) && !isLegacyObjectId(targetUserId))) return;
    let active = true;
    setLoading(true);
    setError("");
    Promise.all([getCompanyRoles(companyId), getCompanyAccounts(companyId)])
      .then(([roleResult, accountResult]) => {
        if (!active) return;
        const companyRoles = (roleResult.roles || []).filter((role) => role.scopeType === 1);
        const current = (accountResult.accounts || []).find((account) => account.userId === targetUserId) || null;
        setRoles(companyRoles);
        setMembership(current);
        setUserType(current?.userType || 3);
        setRoleId(current?.roles?.[0]?.roleId || companyRoles[0]?.roleId || "");
      })
      .catch((requestError) => {
        if (active) setError(requestError.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [open, companyId, targetUserId]);

  const handleSave = async () => {
    if (!companyId || !roleId || !targetUserId) {
      setError("Vui lòng chọn Company, loại thành viên và role cấp Company.");
      return;
    }
    setSaving(true);
    setError("");
    try {
      const result = await saveCompanyMembership({ companyId, userId: targetUserId, userType, roleId });
      toast.success(result.message || "Cập nhật phạm vi truy cập thành công");
      await onChanged?.(result.account);
      onClose?.();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSaving(false);
    }
  };

  const handleRevoke = async () => {
    if (!membership || !window.confirm("Thu hồi quyền truy cập Company của tài khoản này?")) return;
    setSaving(true);
    setError("");
    try {
      const result = await revokeCompanyMembership({ companyId, userId: targetUserId });
      toast.success(result.message || "Thu hồi phạm vi truy cập thành công");
      await onChanged?.(null);
      onClose?.();
    } catch (requestError) {
      setError(requestError.message);
    } finally {
      setSaving(false);
    }
  };

  const currentCompany = companies.find((company) => company.companyId === companyId);
  const currentRoleNames = membership?.roles?.map((role) => role.name).join(", ");

  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Phạm vi truy cập</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">
            Tài khoản: <b>{user?.displayName || user?.name || targetUserId || "Chưa chọn identity"}</b>
          </Typography>
          {!presetUserId && (
            <TextField
              label="Control Plane userId"
              value={targetUserId}
              onChange={(event) => setTargetUserId(event.target.value.trim())}
              helperText="Nhập GUID của Control Plane identity; ObjectId legacy sẽ bị từ chối rõ ràng."
              size="small"
              fullWidth
              disabled={saving}
            />
          )}
          {error && <Alert severity="error">{error}</Alert>}
          {companies.length === 0 && !profile?.isPlatformSuperAdmin && (
            <Alert severity="warning">Không có Company trong companyMemberships để quản lý.</Alert>
          )}
          {companies.length > 0 ? (
            <FormControl fullWidth size="small" disabled={loading || saving}>
              <InputLabel id="company-scope-label">Company</InputLabel>
              <Select
                labelId="company-scope-label"
                label="Company"
                value={companyId}
                onChange={(event) => setCompanyId(event.target.value)}
              >
                {companies.map((company) => (
                  <MenuItem key={company.companyId} value={company.companyId}>
                    {company.name || company.companyCode}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          ) : profile?.isPlatformSuperAdmin ? (
            <TextField
              label="CompanyId"
              value={companyId}
              onChange={(event) => setCompanyId(event.target.value.trim())}
              helperText="SuperAdmin có thể quản lý mọi Company bằng CompanyId chính xác."
              size="small"
              fullWidth
              disabled={saving}
            />
          ) : null}
          <Box>
            <Typography variant="caption" color="text.secondary">Phạm vi hiện tại</Typography>
            <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
              <Chip size="small" label={currentCompany?.name || currentCompany?.companyCode || "Chưa chọn Company"} />
              <Chip
                size="small"
                color={membership ? "primary" : "default"}
                label={membership ? currentRoleNames || "Chưa có role" : "Chưa được cấp"}
              />
            </Stack>
          </Box>
          <FormControl fullWidth size="small" disabled={loading || saving}>
            <InputLabel id="company-user-type-label">Loại thành viên</InputLabel>
            <Select
              labelId="company-user-type-label"
              label="Loại thành viên"
              value={userType}
              onChange={(event) => setUserType(Number(event.target.value))}
            >
              {USER_TYPES.map((type) => (
                <MenuItem key={type.value} value={type.value}>{type.label}</MenuItem>
              ))}
            </Select>
          </FormControl>
          <FormControl fullWidth size="small" disabled={loading || saving || roles.length === 0}>
            <InputLabel id="company-role-label">Role cấp Company</InputLabel>
            <Select
              labelId="company-role-label"
              label="Role cấp Company"
              value={roleId}
              onChange={(event) => setRoleId(event.target.value)}
            >
              {roles.map((role) => (
                <MenuItem key={role.roleId} value={role.roleId}>{role.name}</MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>
      </DialogContent>
      <DialogActions>
        {membership && (
          <Button color="error" onClick={handleRevoke} disabled={saving || loading}>Thu hồi</Button>
        )}
        <Button onClick={onClose} color="inherit" disabled={saving}>Hủy</Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={saving || loading || !companyId || !targetUserId || !roleId}
        >
          {saving ? "Đang lưu..." : membership ? "Cập nhật" : "Cấp quyền"}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default CompanyAccessScopeDialog;
