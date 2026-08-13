import { useState, useRef, useEffect } from "react";
import { Fab, Tooltip, CircularProgress, Box, IconButton, TextField } from "@mui/material";
import MicIcon from "@mui/icons-material/Mic";
import GraphicEqIcon from "@mui/icons-material/GraphicEq";
import KeyboardIcon from "@mui/icons-material/Keyboard";
import CloseIcon from "@mui/icons-material/Close";
import SearchIcon from "@mui/icons-material/Search";
import { useNavigate } from "react-router-dom";
import toast from "react-hot-toast";
import { useLanguage } from "../context/language.js";
import { getCustomerProfile } from "../api/customerAccountApi";
import {
  queryStorefrontVoiceAudio,
  queryStorefrontVoiceText,
} from "../api/storefrontCatalogApi";

const VoiceSearchFAB = () => {
  const { t } = useLanguage();
  const [isRecording, setIsRecording] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [textMode, setTextMode] = useState(false);
  const [textValue, setTextValue] = useState("");
  const mediaRecorderRef = useRef(null);
  const audioChunksRef = useRef([]);
  const recordingTimeoutRef = useRef(null);
  const lastTriggerRef = useRef(0);
  const isStartingRef = useRef(false);
  const navigate = useNavigate();

  useEffect(() => {
    const checkLoginStatus = async () => {
      try {
        const response = await getCustomerProfile();
        if (response.ok) {
          setIsLoggedIn(true);
        } else {
          setIsLoggedIn(false);
        }
      } catch (err) {
        console.error("Lỗi kiểm tra trạng thái đăng nhập:", err);
        setIsLoggedIn(false);
      }
    };

    checkLoginStatus();
  }, []);

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
      toast.error(t("microphone_secure_required"), {
        duration: 5000
      });
      return;
    }

    try {
      isStartingRef.current = true;
      setIsRecording(true);
      audioChunksRef.current = [];

      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      let options = { mimeType: "audio/webm" };
      if (!MediaRecorder.isTypeSupported(options.mimeType)) {
        options = { mimeType: "audio/ogg" };
      }
      if (!MediaRecorder.isTypeSupported(options.mimeType)) {
        options = { mimeType: "" };
      }

      const mediaRecorder = new MediaRecorder(stream, options);
      mediaRecorderRef.current = mediaRecorder;

      mediaRecorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop());

        const audioBlob = new Blob(audioChunksRef.current, {
          type: mediaRecorder.mimeType || "audio/webm",
        });

        if (audioBlob.size < 1000) {
          toast.error(t("speak_longer"));
          setIsProcessing(false);
          return;
        }

        await sendAudioToAPI(audioBlob);
      };

      mediaRecorder.start();
      toast.success(t("listening_instructions"), {
        id: "voice-status-fe",
        duration: 3000,
      });

      recordingTimeoutRef.current = setTimeout(() => {
        stopRecording();
      }, 15000);

    } catch (err) {
      console.error("Lỗi truy cập micro:", err);
      toast.error(t("microphone_permission_error"));
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
    toast.loading(t("processing_voice"), { id: "voice-status-fe" });
    try {
      const response = await queryStorefrontVoiceAudio(audioBlob);

      const data = await response.json();

      if (response.ok && data.success) {
        const keyword = data.keyword || "";
        const filters = data.filters || {};

        toast.success(`${t("search_result_prefix")}: "${keyword || data.transcript}"`, {
          id: "voice-status-fe",
          duration: 3000,
        });

        // Construct search query string
        const params = new URLSearchParams();
        let searchVal = filters.code ? filters.code : (keyword || "");

        if (searchVal) params.set("search", searchVal);
        if (filters.brand) params.set("brand", filters.brand);
        if (filters.type) params.set("type", filters.type);

        navigate(`/product?${params.toString()}`);
      } else {
        throw new Error("audio_parse_failed");
      }
    } catch (err) {
      console.error("Lỗi voice-query API:", err);
      toast.error(err.message === "audio_parse_failed" ? t("audio_parse_failed") : t("voice_process_error"), {
        id: "voice-status-fe",
      });
    } finally {
      setIsProcessing(false);
    }
  };

  // Gửi câu tìm kiếm dạng chữ (dùng để test khi máy không có micro).
  // Đi qua cùng endpoint chuẩn hóa nên kết quả điều hướng giống hệt giọng nói.
  const submitTextQuery = async (e) => {
    if (e) e.preventDefault();
    const query = textValue.trim();
    if (!query) {
      toast.error(t("search_text_required"));
      return;
    }

    setIsProcessing(true);
    toast.loading(t("processing_search"), { id: "voice-status-fe" });
    try {
      const response = await queryStorefrontVoiceText(query);

      const data = await response.json();

      if (response.ok && data.success) {
        const keyword = data.keyword || "";
        const filters = data.filters || {};

        toast.success(`${t("search_result_prefix")}: "${keyword || data.transcript}"`, {
          id: "voice-status-fe",
          duration: 3000,
        });

        const params = new URLSearchParams();
        let searchVal = filters.code ? filters.code : (keyword || "");

        if (searchVal) params.set("search", searchVal);
        if (filters.brand) params.set("brand", filters.brand);
        if (filters.type) params.set("type", filters.type);

        setTextMode(false);
        setTextValue("");
        navigate(`/product?${params.toString()}`);
      } else {
        throw new Error("text_parse_failed");
      }
    } catch (err) {
      console.error("Lỗi voice-query-text API:", err);
      toast.error(err.message === "text_parse_failed" ? t("text_parse_failed") : t("search_process_error"), {
        id: "voice-status-fe",
      });
    } finally {
      setIsProcessing(false);
    }
  };

  if (!isLoggedIn) return null;

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
        "@media (max-width: 760px)": {
          display: "none",
        },
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
            placeholder={t("voice_search_placeholder")}
            value={textValue}
            onChange={(e) => setTextValue(e.target.value)}
            autoFocus
            disabled={isProcessing}
            InputProps={{ disableUnderline: true }}
            sx={{ width: { xs: 200, sm: 300 } }}
          />
          <IconButton type="submit" color="primary" disabled={isProcessing} aria-label={t("search")}>
            {isProcessing ? <CircularProgress size={22} color="inherit" /> : <SearchIcon />}
          </IconButton>
        </Box>
      )}

      <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
        {/* Nút bật/tắt chế độ nhập chữ */}
        <Tooltip
          title={textMode ? t("close_text_input") : t("keyboard_search")}
          placement="top"
          arrow
        >
          <Fab
            size="small"
            color={textMode ? "error" : "default"}
            onClick={() => setTextMode((prev) => !prev)}
            aria-label={t("switch_to_text_input")}
          >
            {textMode ? <CloseIcon /> : <KeyboardIcon />}
          </Fab>
        </Tooltip>

        <Tooltip
          title={
            isRecording
              ? t("stop_and_search")
              : isProcessing
              ? t("processing")
              : t("start_voice_search")
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
