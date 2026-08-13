import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import SendIcon from "@mui/icons-material/Send";
import toast from "react-hot-toast";
import {
  createTelegramRecipient,
  deleteTelegramRecipient,
  getTelegramSettings,
  sendTelegramTestMessage,
  updateTelegramRecipient,
  updateTelegramSettings,
} from "../api/messagingSettingsApi";

const emptyRecipient = {
  label: "",
  chatId: "",
  type: "personal",
  enabled: true,
  notifyTypes: ["new_order"],
};

const TelegramSettings = () => {
  const [config, setConfig] = useState({ enabled: false, recipients: [], botConfigured: false });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingRecipient, setEditingRecipient] = useState(null);
  const [recipientForm, setRecipientForm] = useState(emptyRecipient);

  const runTelegramRequest = async (responsePromise) => {
    const response = await responsePromise;
    const data = await response.json();
    if (!response.ok) throw new Error(data.message || "Không thể thực hiện yêu cầu");
    return data;
  };

  const fetchSettings = async () => {
    setLoading(true);
    try {
      const data = await runTelegramRequest(getTelegramSettings());
      setConfig(data.data);
    } catch (error) {
      console.error("Lỗi tải cấu hình Telegram:", error);
      toast.error(error.message || "Không thể tải cấu hình Telegram");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSettings();
  }, []);

  const updateEnabled = async (enabled) => {
    setSaving(true);
    try {
      await runTelegramRequest(updateTelegramSettings(enabled));
      setConfig((current) => ({ ...current, enabled }));
      toast.success(enabled ? "Đã bật thông báo Telegram" : "Đã tắt thông báo Telegram");
    } catch (error) {
      console.error("Lỗi cập nhật Telegram:", error);
      toast.error(error.message || "Không thể cập nhật cấu hình Telegram");
    } finally {
      setSaving(false);
    }
  };

  const openCreateDialog = () => {
    setEditingRecipient(null);
    setRecipientForm(emptyRecipient);
    setDialogOpen(true);
  };

  const openEditDialog = (recipient) => {
    setEditingRecipient(recipient);
    setRecipientForm({
      label: recipient.label || "",
      chatId: recipient.chatId || "",
      type: recipient.type || "personal",
      enabled: recipient.enabled !== false,
      notifyTypes: recipient.notifyTypes || ["new_order"],
    });
    setDialogOpen(true);
  };

  const saveRecipient = async () => {
    if (!recipientForm.chatId.trim()) {
      toast.error("Vui lòng nhập Chat ID");
      return;
    }

    setSaving(true);
    try {
      await runTelegramRequest(
        editingRecipient
          ? updateTelegramRecipient(editingRecipient._id, recipientForm)
          : createTelegramRecipient(recipientForm),
      );
      setDialogOpen(false);
      await fetchSettings();
      toast.success(editingRecipient ? "Đã cập nhật người nhận" : "Đã thêm người nhận");
    } catch (error) {
      console.error("Lỗi lưu người nhận Telegram:", error);
      toast.error(error.message || "Không thể lưu người nhận Telegram");
    } finally {
      setSaving(false);
    }
  };

  const deleteRecipient = async (recipient) => {
    if (!window.confirm(`Xóa ${recipient.label || recipient.chatId}?`)) return;

    try {
      await runTelegramRequest(deleteTelegramRecipient(recipient._id));
      await fetchSettings();
      toast.success("Đã xóa người nhận");
    } catch (error) {
      console.error("Lỗi xóa người nhận Telegram:", error);
      toast.error(error.message || "Không thể xóa người nhận Telegram");
    }
  };

  const sendTest = async (recipient) => {
    try {
      const data = await runTelegramRequest(
        sendTelegramTestMessage(recipient.chatId),
      );
      if (data.sent > 0) {
        toast.success("Đã gửi tin nhắn thử");
      } else {
        toast.error("Telegram không gửi được tin thử");
      }
    } catch (error) {
      console.error("Lỗi gửi thử Telegram:", error);
      toast.error(error.message || "Không thể gửi tin nhắn thử");
    }
  };

  const updateRecipientForm = (field, value) => {
    setRecipientForm((current) => ({ ...current, [field]: value }));
  };

  return (
    <Box sx={{ maxWidth: 1150, mx: "auto", py: 2 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 2, mb: 3, flexWrap: "wrap" }}>
        <Box>
          <Typography variant="h5" fontWeight={700}>Cấu hình Telegram</Typography>
          <Typography color="text.secondary">Thông báo đơn hàng mới đến người nhận hoặc nhóm đã cấu hình</Typography>
        </Box>
        <FormControlLabel
          control={<Switch checked={config.enabled} onChange={(event) => updateEnabled(event.target.checked)} disabled={loading || saving || !config.botConfigured} />}
          label={config.enabled ? "Đang bật" : "Đang tắt"}
        />
      </Box>

      {!config.botConfigured && (
        <Alert severity="warning" sx={{ mb: 2 }}>Chưa cấu hình TELEGRAM_BOT_TOKEN trên máy chủ</Alert>
      )}

      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Typography variant="subtitle1" fontWeight={600} gutterBottom>Thiết lập Chat ID</Typography>
        <Typography variant="body2" color="text.secondary">
          Với cá nhân, nhắn <b>/start</b> cho bot rồi mở getUpdates để lấy Chat ID. Với nhóm, thêm bot vào nhóm; Chat ID của nhóm thường là số âm.
        </Typography>
      </Paper>

      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5, gap: 2, flexWrap: "wrap" }}>
        <Typography variant="h6">Người và nhóm nhận</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Thêm người/nhóm nhận</Button>
      </Box>

      {loading ? (
        <Box sx={{ py: 6, textAlign: "center" }}><CircularProgress /></Box>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nhãn</TableCell>
                <TableCell>Chat ID</TableCell>
                <TableCell>Loại</TableCell>
                <TableCell align="center">Bật</TableCell>
                <TableCell>Loại thông báo</TableCell>
                <TableCell align="right">Thao tác</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {config.recipients.length === 0 ? (
                <TableRow><TableCell colSpan={6} align="center">Chưa có người hoặc nhóm nhận</TableCell></TableRow>
              ) : config.recipients.map((recipient) => (
                <TableRow key={recipient._id}>
                  <TableCell>{recipient.label || "Không có nhãn"}</TableCell>
                  <TableCell>{recipient.chatId}</TableCell>
                  <TableCell>{recipient.type === "group" ? "Nhóm" : "Cá nhân"}</TableCell>
                  <TableCell align="center">{recipient.enabled ? "Có" : "Không"}</TableCell>
                  <TableCell>{recipient.notifyTypes.includes("new_order") ? "Đơn hàng mới" : "Không có"}</TableCell>
                  <TableCell align="right">
                    <Tooltip title="Gửi thử"><span><Button size="small" onClick={() => sendTest(recipient)} disabled={!config.botConfigured || !recipient.enabled} startIcon={<SendIcon />}>Gửi thử</Button></span></Tooltip>
                    <Tooltip title="Sửa"><Button aria-label="Sửa người nhận" size="small" onClick={() => openEditDialog(recipient)}><EditIcon fontSize="small" /></Button></Tooltip>
                    <Tooltip title="Xóa"><Button aria-label="Xóa người nhận" size="small" color="error" onClick={() => deleteRecipient(recipient)}><DeleteIcon fontSize="small" /></Button></Tooltip>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} disableScrollLock fullWidth maxWidth="sm">
        <DialogTitle>{editingRecipient ? "Sửa người/nhóm nhận" : "Thêm người/nhóm nhận"}</DialogTitle>
        <DialogContent sx={{ pt: 2 }}>
          <Box sx={{ display: "grid", gap: 2, pt: 1 }}>
            <TextField label="Nhãn" value={recipientForm.label} onChange={(event) => updateRecipientForm("label", event.target.value)} placeholder="Ví dụ: Nhóm HN" fullWidth />
            <TextField label="Chat ID" value={recipientForm.chatId} onChange={(event) => updateRecipientForm("chatId", event.target.value)} required fullWidth />
            <FormControl fullWidth>
              <InputLabel id="telegram-recipient-type">Loại</InputLabel>
              <Select labelId="telegram-recipient-type" label="Loại" value={recipientForm.type} onChange={(event) => updateRecipientForm("type", event.target.value)}>
                <MenuItem value="personal">Cá nhân</MenuItem>
                <MenuItem value="group">Nhóm</MenuItem>
              </Select>
            </FormControl>
            <FormControlLabel control={<Switch checked={recipientForm.enabled} onChange={(event) => updateRecipientForm("enabled", event.target.checked)} />} label="Bật nhận thông báo" />
            <FormControlLabel control={<Checkbox checked={recipientForm.notifyTypes.includes("new_order")} onChange={(event) => updateRecipientForm("notifyTypes", event.target.checked ? ["new_order"] : [])} />} label="Thông báo đơn hàng mới" />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>Hủy</Button>
          <Button variant="contained" onClick={saveRecipient} disabled={saving}>{saving ? "Đang lưu" : "Lưu"}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default TelegramSettings;
