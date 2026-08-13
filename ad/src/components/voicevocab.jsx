import { useEffect, useState } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  TextField,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  Typography,
  Paper,
  Chip,
  CircularProgress,
  Tabs,
  Tab,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import toast from "react-hot-toast";
import {
  createVoiceVocabularyEntry,
  deleteVoiceVocabularyEntry,
  getVoiceVocabulary,
  updateVoiceVocabularyEntry,
} from "../api/voiceApi";

// Metadata cho từng nhóm từ vựng: nhãn hiển thị + kiểu (simple = mảng chuỗi,
// object = mảng bản ghi có nhiều trường). Dùng chung để render bảng + form.
const GROUPS = [
  {
    key: "brands",
    label: "Thương hiệu",
    kind: "simple",
    hint: "Danh sách hãng chuẩn. AI ánh xạ tên hãng người dùng nói về đúng một giá trị ở đây.",
  },
  {
    key: "types",
    label: "Loại sản phẩm",
    kind: "simple",
    hint: "Danh sách loại sản phẩm chuẩn (Aptomat, PLC, Biến tần...).",
  },
  {
    key: "stopwords",
    label: "Từ dẫn (bỏ khi tìm)",
    kind: "simple",
    hint: "Từ đệm/nghi vấn ở đầu-cuối câu sẽ bị bóc (tìm, cho tôi, còn hàng không...). Nhập dạng đã bỏ dấu.",
  },
  {
    key: "brandAliases",
    label: "Cách đọc hãng",
    kind: "brandAliases",
    hint: "Cách đọc lóng của hãng (đã bỏ dấu). VD: Omron <- om ron, om rong.",
  },
  {
    key: "typeAliases",
    label: "Cách gọi loại",
    kind: "typeAliases",
    hint: "Cách gọi dân dã của loại (đã bỏ dấu). VD: Aptomat <- at, at to mat, cau dao tu dong.",
  },
  {
    key: "intentAliases",
    label: "Ý định (intent)",
    kind: "intentAliases",
    hint: "Từ đồng nghĩa cho mỗi ý định (đã bỏ dấu). Hiện chỉ search_product tác động tới hành vi tìm kiếm; add/update/delete được lưu để dùng cho tính năng ra lệnh bằng giọng nói sau này.",
  },
  {
    key: "codeMap",
    label: "Mã model",
    kind: "codeMap",
    hint: "Mã thiết bị đặc thù thuộc một hãng (S7-1200 -> Siemens/PLC). Nâng cao: patterns là regex đã bỏ dấu.",
  },
];

const VoiceVocab = () => {
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState({
    stopwords: [],
    brands: [],
    types: [],
    brandAliases: [],
    typeAliases: [],
    intentAliases: [],
    codeMap: [],
  });
  const [tab, setTab] = useState(0);

  // Dialog thêm/sửa dùng chung; mode = 'add' | 'edit'.
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogMode, setDialogMode] = useState("add");
  const [activeGroup, setActiveGroup] = useState(GROUPS[0]);
  const [form, setForm] = useState({});
  const [originalKey, setOriginalKey] = useState(""); // giá trị/key gốc khi sửa
  const [submitting, setSubmitting] = useState(false);

  // Dialog xác nhận xóa.
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState(null); // { group, item }

  const fetchVocab = async () => {
    setLoading(true);
    try {
      const res = await getVoiceVocabulary();
      const json = await res.json();
      if (res.ok && json.success) {
        setData(json.data);
      } else {
        toast.error(json.message || "Không tải được từ vựng voice.");
      }
    } catch (err) {
      console.error("Lỗi tải từ vựng voice:", err);
      toast.error("Lỗi kết nối khi tải từ vựng voice.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchVocab();
  }, []);

  const currentGroup = GROUPS[tab];

  // Khởi tạo form rỗng theo kiểu nhóm.
  const emptyForm = (group) => {
    switch (group.kind) {
      case "simple":
        return { value: "" };
      case "brandAliases":
        return { name: "", aliases: "" };
      case "typeAliases":
        return { type: "", keyword: "", aliases: "" };
      case "intentAliases":
        return { intent: "", label: "", aliases: "" };
      case "codeMap":
        return { code: "", keyword: "", brand: "", type: "", patterns: "", compact: "" };
      default:
        return {};
    }
  };

  const openAdd = (group) => {
    setActiveGroup(group);
    setDialogMode("add");
    setForm(emptyForm(group));
    setOriginalKey("");
    setDialogOpen(true);
  };

  const openEdit = (group, item) => {
    setActiveGroup(group);
    setDialogMode("edit");
    if (group.kind === "simple") {
      setForm({ value: item });
      setOriginalKey(item);
    } else if (group.kind === "brandAliases") {
      setForm({ name: item.name, aliases: (item.aliases || []).join(", ") });
      setOriginalKey(item.name);
    } else if (group.kind === "typeAliases") {
      setForm({ type: item.type, keyword: item.keyword || "", aliases: (item.aliases || []).join(", ") });
      setOriginalKey(item.type);
    } else if (group.kind === "intentAliases") {
      setForm({ intent: item.intent, label: item.label || "", aliases: (item.aliases || []).join(", ") });
      setOriginalKey(item.intent);
    } else if (group.kind === "codeMap") {
      setForm({
        code: item.code,
        keyword: item.keyword || "",
        brand: item.brand || "",
        type: item.type || "",
        patterns: (item.patterns || []).join(", "),
        compact: item.compact || "",
      });
      setOriginalKey(item.code);
    }
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setForm({});
    setOriginalKey("");
  };

  // Dựng body request theo nhóm + mode.
  const buildBody = (group, mode) => {
    if (group.kind === "simple") {
      return mode === "add"
        ? { value: form.value }
        : { oldValue: originalKey, newValue: form.value };
    }
    if (group.kind === "brandAliases") {
      return { name: form.name, aliases: form.aliases };
    }
    if (group.kind === "typeAliases") {
      return { type: form.type, keyword: form.keyword, aliases: form.aliases };
    }
    if (group.kind === "intentAliases") {
      return { intent: form.intent, label: form.label, aliases: form.aliases };
    }
    if (group.kind === "codeMap") {
      return {
        code: form.code,
        keyword: form.keyword,
        brand: form.brand,
        type: form.type,
        patterns: form.patterns,
        compact: form.compact,
      };
    }
    return {};
  };

  const handleSubmit = async () => {
    const group = activeGroup;
    setSubmitting(true);
    try {
      const body = buildBody(group, dialogMode);
      const res = await (dialogMode === "add"
        ? createVoiceVocabularyEntry(group.key, body)
        : updateVoiceVocabularyEntry(group.key, body));
      const json = await res.json();
      if (res.ok && json.success) {
        toast.success(json.message || "Lưu thành công.");
        closeDialog();
        fetchVocab();
      } else {
        toast.error(json.message || "Không lưu được.");
      }
    } catch (err) {
      console.error("Lỗi lưu từ vựng voice:", err);
      toast.error("Lỗi kết nối khi lưu.");
    } finally {
      setSubmitting(false);
    }
  };

  const askDelete = (group, item) => {
    setDeleteTarget({ group, item });
    setDeleteOpen(true);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    const { group, item } = deleteTarget;
    let body;
    if (group.kind === "simple") {
      body = { value: item };
    } else if (group.kind === "brandAliases") {
      body = { name: item.name };
    } else if (group.kind === "typeAliases") {
      body = { type: item.type };
    } else if (group.kind === "intentAliases") {
      body = { intent: item.intent };
    } else if (group.kind === "codeMap") {
      body = { code: item.code };
    }
    try {
      const res = await deleteVoiceVocabularyEntry(group.key, body);
      const json = await res.json();
      if (res.ok && json.success) {
        toast.success(json.message || "Xóa thành công.");
        setDeleteOpenSafe(false);
        fetchVocab();
      } else {
        toast.error(json.message || "Không xóa được.");
      }
    } catch (err) {
      console.error("Lỗi xóa từ vựng voice:", err);
      toast.error("Lỗi kết nối khi xóa.");
    }
  };

  // Đóng dialog xóa an toàn (tránh giữ target cũ).
  const setDeleteOpenSafe = (open) => {
    setDeleteOpen(open);
    if (!open) setDeleteTarget(null);
  };

  const renderSimpleTable = (group) => {
    const rows = data[group.key] || [];
    return (
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell>Giá trị</TableCell>
            <TableCell align="right" sx={{ width: 160 }}>Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((v, i) => (
            <TableRow key={i} hover>
              <TableCell>{v}</TableCell>
              <TableCell align="right">
                <Button size="small" onClick={() => openEdit(group, v)}>Sửa</Button>
                <Button size="small" color="error" onClick={() => askDelete(group, v)}>Xóa</Button>
              </TableCell>
            </TableRow>
          ))}
          {rows.length === 0 && (
            <TableRow>
              <TableCell colSpan={2} align="center" sx={{ color: "text.secondary", py: 3 }}>
                Chưa có mục nào.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    );
  };

  const renderBrandAliasTable = (group) => {
    const rows = data.brandAliases || [];
    return (
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 200 }}>Thương hiệu</TableCell>
            <TableCell>Cách đọc (alias)</TableCell>
            <TableCell align="right" sx={{ width: 160 }}>Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((item, i) => (
            <TableRow key={i} hover>
              <TableCell><strong>{item.name}</strong></TableCell>
              <TableCell>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {(item.aliases || []).map((a, j) => (
                    <Chip key={j} label={a} size="small" variant="outlined" />
                  ))}
                </Box>
              </TableCell>
              <TableCell align="right">
                <Button size="small" onClick={() => openEdit(group, item)}>Sửa</Button>
                <Button size="small" color="error" onClick={() => askDelete(group, item)}>Xóa</Button>
              </TableCell>
            </TableRow>
          ))}
          {rows.length === 0 && (
            <TableRow>
              <TableCell colSpan={3} align="center" sx={{ color: "text.secondary", py: 3 }}>
                Chưa có mục nào.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    );
  };

  const renderTypeAliasTable = (group) => {
    const rows = data.typeAliases || [];
    return (
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 180 }}>Loại</TableCell>
            <TableCell sx={{ width: 160 }}>Keyword</TableCell>
            <TableCell>Cách gọi (alias)</TableCell>
            <TableCell align="right" sx={{ width: 160 }}>Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((item, i) => (
            <TableRow key={i} hover>
              <TableCell><strong>{item.type}</strong></TableCell>
              <TableCell>{item.keyword}</TableCell>
              <TableCell>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {(item.aliases || []).map((a, j) => (
                    <Chip key={j} label={a} size="small" variant="outlined" />
                  ))}
                </Box>
              </TableCell>
              <TableCell align="right">
                <Button size="small" onClick={() => openEdit(group, item)}>Sửa</Button>
                <Button size="small" color="error" onClick={() => askDelete(group, item)}>Xóa</Button>
              </TableCell>
            </TableRow>
          ))}
          {rows.length === 0 && (
            <TableRow>
              <TableCell colSpan={4} align="center" sx={{ color: "text.secondary", py: 3 }}>
                Chưa có mục nào.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    );
  };

  const renderIntentAliasTable = (group) => {
    const rows = data.intentAliases || [];
    return (
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 180 }}>Intent</TableCell>
            <TableCell sx={{ width: 160 }}>Nhãn</TableCell>
            <TableCell>Aliases</TableCell>
            <TableCell align="right" sx={{ width: 160 }}>Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((item, i) => (
            <TableRow key={i} hover>
              <TableCell><strong>{item.intent}</strong></TableCell>
              <TableCell>{item.label || "-"}</TableCell>
              <TableCell>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {(item.aliases || []).map((a, j) => (
                    <Chip key={j} label={a} size="small" variant="outlined" />
                  ))}
                </Box>
              </TableCell>
              <TableCell align="right">
                <Button size="small" onClick={() => openEdit(group, item)}>Sửa</Button>
                <Button size="small" color="error" onClick={() => askDelete(group, item)}>Xóa</Button>
              </TableCell>
            </TableRow>
          ))}
          {rows.length === 0 && (
            <TableRow>
              <TableCell colSpan={4} align="center" sx={{ color: "text.secondary", py: 3 }}>
                Chưa có mục nào.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    );
  };

  const renderCodeMapTable = (group) => {
    const rows = data.codeMap || [];
    return (
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 140 }}>Mã</TableCell>
            <TableCell sx={{ width: 160 }}>Keyword</TableCell>
            <TableCell sx={{ width: 120 }}>Hãng</TableCell>
            <TableCell sx={{ width: 100 }}>Loại</TableCell>
            <TableCell>Patterns / Compact</TableCell>
            <TableCell align="right" sx={{ width: 160 }}>Thao tác</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((item, i) => (
            <TableRow key={i} hover>
              <TableCell><strong>{item.code}</strong></TableCell>
              <TableCell>{item.keyword}</TableCell>
              <TableCell>{item.brand || "-"}</TableCell>
              <TableCell>{item.type || "-"}</TableCell>
              <TableCell>
                <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                  {(item.patterns || []).map((p, j) => (
                    <Chip key={j} label={p} size="small" variant="outlined" />
                  ))}
                  {item.compact && <Chip label={`compact: ${item.compact}`} size="small" color="info" variant="outlined" />}
                </Box>
              </TableCell>
              <TableCell align="right">
                <Button size="small" onClick={() => openEdit(group, item)}>Sửa</Button>
                <Button size="small" color="error" onClick={() => askDelete(group, item)}>Xóa</Button>
              </TableCell>
            </TableRow>
          ))}
          {rows.length === 0 && (
            <TableRow>
              <TableCell colSpan={6} align="center" sx={{ color: "text.secondary", py: 3 }}>
                Chưa có mục nào.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    );
  };

  const renderTable = (group) => {
    switch (group.kind) {
      case "simple":
        return renderSimpleTable(group);
      case "brandAliases":
        return renderBrandAliasTable(group);
      case "typeAliases":
        return renderTypeAliasTable(group);
      case "intentAliases":
        return renderIntentAliasTable(group);
      case "codeMap":
        return renderCodeMapTable(group);
      default:
        return null;
    }
  };

  // Form fields trong dialog theo nhóm.
  const renderFormFields = () => {
    const group = activeGroup;
    if (group.kind === "simple") {
      return (
        <TextField
          autoFocus
          fullWidth
          label="Giá trị"
          value={form.value || ""}
          onChange={(e) => setForm({ ...form, value: e.target.value })}
          sx={{ mt: 1 }}
          size="small"
        />
      );
    }
    if (group.kind === "brandAliases") {
      return (
        <>
          <TextField
            autoFocus
            fullWidth
            label="Tên thương hiệu (chuẩn)"
            value={form.name || ""}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            disabled={dialogMode === "edit"}
            sx={{ mt: 1 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Cách đọc, cách nhau bằng dấu phẩy (đã bỏ dấu)"
            placeholder="om ron, om rong"
            value={form.aliases || ""}
            onChange={(e) => setForm({ ...form, aliases: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
            multiline
            minRows={2}
          />
        </>
      );
    }
    if (group.kind === "typeAliases") {
      return (
        <>
          <TextField
            autoFocus
            fullWidth
            label="Tên loại (chuẩn)"
            value={form.type || ""}
            onChange={(e) => setForm({ ...form, type: e.target.value })}
            disabled={dialogMode === "edit"}
            sx={{ mt: 1 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Keyword tìm kiếm (để trống = dùng tên loại)"
            value={form.keyword || ""}
            onChange={(e) => setForm({ ...form, keyword: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Cách gọi, cách nhau bằng dấu phẩy (đã bỏ dấu)"
            placeholder="at, at to mat, cau dao tu dong"
            value={form.aliases || ""}
            onChange={(e) => setForm({ ...form, aliases: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
            multiline
            minRows={2}
          />
        </>
      );
    }
    if (group.kind === "intentAliases") {
      return (
        <>
          <TextField
            autoFocus
            fullWidth
            label="Intent"
            placeholder="add_to_cart"
            value={form.intent || ""}
            onChange={(e) => setForm({ ...form, intent: e.target.value })}
            disabled={dialogMode === "edit"}
            sx={{ mt: 1 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Nhãn hiển thị"
            placeholder="Thêm"
            value={form.label || ""}
            onChange={(e) => setForm({ ...form, label: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Aliases, cách nhau bằng dấu phẩy (đã bỏ dấu)"
            placeholder="them, cho vao, add"
            value={form.aliases || ""}
            onChange={(e) => setForm({ ...form, aliases: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
            multiline
            minRows={2}
          />
        </>
      );
    }
    if (group.kind === "codeMap") {
      return (
        <>
          <TextField
            autoFocus
            fullWidth
            label="Mã model (VD: S7-1200)"
            value={form.code || ""}
            onChange={(e) => setForm({ ...form, code: e.target.value })}
            disabled={dialogMode === "edit"}
            sx={{ mt: 1 }}
            size="small"
          />
          <TextField
            fullWidth
            label="Keyword (để trống = dùng mã)"
            value={form.keyword || ""}
            onChange={(e) => setForm({ ...form, keyword: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
          />
          <Box sx={{ display: "flex", gap: 2, mt: 2 }}>
            <TextField
              fullWidth
              label="Hãng (tùy chọn)"
              value={form.brand || ""}
              onChange={(e) => setForm({ ...form, brand: e.target.value })}
              size="small"
            />
            <TextField
              fullWidth
              label="Loại (tùy chọn)"
              value={form.type || ""}
              onChange={(e) => setForm({ ...form, type: e.target.value })}
              size="small"
            />
          </Box>
          <TextField
            fullWidth
            label="Patterns regex, cách nhau bằng dấu phẩy (nâng cao, đã bỏ dấu)"
            placeholder="\\bs\\s*7\\s*[- ]?\\s*1200\\b"
            value={form.patterns || ""}
            onChange={(e) => setForm({ ...form, patterns: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
            multiline
            minRows={2}
          />
          <TextField
            fullWidth
            label="Compact (chuỗi con khi bỏ hết khoảng trắng, VD: s71200)"
            value={form.compact || ""}
            onChange={(e) => setForm({ ...form, compact: e.target.value })}
            sx={{ mt: 2 }}
            size="small"
          />
        </>
      );
    }
    return null;
  };

  return (
    <Box
      sx={{
        p: { xs: 1, md: 2 },
        height: { xs: "calc(100vh - 90px)", md: "calc(100vh - 40px)" },
        boxSizing: "border-box",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
      }}
    >
      <Typography variant="h5" sx={{ fontWeight: "bold", mb: 0.5 }}>
        Từ vựng tìm kiếm giọng nói
      </Typography>
      <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
        Thêm từ để AI hiểu thêm cách người dùng gọi sản phẩm. Sửa ở đây áp dụng ngay cho cả giọng nói lẫn ô nhập chữ, không cần khởi động lại server.
      </Typography>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", flex: 1, py: 6 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Paper
          variant="outlined"
          sx={{
            p: { xs: 1, md: 2 },
            flex: 1,
            minHeight: 0,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}
        >
          <Tabs
            value={tab}
            onChange={(e, v) => setTab(v)}
            variant="scrollable"
            scrollButtons="auto"
            sx={{ mb: 1, flexShrink: 0 }}
          >
            {GROUPS.map((g) => (
              <Tab key={g.key} label={`${g.label} (${(data[g.key] || []).length})`} />
            ))}
          </Tabs>

          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1, flexShrink: 0 }}>
            <Typography variant="body2" sx={{ color: "text.secondary", flex: 1, minWidth: 240 }}>
              {currentGroup.hint}
            </Typography>
            <Button variant="contained" startIcon={<AddIcon />} onClick={() => openAdd(currentGroup)}>
              Thêm
            </Button>
          </Box>

          <Box sx={{ overflow: "auto", flex: 1, minHeight: 0 }}>
            {renderTable(currentGroup)}
          </Box>
        </Paper>
      )}

      {/* Dialog thêm/sửa */}
      <Dialog open={dialogOpen} onClose={closeDialog} disableScrollLock fullWidth maxWidth="sm">
        <DialogTitle>
          {dialogMode === "add" ? "Thêm" : "Sửa"} - {activeGroup.label}
        </DialogTitle>
        <DialogContent>{renderFormFields()}</DialogContent>
        <DialogActions>
          <Button onClick={closeDialog} color="secondary">Hủy</Button>
          <Button onClick={handleSubmit} variant="contained" disabled={submitting}>
            {submitting ? <CircularProgress size={22} color="inherit" /> : "Lưu"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Dialog xác nhận xóa */}
      <Dialog open={deleteOpen} onClose={() => setDeleteOpenSafe(false)} disableScrollLock>
        <DialogTitle>Xác nhận xóa</DialogTitle>
        <DialogContent>
          <Typography>Bạn có chắc chắn muốn xóa mục này khỏi từ vựng?</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleDelete} color="error" variant="contained">Xóa</Button>
          <Button onClick={() => setDeleteOpenSafe(false)} color="secondary" variant="outlined">Hủy</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default VoiceVocab;
