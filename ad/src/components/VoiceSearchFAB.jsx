import { useState, useRef } from "react";
import { Fab, Tooltip, CircularProgress, Box, IconButton, TextField } from "@mui/material";
import MicIcon from "@mui/icons-material/Mic";
import GraphicEqIcon from "@mui/icons-material/GraphicEq";
import KeyboardIcon from "@mui/icons-material/Keyboard";
import CloseIcon from "@mui/icons-material/Close";
import SearchIcon from "@mui/icons-material/Search";
import { useNavigate, useLocation } from "react-router-dom";
import toast from "react-hot-toast";
import {
  queryProductsByVoice,
  queryProductsByVoiceText,
} from "../api/voiceApi";
import { usePermissions } from "../context/permissioncontext";

const VOICE_HISTORY_EXPORT_KEY = "voiceHistoryExport";

const VoiceSearchFAB = () => {
  const [isRecording, setIsRecording] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [textMode, setTextMode] = useState(false);
  const [textValue, setTextValue] = useState("");
  const mediaRecorderRef = useRef(null);
  const audioChunksRef = useRef([]);
  const recordingTimeoutRef = useRef(null);
  const lastTriggerRef = useRef(0);
  const isStartingRef = useRef(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { can } = usePermissions();

  const handleHistoryExportCommand = (data) => {
    const historyExport = data.historyExport || {};
    const direction = historyExport.direction;
    if (!['import', 'export'].includes(direction)) {
      throw new Error("Vui lòng nói rõ lịch sử nhập hoặc lịch sử xuất cần xuất Excel.");
    }

    const requiredPermission = direction === 'export'
      ? 'history_export.view'
      : 'history_import.view';
    if (!can(requiredPermission)) {
      throw new Error(`Bạn không có quyền xem lịch sử ${direction === 'export' ? 'xuất' : 'nhập'} kho.`);
    }

    const datePreset = historyExport.datePreset || 'all';
    if (
      datePreset === 'custom'
      && (!historyExport.startDate || !historyExport.endDate)
    ) {
      throw new Error("Không nhận diện được khoảng ngày. Vui lòng nói rõ ngày bắt đầu và ngày kết thúc.");
    }

    const command = {
      direction,
      datePreset,
      ...(datePreset === 'custom' && {
        startDate: historyExport.startDate,
        endDate: historyExport.endDate,
      }),
      requestedAt: Date.now(),
    };
    sessionStorage.setItem(VOICE_HISTORY_EXPORT_KEY, JSON.stringify(command));

    const directionLabel = direction === 'export' ? 'xuất' : 'nhập';
    toast.success(`Đang xuất Excel lịch sử ${directionLabel} kho...`, {
      id: "voice-status",
      duration: 3000,
    });

    setTextMode(false);
    setTextValue("");

    const targetPath = `/history/${direction}`;
    if (location.pathname === targetPath) {
      window.dispatchEvent(new Event("voiceHistoryExport"));
    } else {
      navigate(targetPath);
    }
  };

  const toggleRecording = () => {
    const now = Date.now();
    if (now - lastTriggerRef.current < 300) {
      return;
    }
    lastTriggerRef.current = now;

    if (isProcessing || isStartingRef.current) return;

    if (isRecording) {
      stopRecording();
    } else {
      startRecording();
    }
  };

  const startRecording = async () => {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      toast.error("Trình duyệt yêu cầu kết nối bảo mật HTTPS hoặc Localhost để sử dụng Micro!", {
        duration: 5000
      });
      return;
    }

    try {
      isStartingRef.current = true;
      setIsRecording(true);
      audioChunksRef.current = [];

      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // Choose a mimeType supported by browser
      let options = { mimeType: "audio/webm" };
      if (!MediaRecorder.isTypeSupported(options.mimeType)) {
        options = { mimeType: "audio/ogg" };
      }
      if (!MediaRecorder.isTypeSupported(options.mimeType)) {
        options = { mimeType: "" }; // default fallback
      }

      const mediaRecorder = new MediaRecorder(stream, options);
      mediaRecorderRef.current = mediaRecorder;

      mediaRecorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = async () => {
        // Stop all track nodes
        stream.getTracks().forEach((track) => track.stop());

        const audioBlob = new Blob(audioChunksRef.current, {
          type: mediaRecorder.mimeType || "audio/webm",
        });

        if (audioBlob.size < 1000) {
          toast.error("Vui lòng nói lâu hơn một chút trước khi bấm dừng!");
          setIsProcessing(false);
          return;
        }

        await sendAudioToAPI(audioBlob);
      };

      mediaRecorder.start();
      toast.success("Đang lắng nghe... Bấm lại nút micro khi nói xong.", {
        id: "voice-status",
        duration: 3000,
      });

      // Auto-stop after 15 seconds to prevent runaway recording
      recordingTimeoutRef.current = setTimeout(() => {
        stopRecording();
      }, 15000);

    } catch (err) {
      console.error("Lỗi truy cập micro:", err);
      toast.error("Không thể mở micro. Vui lòng cấp quyền micro cho trang web.");
      setIsRecording(false);
    } finally {
      isStartingRef.current = false;
    }
  };

  const stopRecording = () => {
    if (recordingTimeoutRef.current) {
      clearTimeout(recordingTimeoutRef.current);
    }

    if (mediaRecorderRef.current && mediaRecorderRef.current.state === "recording") {
      setIsProcessing(true);
      mediaRecorderRef.current.stop();
    }
    setIsRecording(false);
  };

  const sendAudioToAPI = async (audioBlob) => {
    toast.loading("Đang xử lý giọng nói...", { id: "voice-status" });
    try {
      const response = await queryProductsByVoice(audioBlob);

      const data = await response.json();

      if (response.ok && data.success) {
        if (data.intent === 'export_history') {
          handleHistoryExportCommand(data);
          return;
        }

        const keyword = data.keyword || "";
        const filters = data.filters || {};

        toast.success(`Tìm kiếm: "${keyword || data.transcript}"`, {
          id: "voice-status",
          duration: 3000,
        });

        // Save new filters to session storage
        const savedFiltersStr = sessionStorage.getItem("productFilters");
        const currentFilters = savedFiltersStr ? JSON.parse(savedFiltersStr) : {
          search: "",
          code: "",
          brand: "Tất cả",
          type: "Tất cả",
          section: "Tất cả",
          value: "Tất cả",
          sortBy: "createdAt",
          sortOrder: "desc",
        };

        const updatedFilters = {
          ...currentFilters,
          search: keyword,
          brand: filters.brand || "Tất cả",
          type: filters.type || "Tất cả",
          code: filters.code || "",
          section: "Tất cả",
          value: "Tất cả",
        };

        sessionStorage.setItem("productFilters", JSON.stringify(updatedFilters));

        // Dispatch window event so products.jsx updates its state instantly
        window.dispatchEvent(new Event("voiceSearchQuery"));

        // If not on product list page, redirect there
        if (location.pathname !== "/product") {
          navigate("/product");
        }
      } else {
        throw new Error(data.message || "Không phân tích được âm thanh.");
      }
    } catch (err) {
      console.error("Lỗi voice-query API:", err);
      toast.error(err.message || "Gặp lỗi khi xử lý giọng nói.", {
        id: "voice-status",
      });
    } finally {
      setIsProcessing(false);
    }
  };

  // Gửi câu tìm kiếm dạng chữ (dùng để test khi máy không có micro).
  // Đi qua cùng endpoint chuẩn hóa nên kết quả giống hệt giọng nói; áp filter
  // theo đúng cơ chế sessionStorage + event "voiceSearchQuery" mà products.jsx đang nghe.
  const submitTextQuery = async (e) => {
    if (e) e.preventDefault();
    const query = textValue.trim();
    if (!query) {
      toast.error("Vui lòng nhập câu tìm kiếm.");
      return;
    }

    setIsProcessing(true);
    toast.loading("Đang xử lý câu tìm kiếm...", { id: "voice-status" });
    try {
      const response = await queryProductsByVoiceText(query);

      const data = await response.json();

      if (response.ok && data.success) {
        if (data.intent === 'export_history') {
          handleHistoryExportCommand(data);
          return;
        }

        const keyword = data.keyword || "";
        const filters = data.filters || {};

        toast.success(`Tìm kiếm: "${keyword || data.transcript}"`, {
          id: "voice-status",
          duration: 3000,
        });

        const savedFiltersStr = sessionStorage.getItem("productFilters");
        const currentFilters = savedFiltersStr ? JSON.parse(savedFiltersStr) : {
          search: "",
          code: "",
          brand: "Tất cả",
          type: "Tất cả",
          section: "Tất cả",
          value: "Tất cả",
          sortBy: "createdAt",
          sortOrder: "desc",
        };

        const updatedFilters = {
          ...currentFilters,
          search: keyword,
          brand: filters.brand || "Tất cả",
          type: filters.type || "Tất cả",
          code: filters.code || "",
          section: "Tất cả",
          value: "Tất cả",
        };

        sessionStorage.setItem("productFilters", JSON.stringify(updatedFilters));
        window.dispatchEvent(new Event("voiceSearchQuery"));

        setTextMode(false);
        setTextValue("");

        if (location.pathname !== "/product") {
          navigate("/product");
        }
      } else {
        throw new Error(data.message || "Không phân tích được câu tìm kiếm.");
      }
    } catch (err) {
      console.error("Lỗi voice-query-text API:", err);
      toast.error(err.message || "Gặp lỗi khi xử lý câu tìm kiếm.", {
        id: "voice-status",
      });
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <Box
      sx={{
        position: "fixed",
        bottom: 24,
        right: 24,
        zIndex: 9999,
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-end",
        gap: 1.5,
      }}
    >
      {/* Ô nhập chữ để test đầu vào khi máy không có micro; đi qua cùng luồng chuẩn hóa */}
      {textMode && (
        <Box
          component="form"
          onSubmit={submitTextQuery}
          sx={{
            display: "flex",
            alignItems: "center",
            gap: 0.5,
            backgroundColor: "white",
            borderRadius: "999px",
            boxShadow: "0 4px 12px rgba(0,0,0,0.15)",
            pl: 2,
            pr: 0.5,
            py: 0.5,
          }}
        >
          <TextField
            variant="standard"
            placeholder="Nhập câu tìm kiếm, vd: tìm van điện khí TTSM1"
            value={textValue}
            onChange={(e) => setTextValue(e.target.value)}
            autoFocus
            disabled={isProcessing}
            InputProps={{ disableUnderline: true }}
            sx={{ width: { xs: 200, sm: 300 } }}
          />
          <IconButton type="submit" color="primary" disabled={isProcessing} aria-label="Tìm kiếm">
            {isProcessing ? <CircularProgress size={22} color="inherit" /> : <SearchIcon />}
          </IconButton>
        </Box>
      )}

      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {/* Nút bật/tắt chế độ nhập chữ */}
        <Tooltip
          title={textMode ? "Đóng ô nhập chữ" : "Nhập câu tìm kiếm bằng bàn phím"}
          placement="top"
          arrow
        >
          <Fab
            size="small"
            color={textMode ? "error" : "default"}
            onClick={() => setTextMode((prev) => !prev)}
            aria-label="Chuyển chế độ nhập chữ"
          >
            {textMode ? <CloseIcon /> : <KeyboardIcon />}
          </Fab>
        </Tooltip>

        <Tooltip
          title={
            isRecording
              ? "Bấm lại để dừng và tìm kiếm"
              : isProcessing
              ? "Đang xử lý..."
              : "Bấm để bắt đầu tìm kiếm bằng giọng nói"
          }
          placement="top"
          arrow
        >
          <Fab
            color={isRecording ? "error" : "primary"}
            onClick={toggleRecording}
            onContextMenu={(e) => e.preventDefault()}
            sx={{
              width: 56,
              height: 56,
              boxShadow: isRecording
                ? "0 0 20px #d32f2f, 0 0 40px #d32f2f"
                : "0 4px 10px rgba(0,0,0,0.3)",
              transition: "all 0.3s ease",
              transform: isRecording ? "scale(1.15)" : "scale(1)",
              "&::after": isRecording
                ? {
                    content: '""',
                    position: "absolute",
                    width: "100%",
                    height: "100%",
                    borderRadius: "50%",
                    border: "2px solid #d32f2f",
                    animation: "pulse 1.2s infinite ease-in-out",
                  }
                : {},
              "@keyframes pulse": {
                "0%": { transform: "scale(1)", opacity: 1 },
                "100%": { transform: "scale(1.8)", opacity: 0 },
              },
            }}
          >
            {isProcessing ? (
              <CircularProgress size={24} color="inherit" />
            ) : isRecording ? (
              <GraphicEqIcon />
            ) : (
              <MicIcon />
            )}
          </Fab>
        </Tooltip>
      </Box>
    </Box>
  );
};

export default VoiceSearchFAB;
