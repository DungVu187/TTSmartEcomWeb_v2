import { useState, useEffect } from "react";
import { translations } from "../i18n/translations.js";
import { LanguageContext } from "./language.js";

export const LanguageProvider = ({ children }) => {
  const [language, setLanguageState] = useState(() => {
    const saved = localStorage.getItem("language");
    return saved && translations[saved] ? saved : "vi";
  });

  const setLanguage = (lang) => {
    if (translations[lang]) {
      setLanguageState(lang);
      localStorage.setItem("language", lang);
    }
  };

  useEffect(() => {
    document.documentElement.lang = language === "zh" ? "zh-CN" : language;
  }, [language]);

  const t = (key, fallback = null) => {
    if (!key) return "";

    // Check local translations first
    const langDict = translations[language];
    if (langDict && langDict[key] !== undefined) {
      return langDict[key];
    }

    return fallback !== null ? fallback : key;
  };

  const locale = language === "zh" ? "zh-CN" : language === "en" ? "en-US" : "vi-VN";

  return (
    <LanguageContext.Provider value={{ language, setLanguage, locale, t }}>
      {children}
    </LanguageContext.Provider>
  );
};
