
import React, { useState, useEffect, useRef } from "react";
import {
  Typography,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Collapse,
  IconButton,
  Box,
  Stack,
  Avatar,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
} from "@mui/material";
import { ExpandMore, ExpandLess } from "@mui/icons-material";
import toast from "react-hot-toast";
import { useNavigate } from "react-router-dom";
import {
  addStationToCustomer,
  deleteCustomer,
  getCustomerUsers,
  getStationOptions,
  registerCustomer,
  replaceCustomerStations,
  rotateCustomerAutoLoginToken,
  updateCustomer,
} from "../api/stationAdministrationApi";
import { usePermissions } from "../context/permissioncontext";

const StationUser = () => {
  const { can } = usePermissions();
  const canCreate = can("customer.create");
  const canEdit = can("customer.edit");
  const canDelete = can("customer.delete");
  const canAssignStation = can("customer.assign_station");
  const [users, setUsers] = useState([]);
  const [stations, setStations] = useState([]);
  const [stationMap, setStationMap] = useState({});
  const [openRows, setOpenRows] = useState({});
  const [openDialog, setOpenDialog] = useState(false);
  const [formData, setFormData] = useState({
    name: "",
    phone: "",
    password: "",
    confirmPassword: "",
  });
  const [, setLoading] = useState(false);

  const [openStationDialog, setOpenStationDialog] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState(null);
  const [stationSearch, setStationSearch] = useState({ name: "", code: "" });
  const navigate = useNavigate();
  const [openPasswordDialog, setOpenPasswordDialog] = useState(false);
  const [encryptedString, setEncryptedString] = useState("");
  const encryptedInputRef = useRef(null);

  // Sửa thông tin khách hàng
  const [openEditDialog, setOpenEditDialog] = useState(false);
  const [editUser, setEditUser] = useState(null);
  const [editFormData, setEditFormData] = useState({ name: "", phone: "", email: "" });
  const [editLoading, setEditLoading] = useState(false);

  useEffect(() => {
    fetchUsers();
    fetchStations();
  }, []);

  const fetchUsers = async () => {
    setLoading(true);
    try {
      const data = await getCustomerUsers();
      setUsers(data);
    } catch (error) {
      console.error("Lỗi khi tải users:", error);
    } finally {
      setLoading(false);
    }
  };

  const fetchStations = async () => {
    try {
      const data = await getStationOptions();
      setStations(data);
      const map = {};
      data.forEach((s) => (map[s._id] = s));
      setStationMap(map);
    } catch (err) {
      console.error("Lỗi khi tải trạm:", err);
    }
  };

  const toggleRow = (id) => {
    setOpenRows((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const handleInputChange = (e) => {
    const field = e.target.name?.replace("register-", "");
    if (!field) return;
    setFormData((prev) => ({ ...prev, [field]: e.target.value }));
  };

  const handleRegister = async () => {
    if (formData.password !== formData.confirmPassword) {
      alert("Mật khẩu không khớp");
      return;
    }

    if (!formData.phone || !formData.password) {
      alert("Thiếu SĐT hoặc mật khẩu");
      return;
    }

    try {
      await registerCustomer({
        name: formData.name,
        phone: formData.phone,
        password: formData.password,
      });
      handleCloseDialog();
      toast.success("Thêm người dùng thành công");
      fetchUsers();
    } catch (error) {
      alert(error.message || "Đăng ký thất bại");
    }
  };

  const handleCloseDialog = () => {
    setOpenDialog(false);
    setFormData({ name: "", phone: "", password: "", confirmPassword: "" });
  };

  const handleAddStation = async (stationId) => {
    try {
      await addStationToCustomer(selectedUserId, stationId);
      toast.success("Đã thêm trạm");
      setOpenStationDialog(false);
      fetchUsers();
    } catch (error) {
      toast.error(error.message || "Lỗi khi thêm trạm");
    }
  };

  const handleRemoveStation = async (userPhone, stationIdToRemove) => {
    try {
      const user = users.find((u) => u.phone === userPhone);
      if (!user) return;

      const updatedStationList = (user.station || []).filter(
        (id) => id !== stationIdToRemove
      );

      await replaceCustomerStations(userPhone, updatedStationList);

      toast.success("Đã xoá trạm khỏi người dùng");
      fetchUsers();
    } catch (err) {
      toast.error(err.message || "Lỗi khi xóa trạm");
    }
  };

  const handleDeleteUser = async (user) => {
    if (!window.confirm(`Bạn có chắc muốn xóa người dùng ${user.name}?`))
      return;
    try {
      await deleteCustomer(user._id);
      toast.success("Đã xóa người dùng");
      fetchUsers();
    } catch (err) {
      toast.error(err.message || "Lỗi khi xóa người dùng");
    }
  };

  const filteredStations = stations.filter((station) => {
    const searchName = (stationSearch.name || "").trim().toLowerCase();
    const searchCode = (stationSearch.code || "").trim().toLowerCase();

    const nameMatch = !searchName || (station.stationName || "").toLowerCase().includes(searchName);
    const codeMatch = !searchCode || (station.stationCode || "").toLowerCase().includes(searchCode);

    return nameMatch && codeMatch;
  });

  const handleOpenPasswordDialog = (user) => {
    setEncryptedString(user.logInString || "");
    setOpenPasswordDialog(true);
  };

  const handleCopy = () => {
    try {
      const input = encryptedInputRef.current;
      if (input) {
        input.select();
        document.execCommand("copy");
        toast.success("Đã sao chép vào bộ nhớ tạm");
      } else {
        toast.error("Không tìm thấy nội dung để sao chép");
      }
    } catch {
      toast.error("Không thể sao chép!");
    }
  };

  // Mở dialog sửa thông tin
  const handleOpenEditDialog = (user) => {
    setEditUser(user);
    setEditFormData({
      name: user.name || "",
      phone: user.phone || "",
      email: user.email || "",
    });
    setOpenEditDialog(true);
  };

  // Lưu thông tin đã sửa
  const handleSaveEdit = async () => {
    if (!editUser) return;
    setEditLoading(true);
    try {
      await updateCustomer(editUser._id, {
        name: editFormData.name,
        phone: editFormData.phone,
        email: editFormData.email,
      });
      toast.success("Cập nhật thông tin thành công");
      setOpenEditDialog(false);
      fetchUsers();
    } catch (err) {
      toast.error(err.message || "Lỗi khi cập nhật");
    } finally {
      setEditLoading(false);
    }
  };

  // Reset mật khẩu về 123456
  const handleResetPassword = async () => {
    if (!editUser) return;
    if (!window.confirm(`Bạn có chắc muốn reset mật khẩu của ${editUser.name || editUser.phone} về 123456?`)) return;
    setEditLoading(true);
    try {
      await updateCustomer(editUser._id, { password: "123456" }, "Reset thất bại");
      toast.success("Đã reset mật khẩu về 123456");
      setOpenEditDialog(false);
      fetchUsers();
    } catch (err) {
      toast.error(err.message || "Lỗi khi reset mật khẩu");
    } finally {
      setEditLoading(false);
    }
  };

  // Xoay mã đăng nhập tự động
  const handleRotateToken = async () => {
    if (!editUser) return;
    if (
      !window.confirm(
        `Bạn có chắc muốn xoay mã đăng nhập tự động của ${
          editUser.name || editUser.phone
        }? Link QR/NFC cũ sẽ lập tức vô hiệu hóa.`
      )
    )
      return;
    setEditLoading(true);
    try {
      const data = await rotateCustomerAutoLoginToken(editUser._id);
      toast.success("Đã xoay mã đăng nhập tự động thành công!");
      setOpenEditDialog(false);
      setEncryptedString(data.logInString);
      setOpenPasswordDialog(true);
      fetchUsers();
    } catch (err) {
      toast.error(err.message || "Lỗi khi xoay mã");
    } finally {
      setEditLoading(false);
    }
  };

  return (
    <Box p={3} className="admin-list-page">
      <div className="sticky-header">
        <Typography variant="h4" gutterBottom>
          Quản lý người dùng và trạm
        </Typography>
        {canCreate && (
          <Button
            variant="contained"
            color="primary"
            onClick={() => setOpenDialog(true)}
          >
            Thêm người dùng mới
          </Button>
        )}
      </div>

      <TableContainer component={Paper} className="admin-list-table">
        <Table>
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>Tên</TableCell>
              <TableCell>SĐT</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Số trạm</TableCell>
              <TableCell>Tác vụ</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((user) => (
              <React.Fragment key={user._id}>
                <TableRow>
                  <TableCell>
                    <IconButton
                      size="small"
                      onClick={() => toggleRow(user._id)}
                    >
                      {openRows[user._id] ? <ExpandLess /> : <ExpandMore />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{user.name}</TableCell>
                  <TableCell>{user.phone}</TableCell>
                  <TableCell>{user.email || "-"}</TableCell>
                  <TableCell>{user.station?.length || 0}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1}>
                      {canEdit && (
                        <Button
                          variant="contained"
                          size="small"
                          color="info"
                          onClick={() => handleOpenEditDialog(user)}
                        >
                          Sửa
                        </Button>
                      )}
                      {canAssignStation && (
                        <Button
                          variant="contained"
                          size="small"
                          onClick={() => {
                            setSelectedUserId(user._id);
                            setOpenStationDialog(true);
                          }}
                        >
                          Thêm
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          variant="contained"
                          size="small"
                          color="error"
                          onClick={() => handleDeleteUser(user)}
                        >
                          Xóa
                        </Button>
                      )}
                      {canEdit && (
                        <Button
                          variant="contained"
                          size="small"
                          color="success"
                          onClick={() => handleOpenPasswordDialog(user)}
                        >
                          Xuất thông tin
                        </Button>
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
                <TableRow>
                  <TableCell colSpan={6} sx={{ p: 0 }}>
                    <Collapse
                      in={openRows[user._id]}
                      timeout="auto"
                      unmountOnExit
                    >
                      <Box sx={{ margin: 2 }}>
                        <Typography variant="subtitle1" gutterBottom>
                          Danh sách trạm
                        </Typography>
                        <Table size="small">
                          <TableHead>
                            <TableRow>
                              <TableCell>Tên trạm</TableCell>
                              <TableCell>Mã trạm</TableCell>
                              <TableCell>Số sản phẩm</TableCell>
                              <TableCell>Vị trí</TableCell>
                              <TableCell>Thao tác</TableCell>
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {(user.station || []).map((stationId) => {
                              const s = stationMap[stationId];
                              if (!s) return null;
                              return (
                                <TableRow key={stationId}>
                                  <TableCell>{s.stationName}</TableCell>
                                  <TableCell>{s.stationCode}</TableCell>
                                  <TableCell>
                                    {s.productId?.length || 0}
                                  </TableCell>
                                  <TableCell>{s.location || "-"}</TableCell>
                                  <TableCell>
                                    <Stack direction="row" spacing={1}>
                                      <Button
                                        size="small"
                                        variant="contained"
                                        color="primary"
                                        onClick={() =>
                                          navigate(`/station/${s.stationCode}`)
                                        }
                                      >
                                        Chi tiết
                                      </Button>
                                      {canAssignStation && (
                                        <Button
                                          size="small"
                                          variant="contained"
                                          color="error"
                                          onClick={() =>
                                            handleRemoveStation(
                                              user.phone,
                                              stationId
                                            )
                                          }
                                        >
                                          Xóa
                                        </Button>
                                      )}
                                    </Stack>
                                  </TableCell>
                                </TableRow>
                              );
                            })}
                          </TableBody>
                        </Table>
                      </Box>
                    </Collapse>
                  </TableCell>
                </TableRow>
              </React.Fragment>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Dialog tạo user */}
      <Dialog open={openDialog} onClose={handleCloseDialog} disableScrollLock>
        <DialogTitle>Đăng ký người dùng</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ minWidth: 300 }}>
            <TextField
              label="Tên"
              name="register-name"
              value={formData.name}
              onChange={handleInputChange}
              size="small"
            />
            <TextField
              label="SĐT"
              name="register-phone"
              value={formData.phone}
              onChange={handleInputChange}
              size="small"
            />
            <TextField
              label="Mật khẩu"
              name="register-password"
              type="password"
              value={formData.password}
              onChange={handleInputChange}
              size="small"
            />
            <TextField
              label="Xác nhận mật khẩu"
              name="register-confirmPassword"
              type="password"
              value={formData.confirmPassword}
              onChange={handleInputChange}
              size="small"
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCloseDialog}>Hủy</Button>
          <Button onClick={handleRegister} variant="contained">
            Đăng ký
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog thêm trạm */}
      <Dialog
        open={openStationDialog}
        onClose={() => setOpenStationDialog(false)}
        disableScrollLock
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Thêm trạm</DialogTitle>
        <DialogContent
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
          <TextField
            label="Tên trạm"
            value={stationSearch.name}
            onChange={(e) =>
              setStationSearch({ ...stationSearch, name: e.target.value })
            }
            size="small"
          />
          <TextField
            label="Mã trạm"
            value={stationSearch.code}
            onChange={(e) =>
              setStationSearch({ ...stationSearch, code: e.target.value })
            }
            size="small"
          />
          <List sx={{ maxHeight: 400, overflowY: "auto" }}>
            {filteredStations.map((station) => (
              <ListItem
                key={station._id}
                button
                onClick={() => handleAddStation(station._id)}
              >
                <ListItemAvatar>
                  <Avatar variant="square" />
                </ListItemAvatar>
                <ListItemText
                  primary={station.stationName}
                  secondary={`Mã: ${station.stationCode}`}
                />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenStationDialog(false)}>Đóng</Button>
        </DialogActions>
      </Dialog>

      <Dialog
        open={openPasswordDialog}
        onClose={() => setOpenPasswordDialog(false)}
        disableScrollLock
        maxWidth="sm"
        fullWidth
      >
        <DialogTitle>Mã đăng nhập tự động</DialogTitle>
        <DialogContent sx={{ minWidth: 350, display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
          <TextField
            label="Token đăng nhập"
            value={encryptedString}
            fullWidth
            size="small"
            InputProps={{ readOnly: true }}
            sx={{ mt: 1 }}
          />
          <TextField
            label="Đường dẫn tự động đăng nhập (QR/NFC Link)"
            value={encryptedString ? `${window.location.origin.replace(":5173", ":3000")}/${encryptedString}` : ""}
            inputRef={encryptedInputRef}
            fullWidth
            size="small"
            multiline
            InputProps={{ readOnly: true }}
          />
        </DialogContent>
        <DialogActions>
          <Button
            variant="outlined"
            onClick={() => {
              setOpenPasswordDialog(false);
              setEncryptedString("");
            }}
          >
            Đóng
          </Button>
          <Button variant="contained" color="primary" onClick={handleCopy}>
            Sao chép liên kết
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog sửa thông tin khách hàng */}
      <Dialog open={openEditDialog} onClose={() => setOpenEditDialog(false)} disableScrollLock>
        <DialogTitle>Sửa thông tin khách hàng</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ minWidth: 300, mt: 1 }}>
            <TextField
              label="Tên"
              value={editFormData.name}
              onChange={(e) => setEditFormData({ ...editFormData, name: e.target.value })}
              size="small"
              fullWidth
            />
            <TextField
              label="SĐT"
              value={editFormData.phone}
              onChange={(e) => setEditFormData({ ...editFormData, phone: e.target.value })}
              size="small"
              fullWidth
            />
            <TextField
              label="Email"
              value={editFormData.email}
              onChange={(e) => setEditFormData({ ...editFormData, email: e.target.value })}
              size="small"
              fullWidth
            />
            <Button
              variant="outlined"
              color="warning"
              onClick={handleResetPassword}
              disabled={editLoading}
              sx={{ textTransform: "none" }}
            >
              Reset mật khẩu về 123456
            </Button>
            <Button
              variant="outlined"
              color="secondary"
              onClick={handleRotateToken}
              disabled={editLoading}
              sx={{ textTransform: "none" }}
            >
              Xoay mã đăng nhập tự động
            </Button>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenEditDialog(false)}>Hủy</Button>
          <Button
            onClick={handleSaveEdit}
            variant="contained"
            disabled={editLoading}
          >
            {editLoading ? "Đang lưu..." : "Lưu"}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default StationUser;
