
import { useEffect, useState } from "react";
import {
  Button,
  Box,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Avatar,
  Autocomplete,
} from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import { useNavigate } from "react-router-dom";
import toast from "react-hot-toast";
import {
  createStation,
  deleteStation,
  getStationAdminList,
} from "../api/stationAdministrationApi";
import { usePermissions } from "../context/permissioncontext";

const removeVietnameseTones = (str) => {
  if (!str) return "";
  return str
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
};

const Station = () => {
  const navigate = useNavigate();
  const { can } = usePermissions();
  const canCreate = can("station.create");
  const canDelete = can("station.delete");
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(false);
  const [openDialog, setOpenDialog] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [newStation, setNewStation] = useState({
    stationCode: "",
    stationName: "",
    location: "",
  });

  const fetchStations = async () => {
    setLoading(true);
    try {
      const data = await getStationAdminList();
      setStations(
        data.map((s) => ({
          id: s._id,
          code: s.stationCode,
          inviteCode: s.inviteCode || (s.stationCode && s.inviteSecret ? `${s.stationCode}-${s.inviteSecret}` : s.stationCode),
          name: s.stationName,
          location: s.location,
          productCount: s.productId?.length || 0,
          imgUrl: s.imgUrl || "",
        }))
      );
    } catch (error) {
      console.error("Lỗi khi lấy danh sách trạm:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStations();
  }, []);

  const copyToClipboard = (text) => {
    if (navigator.clipboard && window.isSecureContext) {
      return navigator.clipboard.writeText(text);
    } else {
      const textArea = document.createElement("textarea");
      textArea.value = text;
      textArea.style.position = "fixed";
      textArea.style.left = "-999999px";
      textArea.style.top = "-999999px";
      document.body.appendChild(textArea);
      textArea.focus();
      textArea.select();
      return new Promise((resolve, reject) => {
        if (document.execCommand("copy")) {
          resolve();
        } else {
          reject(new Error("Không thể sao chép"));
        }
        document.body.removeChild(textArea);
      });
    }
  };

  const handleCopyLink = (inviteCode) => {
    const customerOrigin = window.location.origin.includes(":5173")
      ? "http://localhost:3000"
      : window.location.origin;
    const link = `${customerOrigin}/station/${inviteCode}`;
    copyToClipboard(link)
      .then(() => toast.success("Đã sao chép link trạm!"))
      .catch(() => toast.error("Không thể sao chép!"));
  };

  const handleCreateStation = async () => {
    const { stationCode, stationName, location } = newStation;
    const normalizedStationCode = stationCode.trim();

    if (!normalizedStationCode || !stationName.trim()) {
      alert("Mã trạm và tên trạm là bắt buộc");
      return;
    }

    const duplicateStation = stations.find(
      (station) => station.code?.trim().toLocaleLowerCase("vi-VN") === normalizedStationCode.toLocaleLowerCase("vi-VN")
    );
    if (duplicateStation) {
      alert(`Mã trạm "${normalizedStationCode}" đã tồn tại (${duplicateStation.name || "Không có tên"}). Vui lòng dùng mã khác.`);
      return;
    }

    try {
      await createStation({
        stationCode: normalizedStationCode,
        stationName: stationName.trim(),
        location,
      });

      setOpenDialog(false);
      toast.success("Tạo trạm thành công");
      setNewStation({ stationCode: "", stationName: "", location: "" });
      fetchStations();
    } catch (error) {
      alert("Lỗi: " + error.message);
    }
  };

  const handleDeleteStation = async (id, name) => {
    if (!id) return;
    if (!window.confirm(`Bạn có chắc chắn muốn xóa trạm ${name || ""}?`)) return;
    try {
      await deleteStation(id);
      toast.success("Xóa trạm thành công!");
      fetchStations();
    } catch (err) {
      toast.error("Lỗi khi xóa trạm: " + err.message);
    }
  };

  const columns = [
    {
      field: "imgUrl",
      headerName: "Ảnh",
      width: 80,
      sortable: false,
      renderCell: (params) => (
        <Box sx={{ height: "100%", display: "flex", alignItems: "center" }}>
          <Avatar
            src={params.value}
            variant="rounded"
            alt="Station"
            sx={{ width: 56, height: 40 }}
          />
        </Box>
      ),
    },
    { field: "code", headerName: "Mã trạm", flex: 1, minWidth: 140 },
    { field: "name", headerName: "Tên trạm", flex: 1.8, minWidth: 260 },
    { field: "productCount", headerName: "Sản phẩm", width: 100 },
    { field: "location", headerName: "Vị trí", flex: 2, minWidth: 220 },
    {
      field: "actions",
      headerName: "Thao tác",
      width: 280,
      sortable: false,
      renderCell: (params) => (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            width: "100%",
            height: "100%",
            gap: 1,
          }}
        >
          <Button
            variant="contained"
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              navigate(`/station/${params.row.code}`);
            }}
          >
            Chi tiết
          </Button>
          {canDelete && (
            <Button
              variant="contained"
              color="error"
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                handleDeleteStation(params.row.id, params.row.name);
              }}
            >
              Xóa
            </Button>
          )}
          <Button
            variant="outlined"
            color="success"
            size="small"
            onClick={(e) => {
              e.stopPropagation();
              handleCopyLink(params.row.inviteCode);
            }}
          >
            Copy Link
          </Button>
        </Box>
      ),
    },
  ];

  const filteredStations = stations.filter((station) => {
    const normalizedSearch = removeVietnameseTones(searchQuery);
    const normalizedName = removeVietnameseTones(station.name);
    const normalizedCode = removeVietnameseTones(station.code);
    return (
      normalizedName.includes(normalizedSearch) ||
      normalizedCode.includes(normalizedSearch)
    );
  });

  return (
    <Box sx={{ p: 2 }} className="admin-list-page">
      <div className="sticky-header">
        <h2>Quản lý danh sách trạm trộn</h2>
        <Box sx={{ display: "flex", gap: 2, alignItems: "center", mt: 1, flexWrap: "wrap" }}>
          {canCreate && (
            <Button
              variant="contained"
              color="primary"
              onClick={() => setOpenDialog(true)}
              sx={{ height: 40 }}
            >
              Thêm trạm
            </Button>
          )}
          <Autocomplete
            freeSolo
            size="small"
            options={stations}
            getOptionLabel={(option) => {
              if (typeof option === "string") return option;
              return option.name ? `${option.name} (${option.code})` : option.code || "";
            }}
            onInputChange={(event, newInputValue) => {
              setSearchQuery(newInputValue);
            }}
            onChange={(event, newValue) => {
              if (newValue) {
                if (typeof newValue === "string") {
                  setSearchQuery(newValue);
                } else {
                  setSearchQuery(newValue.name || newValue.code || "");
                }
              } else {
                setSearchQuery("");
              }
            }}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Tìm kiếm trạm..."
                placeholder="Nhập tên hoặc mã trạm..."
                variant="outlined"
                size="small"
                sx={{ width: 300, bgcolor: "white" }}
              />
            )}
            filterOptions={(options, state) => {
              const inputValue = removeVietnameseTones(state.inputValue);
              return options.filter((option) => {
                const nameMatch = removeVietnameseTones(option.name).includes(inputValue);
                const codeMatch = removeVietnameseTones(option.code).includes(inputValue);
                return nameMatch || codeMatch;
              });
            }}
          />
        </Box>
      </div>

      <Box
        className="admin-list-table"
        sx={{
          width: "100%",
          height: { xs: "calc(100dvh - 180px)", md: "auto" },
          minHeight: { xs: 360, md: 0 },
        }}
      >
        <DataGrid
          rows={filteredStations}
          columns={columns}
          pageSize={5}
          rowsPerPageOptions={[5]}
          loading={loading}
          disableColumnMenu
          disableRowSelectionOnClick
          onRowClick={(params) => navigate(`/station/${params.row.code}`)}
          sx={{
            backgroundColor: "#FFFFFF",
            "& .MuiDataGrid-columnHeaders": {
              borderBottom: "1px solid #000",
            },
            "& .MuiDataGrid-virtualScroller": {
              backgroundColor: "#FFFFFF",
            },
            "& .MuiDataGrid-cell": {
              alignItems: "center",
              borderBottom: "1px solid #000",
            },
            "& .MuiDataGrid-row": {
              cursor: "pointer",
              backgroundColor: "#FFFFFF",
            },
            "& .MuiDataGrid-footerContainer": {
              backgroundColor: "#FFFFFF",
            },
          }}
        />
      </Box>

      <Dialog
        open={openDialog}
        onClose={() => setOpenDialog(false)}
        disableScrollLock
        maxWidth="xs"
        fullWidth
      >
        <DialogTitle>Thêm trạm mới</DialogTitle>
        <DialogContent
          sx={{ display: "flex", flexDirection: "column", gap: 2 }}
        >
          <TextField
            label="Mã trạm"
            value={newStation.stationCode}
            onChange={(e) =>
              setNewStation({ ...newStation, stationCode: e.target.value })
            }
            required
            size="small"
            sx={{ mt: 1 }}
          />
          <TextField
            label="Tên trạm"
            value={newStation.stationName}
            onChange={(e) =>
              setNewStation({ ...newStation, stationName: e.target.value })
            }
            required
            size="small"
          />
          <TextField
            label="Vị trí"
            value={newStation.location}
            onChange={(e) =>
              setNewStation({ ...newStation, location: e.target.value })
            }
            size="small"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpenDialog(false)}>Hủy</Button>
          <Button variant="contained" onClick={handleCreateStation}>
            Tạo
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default Station;
