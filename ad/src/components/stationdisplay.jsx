
import { useEffect, useState, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  TextField,
  Button,
  CircularProgress,
  Alert,
  Stack,
  Avatar,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
  FormControlLabel,
  Switch,
  Tabs,
  Tab,
} from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import ExcelJS from "exceljs";
import { usePermissions } from "../context/permissioncontext";
import {
  deleteStation,
  getStationByCode,
  getStationImportOrders,
  getStationProducts,
  getStationProductsByCodes,
  replaceStationImage,
  searchStationProducts,
  updateStationDetails,
  updateStationProducts,
} from "../api/stationAdministrationApi";

const StationDisplay = () => {
  const { code } = useParams();
  const navigate = useNavigate();
  const { can } = usePermissions();
  const canEdit = can("station.edit");
  const canDelete = can("station.delete");
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [form, setForm] = useState({
    stationCode: "",
    stationName: "",
    location: "",
    allowPublicSignup: true,
  });

  const fileInputRef = useRef();
  const fileExcelRef = useRef();
  const [isImportingExcel, setIsImportingExcel] = useState(false);

  const [openOrderDialog, setOpenOrderDialog] = useState(false);
  const [openProductDialog, setOpenProductDialog] = useState(false);
  const [orderType, setOrderType] = useState("ep"); // "ep" or "ip"
  const [orderSearchText, setOrderSearchText] = useState("");
  const [ordersList, setOrdersList] = useState([]);
  const [ordersLoading, setOrdersLoading] = useState(false);
  const orderDebounceRef = useRef(null);

  const fetchOrders = async (type, search) => {
    setOrdersLoading(true);
    try {
      setOrdersList(await getStationImportOrders({ type, search }));
    } catch (err) {
      console.error("Lỗi khi tải đơn hàng:", err);
    } finally {
      setOrdersLoading(false);
    }
  };

  useEffect(() => {
    if (!openOrderDialog) return;
    if (orderDebounceRef.current) clearTimeout(orderDebounceRef.current);
    orderDebounceRef.current = setTimeout(() => {
      fetchOrders(orderType, orderSearchText);
    }, 300);
    return () => clearTimeout(orderDebounceRef.current);
  }, [orderType, orderSearchText, openOrderDialog]);

  const handleOpenOrderDialog = () => {
    setOrderType("ep");
    setOrderSearchText("");
    setOrdersList([]);
    setOpenOrderDialog(true);
  };

  const handleImportFromOrder = async (order) => {
    if (!station?._id || !order) return;
    try {
      const idsFromOrder = (order.productList || []).map((p) => p.productId).filter(Boolean);
      if (!idsFromOrder.length) {
        alert("Đơn hàng này không có sản phẩm nào hợp lệ.");
        return;
      }
      const currentIds = station.productId || [];
      const newIds = Array.from(new Set([...currentIds, ...idsFromOrder]));

      const updatedStation = await updateStationProducts(station._id, newIds, {
        failureMessage: "Cập nhật sản phẩm vào trạm thất bại",
      });
      setStation(updatedStation);
      setOpenOrderDialog(false);
      alert("Đã nhập sản phẩm từ đơn hàng thành công");
    } catch (err) {
      console.error("Lỗi nhập sản phẩm từ đơn hàng:", err);
      alert("Lỗi khi nhập sản phẩm: " + err.message);
    }
  };

  const [searchInput, setSearchInput] = useState({ name: "", code: "" });
  const [searchResults, setSearchResults] = useState([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [productData, setProductData] = useState([]);
  const debounceTimeout = useRef(null);

  useEffect(() => {
    if (!searchInput.name && !searchInput.code) {
      setSearchResults([]);
      return;
    }

    if (debounceTimeout.current) clearTimeout(debounceTimeout.current);

    debounceTimeout.current = setTimeout(async () => {
      try {
        setSearchLoading(true);
        const data = await searchStationProducts(searchInput);
        setSearchResults(data.products || []);
      } catch (err) {
        console.error("Lỗi tìm sản phẩm:", err);
      } finally {
        setSearchLoading(false);
      }
    }, 1000);
  }, [searchInput]);

  useEffect(() => {
    const fetchStation = async () => {
      try {
        const data = await getStationByCode(code);
        setStation(data);
        setForm({
          stationCode: data.stationCode || "",
          stationName: data.stationName || "",
          location: data.location || "",
          allowPublicSignup: data.allowPublicSignup !== false,
        });
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    fetchStation();
  }, [code]);

  useEffect(() => {
    const fetchProductData = async () => {
      if (!station?.productId?.length) return setProductData([]);
      const data = await getStationProducts(station.productId);
      setProductData(data.products || []);
    };
    fetchProductData();
  }, [station?.productId]);

  const handleChange = (field) => (e) => {
    setForm({ ...form, [field]: e.target.value });
  };

  const handleUpdate = async () => {
    if (!station?._id) return;
    setSaving(true);
    try {
      const updated = await updateStationDetails(station._id, form);
      setStation(updated);
      alert("Cập nhật thành công!");
    } catch (err) {
      alert("Lỗi khi cập nhật: " + err.message);
    } finally {
      setSaving(false);
    }
  };

  const getInviteCode = () => (
    station?.inviteCode || (station?.stationCode && station?.inviteSecret
      ? `${station.stationCode}-${station.inviteSecret}`
      : "")
  );



  const handleDeleteStation = async () => {
    if (!station?._id) return;
    if (!window.confirm(`Bạn có chắc chắn muốn xóa trạm ${station.stationName || ""}?`)) return;
    try {
      await deleteStation(station._id);
      alert("Xóa trạm thành công!");
      navigate("/station");
    } catch (err) {
      alert("Lỗi khi xóa trạm: " + err.message);
    }
  };

  const handleAddProduct = async (productId) => {
    if (!station?._id) return;
    const currentIds = station.productId || [];
    if (currentIds.includes(productId)) return;

    const updatedIds = [...currentIds, productId];
    const data = await updateStationProducts(station._id, updatedIds);
    setStation(data);
    setOpenProductDialog(false);
    setSearchInput({ name: "", code: "" });
  };

  const handleRemoveProduct = async (productId) => {
    const updatedIds = (station.productId || []).filter((id) => id !== productId);
    const data = await updateStationProducts(station._id, updatedIds);
    setStation(data);
  };

  const handleRemoveAllProducts = async () => {
    if (!station?._id) return;
    if (!window.confirm("Bạn có chắc chắn muốn xóa toàn bộ sản phẩm khỏi trạm này?")) return;
    try {
      const data = await updateStationProducts(station._id, [], {
        failureMessage: "Không thể xóa sản phẩm",
      });
      setStation(data);
      alert("Đã xóa toàn bộ sản phẩm khỏi trạm thành công");
    } catch (err) {
      alert("Lỗi khi xóa toàn bộ sản phẩm: " + err.message);
    }
  };

  const handleUploadImage = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (e) => {
    const file = e.target.files[0];
    if (!file || !station?._id) return;

    try {
      const updated = await replaceStationImage(
        station._id,
        Boolean(station.imgUrl),
        file,
      );
      setStation(updated.station || updated);
      alert("Ảnh đã được cập nhật");
    } catch (err) {
      alert("Lỗi khi upload ảnh: " + err.message);
    }
  };

  const handleImportExcel = async (event) => {
    const file = event.target.files[0];
    if (!file || !station?._id) return;
    setIsImportingExcel(true);

    try {
      const buffer = await file.arrayBuffer();
      const workbook = new ExcelJS.Workbook();
      await workbook.xlsx.load(buffer);
      const sheet = workbook.getWorksheet(1);
      const headerRow = sheet.getRow(2).values.slice(1);
      const codeColIndex = headerRow.findIndex((h) =>
        h?.toString().toLowerCase().includes("mã")
      );

      if (codeColIndex === -1) {
        alert("Không tìm thấy cột 'Mã sản phẩm'");
        return;
      }

      const codes = [];
      sheet.eachRow((row, idx) => {
        if (idx <= 2) return;
        const code = row.getCell(codeColIndex + 1).text?.trim();
        if (code) codes.push(code);
      });

      if (!codes.length) {
        alert("Không có mã sản phẩm hợp lệ trong file");
        return;
      }

      const data = await getStationProductsByCodes(codes);
      const idsFromExcel = (data.products || []).map((p) => p._id);
      const currentIds = station.productId || [];
      const newIds = Array.from(new Set([...currentIds, ...idsFromExcel]));

      const updatedStation = await updateStationProducts(station._id, newIds);
      setStation(updatedStation);
      alert("Đã nhập sản phẩm từ Excel thành công");
    } catch (err) {
      console.error("Lỗi nhập Excel:", err);
      alert("Lỗi khi nhập Excel");
    } finally {
      setIsImportingExcel(false);
    }
  };

  const productColumns = [
    {
      field: "image",
      headerName: "Hình ảnh",
      flex: 1,
      minWidth: 80,
      renderCell: (params) => (
        <Box sx={{ display: "flex", alignItems: "center", width: "100%", height: "100%" }}>
          <Avatar src={params.value} variant="rounded" />
        </Box>
      ),
    },
    { field: "name", headerName: "Tên sản phẩm", flex: 2, minWidth: 160 },
    { field: "code", headerName: "Mã sản phẩm", flex: 1.5, minWidth: 120 },
    {
      field: "actions",
      headerName: "Thao tác",
      flex: 1,
      minWidth: 100,
      renderCell: (params) => (
        canEdit ? (
          <Button variant="contained" color="error" size="small" onClick={() => handleRemoveProduct(params.row._id)}>
            Xóa
          </Button>
        ) : null
      ),
    },
  ];

  const productRows = productData.map((p) => ({
    id: p._id,
    _id: p._id,
    name: p.name,
    code: p.code,
    image: p.variant?.[0]?.imgUrl || "",
  }));

  if (loading) return <CircularProgress />;
  if (error) return <Alert severity="error">{error}</Alert>;

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" gutterBottom>Thông tin trạm</Typography>

      <Box mb={3}>
        <Typography variant="subtitle1" gutterBottom>Ảnh trạm</Typography>
        {station?.imgUrl ? (
          <img
            src={station.imgUrl}
            alt="Station"
            onClick={canEdit ? handleUploadImage : undefined}
            style={{
              width: 200,
              height: 120,
              objectFit: "cover",
              borderRadius: 8,
              cursor: canEdit ? "pointer" : "default",
              border: "1px solid #ccc",
            }}
          />
        ) : (
          canEdit ? <Button variant="outlined" onClick={handleUploadImage}>Thêm ảnh</Button> : null
        )}
        {canEdit && <input type="file" accept="image/*" ref={fileInputRef} style={{ display: "none" }} onChange={handleFileChange} />}
      </Box>

      <Stack spacing={2} maxWidth={400} mb={4}>
        <TextField label="Mã trạm" value={form.stationCode} onChange={handleChange("stationCode")} fullWidth size="small" disabled={!canEdit} />
        <TextField label="Tên trạm" value={form.stationName} onChange={handleChange("stationName")} fullWidth size="small" disabled={!canEdit} />
        <TextField label="Vị trí" value={form.location} onChange={handleChange("location")} fullWidth size="small" disabled={!canEdit} />
        <TextField label="Public link code" value={getInviteCode()} fullWidth size="small" InputProps={{ readOnly: true }} />
        <FormControlLabel
          control={
            <Switch
              checked={form.allowPublicSignup}
              onChange={(e) => setForm({ ...form, allowPublicSignup: e.target.checked })}
              disabled={!canEdit}
            />
          }
          label="Cho phép đăng ký công khai"
        />
        <Stack direction="row" spacing={2}>
          {canEdit && (
            <Button variant="contained" onClick={handleUpdate} disabled={saving} sx={{ flex: 1 }}>
              {saving ? "Đang cập nhật..." : "Cập nhật"}
            </Button>
          )}

          {canDelete && (
            <Button variant="contained" color="error" onClick={handleDeleteStation} sx={{ flex: 1 }}>
              Xóa trạm
            </Button>
          )}
        </Stack>
      </Stack>

      <Typography variant="h6" gutterBottom>Danh sách sản phẩm</Typography>
      <Box display="flex" gap={2} mb={2}>
        {canEdit && <Button variant="contained" onClick={() => setOpenProductDialog(true)}>Thêm sản phẩm</Button>}
        {canEdit && (
          <Button variant="outlined" onClick={() => fileExcelRef.current?.click()} disabled={isImportingExcel}>
            Nhập Excel
          </Button>
        )}
        {canEdit && (
          <Button variant="outlined" onClick={handleOpenOrderDialog}>
            Nhập Từ Đơn Hàng
          </Button>
        )}
        {canEdit && (
          <Button variant="outlined" color="error" onClick={handleRemoveAllProducts}>
            Xóa Toàn Bộ Sản Phẩm
          </Button>
        )}
        {canEdit && (
          <input
            type="file"
            accept=".xlsx"
            ref={fileExcelRef}
            style={{ display: "none" }}
            onChange={handleImportExcel}
          />
        )}
      </Box>

      <DataGrid
        rows={productRows}
        columns={productColumns}
        pageSize={5}
        rowsPerPageOptions={[5]}
        disableColumnMenu
        disableRowSelectionOnClick
      />

      <Dialog open={openProductDialog} onClose={() => setOpenProductDialog(false)} disableScrollLock maxWidth="sm">
        <DialogTitle>Tìm kiếm và thêm sản phẩm</DialogTitle>
        <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
          <TextField label="Tên sản phẩm" value={searchInput.name} onChange={(e) => setSearchInput({ ...searchInput, name: e.target.value })} fullWidth size="small" />
          <TextField label="Mã sản phẩm" value={searchInput.code} onChange={(e) => setSearchInput({ ...searchInput, code: e.target.value })} fullWidth size="small" />
          {searchLoading && <Typography>Đang tìm kiếm...</Typography>}
          <List sx={{ maxHeight: 400, overflowY: "auto", border: "1px solid #ddd", borderRadius: 1 }}>
            {searchResults.map((product) => (
              <ListItem key={product._id} button onClick={() => handleAddProduct(product._id)}>
                <ListItemAvatar>
                  <Avatar src={product.variant?.[0]?.imgUrl || ""} variant="square" />
                </ListItemAvatar>
                <ListItemText primary={product.name} secondary={`Mã: ${product.code}`} />
              </ListItem>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenProductDialog(false)}>Đóng</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={openOrderDialog} onClose={() => setOpenOrderDialog(false)} disableScrollLock maxWidth="md" fullWidth>
        <DialogTitle>Chọn đơn hàng nhập/xuất để nhập sản phẩm</DialogTitle>
        <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
          <Tabs
            value={orderType}
            onChange={(e, val) => setOrderType(val)}
            indicatorColor="primary"
            textColor="primary"
            variant="fullWidth"
          >
            <Tab value="ep" label="Đơn hàng xuất" />
            <Tab value="ip" label="Đơn hàng nhập" />
          </Tabs>

          <TextField
            label="Tìm kiếm theo tên đơn hàng..."
            variant="outlined"
            size="small"
            fullWidth
            value={orderSearchText}
            onChange={(e) => setOrderSearchText(e.target.value)}
          />

          <Box sx={{ height: 350, display: "flex", flexDirection: "column", overflow: "hidden" }}>
            {ordersLoading ? (
              <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", flexGrow: 1 }}>
                <CircularProgress />
              </Box>
            ) : ordersList.length === 0 ? (
              <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", flexGrow: 1 }}>
                <Typography>Không tìm thấy đơn hàng nào.</Typography>
              </Box>
            ) : (
              <List sx={{ height: "100%", overflowY: "auto", border: "1px solid #ddd", borderRadius: 1 }}>
                {ordersList.map((order) => (
                  <ListItem
                    key={order._id}
                    button
                    onClick={() => handleImportFromOrder(order)}
                    sx={{
                      borderBottom: "1px solid #eee",
                      "&:hover": { backgroundColor: "#f5f5f5" }
                    }}
                  >
                    <ListItemText
                      primary={order.orderName || `Đơn hàng #${order._id.slice(-6)}`}
                      secondary={`Người tạo: ${order.userName} - Số sản phẩm: ${order.productList?.length || 0} - Ngày tạo: ${new Date(order.createdAt).toLocaleDateString("vi-VN")}`}
                    />
                    <Button variant="contained" size="small">Chọn</Button>
                  </ListItem>
                ))}
              </List>
            )}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenOrderDialog(false)}>Đóng</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default StationDisplay;
