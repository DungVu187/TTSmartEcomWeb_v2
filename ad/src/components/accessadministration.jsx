import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  FormControl, FormControlLabel, InputLabel, MenuItem, Paper, Select, Stack, Tab, Tabs,
  TextField, Typography,
} from "@mui/material";
import toast from "react-hot-toast";
import { usePermissions } from "../context/permissioncontext";
import {
  getBranchRoles, getBranchUsers, getCompanyAccounts, getCompanyPermissions, getCompanyRoles, getPlatformCompanies,
  getUserBranches, lookupCompanyUser, revokeCompanyMembership, revokeUserBranch,
  saveCompanyMembership, saveCompanyRole, saveUserBranch, searchPlatformUsers, setCompanyMembershipStatus,
} from "../api/accountApi";

const USER_TYPES = [
  { value: 1, label: "Chủ sở hữu" },
  { value: 2, label: "Quản trị viên" },
  { value: 3, label: "Thành viên" },
];
const typeLabel = (value) => USER_TYPES.find((item) => item.value === value)?.label || "Thành viên";
const statusLabel = (value) => value === 1 ? "Đang hoạt động" : "Tạm khóa";

const UserRows = ({ users, onEdit, onBranches, onSuspend, onRevoke, canManageUser, branchOnly = false }) => (
  <Stack spacing={1}>
    {users.map((user) => (
      <Paper key={user.userId} variant="outlined" sx={{ p: 1.5 }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" gap={1}>
          <Box>
            <Typography fontWeight={700}>{user.displayName}</Typography>
            <Typography variant="body2" color="text.secondary">
              {[user.phone, user.email].filter(Boolean).join(" • ") || "Chưa có thông tin liên hệ"}
            </Typography>
            <Stack direction="row" spacing={0.75} mt={1} flexWrap="wrap">
              {!branchOnly && <Chip size="small" label={typeLabel(user.userType)} />}
              <Chip size="small" variant="outlined" label={user.roles?.map((role) => role.name).join(", ") || "Chưa gán vai trò"} />
              <Chip size="small" color={user.status === 1 ? "success" : "default"} label={statusLabel(user.status)} />
            </Stack>
          </Box>
          <Stack direction="row" spacing={1} alignItems="center">
            {onEdit && (!canManageUser || canManageUser(user)) && <Button size="small" onClick={() => onEdit(user)}>Gán vai trò</Button>}
            {onBranches && (!canManageUser || canManageUser(user)) && <Button size="small" onClick={() => onBranches(user)}>Gán chi nhánh</Button>}
            {onSuspend && (!canManageUser || canManageUser(user)) && <Button size="small" color="warning" onClick={() => onSuspend(user)}>{user.status === 1 ? "Tạm khóa" : "Mở lại"}</Button>}
            {onRevoke && (!canManageUser || canManageUser(user)) && <Button size="small" color="error" onClick={() => onRevoke(user)}>Ngừng quyền truy cập</Button>}
          </Stack>
        </Stack>
      </Paper>
    ))}
    {users.length === 0 && <Alert severity="info">Chưa có người dùng trong phạm vi này.</Alert>}
  </Stack>
);

const MembershipDialog = ({ open, companyId, user, roles, allowedUserTypes, onClose, onSaved }) => {
  const [userType, setUserType] = useState(3);
  const [roleId, setRoleId] = useState("");
  const [saving, setSaving] = useState(false);
  useEffect(() => {
    if (!open) return;
    setUserType(user?.userType || 3);
    setRoleId(user?.roles?.[0]?.roleId || roles.find((role) => role.scopeType === 1)?.roleId || "");
  }, [open, user, roles]);
  const selectedRole = roles.find((role) => role.roleId === roleId);
  const save = async () => {
    setSaving(true);
    try {
      const result = await saveCompanyMembership({ companyId, userId: user.userId, userType, roleId });
      toast.success(result.message || "Cập nhật quyền truy cập công ty thành công");
      onSaved();
    } catch (error) { toast.error(error.message); } finally { setSaving(false); }
  };
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Cấp quyền truy cập công ty</DialogTitle>
      <DialogContent><Stack spacing={2} pt={1}>
        <TextField label="Người dùng" value={user?.displayName || ""} disabled />
        <TextField label="Công ty" value={user?.companyName || "Công ty đã chọn"} disabled />
        <FormControl><InputLabel>Loại tài khoản</InputLabel><Select label="Loại tài khoản" value={userType} onChange={(e) => setUserType(Number(e.target.value))}>
          {USER_TYPES.filter((item) => allowedUserTypes.includes(item.value)).map((item) => <MenuItem key={item.value} value={item.value}>{item.label}</MenuItem>)}
        </Select></FormControl>
        <FormControl><InputLabel>Vai trò</InputLabel><Select label="Vai trò" value={roleId} onChange={(e) => setRoleId(e.target.value)}>
          {roles.filter((role) => role.scopeType === 1).map((role) => <MenuItem key={role.roleId} value={role.roleId}>{role.name}</MenuItem>)}
        </Select></FormControl>
        <Box><Typography variant="caption" color="text.secondary">Quyền được cấp</Typography>
          <Typography variant="body2">{selectedRole?.permissionLabels?.join(", ") || "Vai trò chưa có quyền đang hiệu lực"}</Typography></Box>
      </Stack></DialogContent>
      <DialogActions><Button onClick={onClose}>Hủy</Button><Button variant="contained" disabled={!roleId || saving} onClick={save}>Xác nhận</Button></DialogActions>
    </Dialog>
  );
};

const BranchDialog = ({ open, companyId, user, branchRoles, onClose, onSaved }) => {
  const [branches, setBranches] = useState([]);
  const [branchId, setBranchId] = useState("");
  const [roleId, setRoleId] = useState("");
  useEffect(() => {
    if (!open || !user) return;
    getUserBranches({ companyId, userId: user.userId }).then((result) => {
      setBranches(result.branches || []);
      setBranchId(result.branches?.[0]?.branchId || "");
    }).catch((error) => toast.error(error.message));
    setRoleId(branchRoles[0]?.roleId || "");
  }, [open, companyId, user, branchRoles]);
  const selected = branches.find((branch) => branch.branchId === branchId);
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>Gán chi nhánh</DialogTitle>
    <DialogContent><Stack spacing={2} pt={1}>
      <TextField label="Người dùng" value={user?.displayName || ""} disabled />
      <FormControl><InputLabel>Chi nhánh</InputLabel><Select label="Chi nhánh" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
        {branches.map((branch) => <MenuItem key={branch.branchId} value={branch.branchId}>{branch.name} ({branch.branchCode})</MenuItem>)}
      </Select></FormControl>
      <FormControl><InputLabel>Vai trò tại chi nhánh</InputLabel><Select label="Vai trò tại chi nhánh" value={roleId} onChange={(e) => setRoleId(e.target.value)}>
        {branchRoles.map((role) => <MenuItem key={role.roleId} value={role.roleId}>{role.name}</MenuItem>)}
      </Select></FormControl>
      {selected?.isAssigned && <Alert severity="info">Người dùng đang có quyền tại chi nhánh này.</Alert>}
    </Stack></DialogContent><DialogActions>
      {selected?.isAssigned && <Button color="error" onClick={async () => { await revokeUserBranch({ companyId, userId: user.userId, branchId }); onSaved(); }}>Ngừng quyền truy cập</Button>}
      <Button onClick={onClose}>Hủy</Button><Button variant="contained" disabled={!branchId || !roleId} onClick={async () => {
        try { await saveUserBranch({ companyId, userId: user.userId, branchId, roleId }); toast.success("Đã cập nhật chi nhánh"); onSaved(); }
        catch (error) { toast.error(error.message); }
      }}>Lưu</Button></DialogActions></Dialog>;
};

const RolePanel = ({ companyId, roles, permissions, canManage, onChanged }) => {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [scopeType, setScopeType] = useState(1);
  const [selected, setSelected] = useState([]);
  const [editingRoleId, setEditingRoleId] = useState("");
  const grouped = useMemo(() => permissions.reduce((result, item) => {
    const key = item.featureName || item.moduleCode;
    result[key] = [...(result[key] || []), item];
    return result;
  }, {}), [permissions]);
  return <Stack spacing={2}>
    {canManage && <Paper variant="outlined" sx={{ p: 2 }}><Stack spacing={1.5}>
      <Typography fontWeight={700}>{editingRoleId ? "Chỉnh sửa vai trò nội bộ" : "Tạo vai trò nội bộ"}</Typography>
      <TextField label="Tên vai trò" value={name} onChange={(e) => setName(e.target.value)} />
      <TextField label="Mô tả" value={description} onChange={(e) => setDescription(e.target.value)} multiline minRows={2} />
      <FormControl><InputLabel>Phạm vi</InputLabel><Select label="Phạm vi" value={scopeType} onChange={(e) => setScopeType(Number(e.target.value))}>
        <MenuItem value={1}>Công ty</MenuItem><MenuItem value={2}>Chi nhánh</MenuItem>
      </Select></FormControl>
      {Object.entries(grouped).map(([feature, items]) => <Box key={feature}><Typography fontWeight={650}>{feature}</Typography>
        {items.map((permission) => <FormControlLabel key={permission.permissionId} control={<Checkbox checked={selected.includes(permission.permissionId)} onChange={() => setSelected((old) => old.includes(permission.permissionId) ? old.filter((id) => id !== permission.permissionId) : [...old, permission.permissionId])} />} label={permission.name} />)}
      </Box>)}
      {permissions.length === 0 && <Alert severity="warning">Công ty chưa được bật chức năng nào có quyền để tạo vai trò.</Alert>}
      <Button variant="contained" disabled={!name.trim() || selected.length === 0} onClick={async () => {
        try { await saveCompanyRole({ companyId, roleId: editingRoleId || undefined, name, description, scopeType, permissionIds: selected }); toast.success(editingRoleId ? "Cập nhật vai trò thành công" : "Tạo vai trò thành công"); setName(""); setDescription(""); setSelected([]); setEditingRoleId(""); onChanged(); }
        catch (error) { toast.error(error.message); }
      }}>{editingRoleId ? "Lưu thay đổi" : "Tạo vai trò"}</Button>
    </Stack></Paper>}
    {roles.map((role) => <Paper key={role.roleId} variant="outlined" sx={{ p: 1.5 }}><Typography fontWeight={700}>{role.name}</Typography>
      <Typography variant="body2" color="text.secondary">{role.scopeType === 1 ? "Phạm vi công ty" : "Phạm vi chi nhánh"} • {role.isSystemTemplate ? "Vai trò mẫu (chỉ đọc)" : "Vai trò nội bộ"}</Typography>
      <Typography variant="body2" mt={0.5}>{role.permissionLabels?.join(", ") || "Chưa có quyền đang hiệu lực"}</Typography>
      {canManage && <Button size="small" sx={{ mt: 1 }} onClick={() => {
        setEditingRoleId(role.isSystemTemplate ? "" : role.roleId);
        setName(role.isSystemTemplate ? `Bản sao ${role.name}` : role.name);
        setDescription(role.description || "");
        setScopeType(role.scopeType);
        setSelected(permissions.filter((permission) => role.permissions?.includes(permission.permissionCode)).map((permission) => permission.permissionId));
      }}>{role.isSystemTemplate ? "Sao chép vai trò mẫu" : "Chỉnh sửa"}</Button>}
    </Paper>)}
  </Stack>;
};

const AccessAdministration = ({ platform = false }) => {
  const { profile, scope } = usePermissions();
  const isPlatform = platform || (profile?.isPlatformSuperAdmin && !scope.companyId);
  const companyId = scope.companyId || profile?.activeCompanyId || "";
  const branchId = scope.branchId || profile?.activeBranchId || "";
  const membership = profile?.companyMemberships?.find((item) => item.companyId === companyId);
  const isOwner = membership?.userType === 1;
  const isCompanyAdmin = membership?.userType === 2;
  const [companies, setCompanies] = useState([]);
  const [selectedCompany, setSelectedCompany] = useState(companyId);
  const [users, setUsers] = useState([]);
  const [roles, setRoles] = useState([]);
  const [permissions, setPermissions] = useState([]);
  const [tab, setTab] = useState(0);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState([]);
  const [editing, setEditing] = useState(null);
  const [branchUser, setBranchUser] = useState(null);
  const activeCompany = isPlatform ? selectedCompany : companyId;

  const load = async () => {
    if (!activeCompany) return;
    try {
      const [accounts, roleResult, permissionResult] = await Promise.all([
        getCompanyAccounts(activeCompany), getCompanyRoles(activeCompany), getCompanyPermissions(activeCompany),
      ]);
      const labelMap = new Map((permissionResult.permissions || []).map((item) => [item.permissionCode, item.name]));
      const decorate = (role) => ({ ...role, permissionLabels: (role.permissions || []).map((code) => labelMap.get(code)).filter(Boolean) });
      setUsers(accounts.accounts || []);
      setRoles((roleResult.roles || []).map(decorate));
      setPermissions(permissionResult.permissions || []);
    } catch (error) { toast.error(error.message); }
  };
  useEffect(() => { if (isPlatform) getPlatformCompanies().then((r) => { setCompanies(r.companies || []); setSelectedCompany((old) => old || r.companies?.[0]?.companyId || ""); }).catch((e) => toast.error(e.message)); }, [isPlatform]);
  useEffect(() => { if (activeCompany && !branchId) load(); }, [activeCompany, branchId]);
  useEffect(() => {
    if (!branchId || !companyId) return;
    // Branch endpoint intentionally returns only users of the active branch.
    getBranchUsers({ companyId, branchId })
      .then((result) => setUsers(result.users || [])).catch((error) => toast.error(error.message));
    getBranchRoles({ companyId, branchId }).then((r) => setRoles(r.roles || [])).catch((error) => toast.error(error.message));
  }, [branchId, companyId]);

  const search = async () => {
    try {
      const result = isPlatform ? await searchPlatformUsers(query) : await lookupCompanyUser(query);
      setResults(result.users || []);
    } catch (error) { toast.error(error.message); }
  };
  const startGrant = (user) => setEditing({ ...user, companyName: companies.find((c) => c.companyId === activeCompany)?.name });
  const revoke = async (user) => {
    if (!window.confirm(`Ngừng quyền truy cập công ty của ${user.displayName}?`)) return;
    try { await revokeCompanyMembership({ companyId: activeCompany, userId: user.userId }); toast.success("Đã ngừng quyền truy cập"); load(); }
    catch (error) { toast.error(error.message); }
  };

  if (branchId) return <Box sx={{ p: 2 }}><Typography variant="h4" mb={0.5}>Người dùng chi nhánh</Typography>
    <Typography color="text.secondary" mb={2}>Chỉ hiển thị người dùng và vai trò tại chi nhánh đang chọn.</Typography>
    <UserRows users={users} branchOnly onEdit={(user) => setBranchUser(user)} onRevoke={async (user) => {
      await revokeUserBranch({ companyId, userId: user.userId, branchId }); setUsers((old) => old.filter((item) => item.userId !== user.userId));
    }} />
    <BranchDialog open={Boolean(branchUser)} companyId={companyId} user={branchUser} branchRoles={roles} onClose={() => setBranchUser(null)} onSaved={() => { setBranchUser(null); window.location.reload(); }} />
  </Box>;

  return <Box sx={{ p: 2, maxWidth: 1200, mx: "auto" }}>
    <Typography variant="h4">Người dùng & Vai trò</Typography>
    {isPlatform && <Stack direction="row" spacing={1} my={1}><Chip label="Vai trò: Quản trị nền tảng" /><Chip label="Phạm vi: Toàn hệ thống" /><Chip color="primary" label="Quyền: Toàn quyền" /></Stack>}
    {isPlatform && <FormControl fullWidth sx={{ mt: 2 }}><InputLabel>Công ty</InputLabel><Select label="Công ty" value={selectedCompany} onChange={(e) => setSelectedCompany(e.target.value)}>
      {companies.map((company) => <MenuItem key={company.companyId} value={company.companyId}>{company.name} ({company.companyCode})</MenuItem>)}
    </Select></FormControl>}
    {!isPlatform && <Tabs value={tab} onChange={(_, value) => setTab(value)} sx={{ my: 2 }}><Tab label="Người dùng" /><Tab label="Vai trò" /></Tabs>}
    {(isPlatform || tab === 0) && <Stack spacing={2} mt={2}>
      <Paper variant="outlined" sx={{ p: 2 }}><Typography fontWeight={700} mb={1}>{isPlatform ? "Cấp quyền truy cập công ty" : "Thêm nhân viên"}</Typography>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={1}><TextField fullWidth label={isPlatform ? "Tìm theo tên, số điện thoại hoặc email" : "Nhập chính xác số điện thoại hoặc email"} value={query} onChange={(e) => setQuery(e.target.value)} /><Button variant="contained" onClick={search}>Tìm người dùng</Button></Stack>
        <Stack mt={1}>{results.map((user) => <Button key={user.userId} sx={{ justifyContent: "flex-start" }} onClick={() => startGrant(user)}>{user.displayName} — {user.phone || user.email}</Button>)}</Stack>
      </Paper>
      <UserRows users={users} onEdit={startGrant} onBranches={!isPlatform ? setBranchUser : undefined}
        onSuspend={async (user) => { try { await setCompanyMembershipStatus({ companyId: activeCompany, userId: user.userId, isActive: user.status !== 1 }); await load(); } catch (error) { toast.error(error.message); } }}
        onRevoke={revoke}
        canManageUser={isPlatform ? undefined : (user) => isOwner ? user.userType !== 1 : isCompanyAdmin && user.userType === 3} />
    </Stack>}
    {!isPlatform && tab === 1 && <RolePanel companyId={companyId} roles={roles} permissions={permissions} canManage={isOwner} onChanged={load} />}
    <MembershipDialog open={Boolean(editing)} companyId={activeCompany} user={editing} roles={roles}
      allowedUserTypes={isPlatform ? [1, 2, 3] : isOwner ? [2, 3] : [3]}
      onClose={() => setEditing(null)} onSaved={() => { setEditing(null); setResults([]); load(); }} />
    <BranchDialog open={Boolean(branchUser)} companyId={activeCompany} user={branchUser} branchRoles={roles.filter((role) => role.scopeType === 2)} onClose={() => setBranchUser(null)} onSaved={() => { setBranchUser(null); load(); }} />
  </Box>;
};

export default AccessAdministration;
