import { createContext, useContext } from "react";
import { translations } from "../i18n/translations.js";

export const LanguageContext = createContext(null);

export const getStoredTranslation = (key, fallback = null) => {
  const savedLanguage = localStorage.getItem("language");
  const language = savedLanguage && translations[savedLanguage] ? savedLanguage : "vi";
  return translations[language]?.[key] ?? (fallback !== null ? fallback : key);
};

export const getStoredLocale = () => {
  const savedLanguage = localStorage.getItem("language");
  if (savedLanguage === "zh") return "zh-CN";
  if (savedLanguage === "en") return "en-US";
  return "vi-VN";
};

export const useLanguage = () => {
  const context = useContext(LanguageContext);
  if (!context) {
    throw new Error("useLanguage must be used within a LanguageProvider");
  }
  return context;
};
