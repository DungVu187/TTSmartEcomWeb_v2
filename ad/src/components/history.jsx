
import { useEffect, useState } from "react";
import {
  Box,
  CircularProgress,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Typography,
  Pagination,
  FormControl,
  Select,
  MenuItem,
  TextField,
  Button,
  Autocomplete,
} from "@mui/material";
import moment from "moment";
import ExcelJS from "exceljs";
import { saveAs } from "file-saver";
import toast from "react-hot-toast";
import { useNavigate } from "react-router-dom";
import {
  getStorageHistory,
  getStorageHistoryExport,
  getStorageHistoryFilterOptions,
} from "../api/adminAuditApi";

const importNoteTypeOptions = [
  { value: "", label: "Tất cả" },
  { value: "nhap_don", label: "Nhập kho theo đơn" },
  { value: "nhap_thu_cong", label: "Nhập kho thủ công" },
  { value: "nhap_ai", label: "Nhập đơn quét AI" },
  { value: "order_line_manual", label: "Trong đơn - gõ tay" },
  { value: "order_line_complete", label: "Trong đơn - tích hoàn thành SP" },
  { value: "order_bulk_complete", label: "Trong đơn - hoàn thành cả đơn" },
];

const exportNoteTypeOptions = [
  { value: "", label: "Tất cả" },
  { value: "xuat_don", label: "Xuất kho theo đơn" },
  { value: "xuat_thu_cong", label: "Xuất kho thủ công" },
  { value: "xuat_ai", label: "Xuất đơn quét AI" },
  { value: "order_line_manual", label: "Trong đơn - gõ tay" },
  { value: "order_line_complete", label: "Trong đơn - tích hoàn thành SP" },
  { value: "order_bulk_complete", label: "Trong đơn - hoàn thành cả đơn" },
  { value: "ban_online", label: "Đơn hàng bán online" },
];

const VOICE_HISTORY_EXPORT_KEY = "voiceHistoryExport";

export const getVoiceHistoryDateRange = (
  datePreset,
  referenceDate = moment(),
  customRange = {},
) => {
  const current = moment(referenceDate);
  if (datePreset === "custom") {
    return {
      startDate: customRange.startDate || "",
      endDate: customRange.endDate || "",
    };
  }
  if (datePreset === "today") {
    const date = current.format("YYYY-MM-DD");
    return { startDate: date, endDate: date };
  }
  if (datePreset === "yesterday") {
    const date = current.subtract(1, "day").format("YYYY-MM-DD");
    return { startDate: date, endDate: date };
  }
  if (datePreset === "this_week") {
    return {
      startDate: current.clone().startOf("isoWeek").format("YYYY-MM-DD"),
      endDate: current.clone().endOf("isoWeek").format("YYYY-MM-DD"),
    };
  }
  if (datePreset === "this_month") {
    return {
      startDate: current.clone().startOf("month").format("YYYY-MM-DD"),
      endDate: current.clone().endOf("month").format("YYYY-MM-DD"),
    };
  }
  return { startDate: "", endDate: "" };
};

const removeVietnameseTones = (str) => {
  if (!str) return "";
  return str
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
};

const getHistoryLabel = (row) => {
  const isImport = row.quantity > 0;

  // Quét AI luôn ưu tiên nhãn quét AI, không dán kèm nhãn khác dù có source gì.
  if (row.isAIScan) {
    return isImport ? "Nhập đơn quét AI" : "Xuất đơn quét AI";
  }

  switch (row.source) {
    case "order_line_manual":
      return isImport ? "Nhập kho (gõ tay trong đơn)" : "Xuất kho (gõ tay trong đơn)";
    case "order_line_complete":
      return isImport ? "Nhập kho (tích hoàn thành SP)" : "Xuất kho (tích hoàn thành SP)";
    case "order_bulk_complete":
      return isImport ? "Nhập kho (hoàn thành cả đơn)" : "Xuất kho (hoàn thành cả đơn)";
    case "product_manual":
      return isImport ? "Nhập kho thủ công" : "Xuất kho thủ công";
    case "online_sale":
      return "Đơn hàng bán online";
    case "online_sale_revert":
      return "Hoàn tác đơn bán online";
    default:
      return row.note
        ? row.note
        : row.orderId
        ? isImport
          ? "Nhập kho theo đơn"
          : "Xuất kho theo đơn"
        : isImport
        ? "Nhập kho thủ công"
        : "Xuất kho thủ công";
  }
};

const History = ({ direction = "import" }) => {
  const historyDirection = direction === "export" ? "export" : "import";
  const noteTypeOptions = historyDirection === "export"
    ? exportNoteTypeOptions
    : importNoteTypeOptions;
  const [histories, setHistories] = useState([]);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [limit, setLimit] = useState(20);

  // bộ lọc
  const [userName, setUserName] = useState("");
  const [orderName, setOrderName] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [noteType, setNoteType] = useState("");

  // debounced values cho tìm kiếm chữ
  const [debouncedUserName, setDebouncedUserName] = useState("");
  const [debouncedOrderName, setDebouncedOrderName] = useState("");
  const [filterOptions, setFilterOptions] = useState({
    userNames: [],
    orderNames: [],
  });

  const navigate = useNavigate();

  // Debounce hiệu ứng gõ phím
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedUserName(userName);
    }, 500);
    return () => clearTimeout(handler);
  }, [userName]);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedOrderName(orderName);
    }, 500);
    return () => clearTimeout(handler);
  }, [orderName]);

  const fetchHistories = async (currentPage = page, currentUserName = debouncedUserName, currentOrderName = debouncedOrderName) => {
    try {
      setLoading(true);
      const res = await getStorageHistory({
        page: currentPage,
        limit,
        direction: historyDirection,
        ...(currentUserName && { userName: currentUserName }),
        ...(currentOrderName && { orderName: currentOrderName }),
        ...(startDate && { startDate }),
        ...(endDate && { endDate }),
        ...(noteType && { noteType }),
      });
      if (!res.ok) throw new Error("Lỗi khi tải dữ liệu lịch sử");
      const data = await res.json();
      setHistories(data.history || []);
      setTotalPages(data.totalPages || 1);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  // Tự động gọi API khi bất kỳ bộ lọc hoặc trang nào thay đổi
  useEffect(() => {
    fetchHistories(page, debouncedUserName, debouncedOrderName);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, limit, startDate, endDate, noteType, debouncedUserName, debouncedOrderName, historyDirection]);

  const fetchFilterOptions = async () => {
    try {
      const res = await getStorageHistoryFilterOptions();
      if (!res.ok) throw new Error("Lỗi khi tải gợi ý lọc lịch sử");
      const data = await res.json();
      setFilterOptions({
        userNames: Array.isArray(data.userNames) ? data.userNames : [],
        orderNames: Array.isArray(data.orderNames) ? data.orderNames : [],
      });
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    fetchFilterOptions();
  }, []);

  const handleResetFilters = () => {
    setUserName("");
    setOrderName("");
    setStartDate("");
    setEndDate("");
    setNoteType("");
    setPage(1);
  };

  const handleExportExcel = async (filterOverrides = {}) => {
    try {
      setExporting(true);
      const hasOverride = (key) => Object.prototype.hasOwnProperty.call(filterOverrides, key);
      const exportUserName = hasOverride("userName")
        ? filterOverrides.userName
        : debouncedUserName;
      const exportOrderName = hasOverride("orderName")
        ? filterOverrides.orderName
        : debouncedOrderName;
      const exportStartDate = hasOverride("startDate")
        ? filterOverrides.startDate
        : startDate;
      const exportEndDate = hasOverride("endDate")
        ? filterOverrides.endDate
        : endDate;
      const exportNoteType = hasOverride("noteType")
        ? filterOverrides.noteType
        : noteType;
      const res = await getStorageHistoryExport({
        direction: historyDirection,
        ...(exportUserName && { userName: exportUserName }),
        ...(exportOrderName && { orderName: exportOrderName }),
        ...(exportStartDate && { startDate: exportStartDate }),
        ...(exportEndDate && { endDate: exportEndDate }),
        ...(exportNoteType && { noteType: exportNoteType }),
      });
      if (!res.ok) throw new Error("Không thể tải dữ liệu để xuất Excel");

      const data = await res.json();
      const exportRows = Array.isArray(data.history) ? data.history : [];
      if (exportRows.length === 0) {
        toast.error("Không có dữ liệu lịch sử để xuất Excel");
        return;
      }

      const workbook = new ExcelJS.Workbook();
      workbook.creator = "TTSmartEcom";
      workbook.created = new Date();

      const directionLabel = historyDirection === "export" ? "xuất" : "nhập";
      const worksheet = workbook.addWorksheet(`Lịch sử ${directionLabel} kho`);
      worksheet.views = [{ state: "frozen", ySplit: 1 }];
      worksheet.columns = [
        { header: "Người dùng", key: "userName", width: 24 },
        { header: "Sản phẩm", key: "productName", width: 36 },
        { header: "Đơn hàng", key: "orderName", width: 32 },
        { header: "Số lượng", key: "quantity", width: 14 },
        { header: "Ghi chú", key: "note", width: 42 },
        { header: "Thời gian", key: "createdAt", width: 22 },
      ];

      worksheet.addRows(exportRows.map((row) => ({
        userName: row.userName || "",
        productName: row.productName || "",
        orderName: row.orderName
          || (row.orderId ? `Đơn hàng (#${String(row.orderId).slice(-6)})` : ""),
        quantity: Number(row.quantity) || 0,
        note: getHistoryLabel(row),
        createdAt: moment(row.createdAt).format("DD/MM/YYYY HH:mm"),
      })));

      const headerRow = worksheet.getRow(1);
      headerRow.height = 24;
      headerRow.font = { bold: true, color: { argb: "FFFFFFFF" } };
      headerRow.alignment = { vertical: "middle", horizontal: "center" };
      headerRow.eachCell((cell) => {
        cell.fill = {
          type: "pattern",
          pattern: "solid",
          fgColor: { argb: "FF1F4E78" },
        };
      });

      worksheet.autoFilter = "A1:F1";
      worksheet.eachRow((row, rowNumber) => {
        row.eachCell((cell, columnNumber) => {
          cell.border = {
            top: { style: "thin", color: { argb: "FFD9E2F3" } },
            left: { style: "thin", color: { argb: "FFD9E2F3" } },
            bottom: { style: "thin", color: { argb: "FFD9E2F3" } },
            right: { style: "thin", color: { argb: "FFD9E2F3" } },
          };
          if (rowNumber > 1) {
            cell.alignment = {
              vertical: "middle",
              horizontal: columnNumber === 4 ? "center" : "left",
              wrapText: true,
            };
          }
        });
      });

      const buffer = await workbook.xlsx.writeBuffer();
      const fileName = `lich-su-${directionLabel}-kho_${moment().format("YYYY-MM-DD_HH-mm")}.xlsx`;
      saveAs(
        new Blob([buffer], {
          type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        }),
        fileName,
      );
      toast.success(`Đã xuất ${exportRows.length} dòng lịch sử ${directionLabel} kho`);
    } catch (error) {
      console.error("Lỗi khi xuất lịch sử Excel:", error);
      toast.error(error.message || "Không thể xuất file Excel");
    } finally {
      setExporting(false);
    }
  };

  useEffect(() => {
    const executeVoiceHistoryExport = () => {
      const serializedCommand = sessionStorage.getItem(VOICE_HISTORY_EXPORT_KEY);
      if (!serializedCommand) return;

      try {
        const command = JSON.parse(serializedCommand);
        if (command.direction !== historyDirection) return;

        sessionStorage.removeItem(VOICE_HISTORY_EXPORT_KEY);
        if (command.requestedAt && Date.now() - Number(command.requestedAt) > 60000) {
          return;
        }

        const dateRange = getVoiceHistoryDateRange(
          command.datePreset,
          moment(),
          { startDate: command.startDate, endDate: command.endDate },
        );
        if (command.datePreset === "custom" && (!dateRange.startDate || !dateRange.endDate)) {
          toast.error("Không nhận diện được khoảng ngày để xuất Excel");
          return;
        }
        setUserName("");
        setDebouncedUserName("");
        setOrderName("");
        setDebouncedOrderName("");
        setStartDate(dateRange.startDate);
        setEndDate(dateRange.endDate);
        setNoteType("");
        setPage(1);

        void handleExportExcel({
          userName: "",
          orderName: "",
          startDate: dateRange.startDate,
          endDate: dateRange.endDate,
          noteType: "",
        });
      } catch (error) {
        sessionStorage.removeItem(VOICE_HISTORY_EXPORT_KEY);
        console.error("Lỗi khi thực hiện lệnh Voice xuất Excel:", error);
        toast.error("Không thể thực hiện lệnh Voice xuất Excel lịch sử");
      }
    };

    executeVoiceHistoryExport();
    window.addEventListener("voiceHistoryExport", executeVoiceHistoryExport);
    return () => window.removeEventListener("voiceHistoryExport", executeVoiceHistoryExport);
    // Chỉ đăng ký lại khi đổi giữa trang lịch sử nhập và xuất.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [historyDirection]);

  return (
    <Box p={2} className="admin-list-page history-list-page">
      <div className="sticky-header">
        <Typography variant="h5" gutterBottom>
          {historyDirection === "export" ? "Lịch sử xuất kho" : "Lịch sử nhập kho"}
        </Typography>
      </div>

      {/* Bộ lọc */}
      <Box
        className="admin-list-controls"
        display="flex"
        flexWrap="wrap"
        alignItems="center"
        gap={2}
        mb={2}
      >
        <Autocomplete
          freeSolo
          size="small"
          options={filterOptions.userNames}
          value={userName}
          onInputChange={(event, newInputValue) => {
            setUserName(newInputValue);
            setPage(1);
          }}
          onChange={(event, newValue) => {
            setUserName(newValue || "");
            setPage(1);
          }}
          filterOptions={(options, state) => {
            const inputValue = removeVietnameseTones(state.inputValue);
            return options.filter((option) =>
              removeVietnameseTones(option).includes(inputValue)
            );
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              label="Người dùng"
              placeholder="Nhập tên..."
              variant="outlined"
              sx={{ width: 200 }}
            />
          )}
        />
        <Autocomplete
          freeSolo
          size="small"
          options={filterOptions.orderNames}
          value={orderName}
          onInputChange={(event, newInputValue) => {
            setOrderName(newInputValue);
            setPage(1);
          }}
          onChange={(event, newValue) => {
            setOrderName(newValue || "");
            setPage(1);
          }}
          filterOptions={(options, state) => {
            const inputValue = removeVietnameseTones(state.inputValue);
            return options.filter((option) =>
              removeVietnameseTones(option).includes(inputValue)
            );
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              label="Tên đơn hàng"
              placeholder="Nhập tên đơn hàng..."
              variant="outlined"
              sx={{ width: 200 }}
            />
          )}
        />
        <TextField
          label="Từ ngày"
          type="date"
          size="small"
          InputLabelProps={{ shrink: true }}
          value={startDate}
          onChange={(e) => {
            setStartDate(e.target.value);
            setPage(1);
          }}
        />
        <TextField
          label="Đến ngày"
          type="date"
          size="small"
          InputLabelProps={{ shrink: true }}
          value={endDate}
          onChange={(e) => {
            setEndDate(e.target.value);
            setPage(1);
          }}
        />
        <TextField
          select
          label="Ghi chú"
          value={noteType}
          onChange={(e) => {
            setNoteType(e.target.value);
            setPage(1);
          }}
          size="small"
          sx={{ width: "180px", minWidth: "150px" }}
          SelectProps={{ native: true }}
          InputLabelProps={{ shrink: true }}
        >
          {noteTypeOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </TextField>
        <Button variant="outlined" color="secondary" onClick={handleResetFilters}>
          Xóa bộ lọc
        </Button>
        <Button
          variant="contained"
          onClick={() => handleExportExcel()}
          disabled={loading || exporting || histories.length === 0}
        >
          {exporting ? "Đang xuất Excel..." : "Xuất Excel"}
        </Button>
        {!loading && histories.length > 0 && (
          <FormControl size="small" sx={{ minWidth: 104, ml: { sm: "auto" } }}>
            <Select
              value={limit}
              onChange={(e) => {
                setLimit(e.target.value);
                setPage(1);
              }}
            >
              <MenuItem value={20}>20 dòng</MenuItem>
              <MenuItem value={50}>50 dòng</MenuItem>
              <MenuItem value={100}>100 dòng</MenuItem>
            </Select>
          </FormControl>
        )}
      </Box>

      {loading ? (
        <Box display="flex" justifyContent="center" my={4}>
          <CircularProgress />
        </Box>
      ) : histories.length === 0 ? (
        <Typography>Không có dữ liệu lịch sử</Typography>
      ) : (
        <>
          <TableContainer component={Paper} className="admin-list-table" sx={{ overflow: "auto" }}>
            <Table size="small" stickyHeader>
              <TableHead>
                <TableRow>
                  <TableCell align="center">
                    <b>Người dùng</b>
                  </TableCell>
                  <TableCell align="center">
                    <b>Sản phẩm</b>
                  </TableCell>
                  <TableCell align="center">
                    <b>Đơn hàng</b>
                  </TableCell>
                  <TableCell align="center">
                    <b>Số lượng</b>
                  </TableCell>
                  <TableCell align="center">
                    <b>Ghi chú</b>
                  </TableCell>
                  <TableCell align="center">
                    <b>Thời gian</b>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {histories.map((row) => (
                  <TableRow key={row._id}>
                    <TableCell align="center">{row.userName || ""}</TableCell>
                    <TableCell align="center">
                      {row.productName || ""}
                    </TableCell>
                    <TableCell align="center">
                      {row.orderId ? (
                        row.note && row.note.includes("bán online") ? (
                          <span>{row.orderName || row.orderId}</span>
                        ) : (
                          <span
                            style={{
                              cursor: "pointer",
                              color: "#1976d2",
                              textDecoration: "underline",
                            }}
                            onClick={() => {
                              if (row.quantity > 0) {
                                navigate(`/importorder/${row.orderId}`);
                              } else if (row.quantity < 0) {
                                navigate(`/exportorder/${row.orderId}`);
                              }
                            }}
                          >
                            {row.orderName || `Đơn hàng (#${row.orderId.slice(-6)})`}
                          </span>
                        )
                      ) : (
                        ""
                      )}
                    </TableCell>
                    <TableCell align="center">{row.quantity}</TableCell>
                    <TableCell align="center">
                      {getHistoryLabel(row)}
                    </TableCell>
                    <TableCell align="center">
                      {moment(row.createdAt).format("DD/MM/YYYY HH:mm")}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Box display="flex" justifyContent="center" my={2}>
            <Pagination
              count={totalPages}
              page={page}
              onChange={(e, value) => setPage(value)}
              color="primary"
            />
          </Box>
        </>
      )}
    </Box>
  );
};

export const HistoryImport = () => <History direction="import" />;

export const HistoryExport = () => <History direction="export" />;

export default History;
