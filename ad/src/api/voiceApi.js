import { apiFetch } from "./httpClient";

export const queryProductsByVoice = (audioBlob) => {
  const formData = new FormData();
  formData.append("audio", audioBlob, "query.webm");
  return apiFetch("/products/voice-query", {
    method: "POST",
    body: formData,
  });
};

export const queryProductsByVoiceText = (text) =>
  apiFetch("/products/voice-query-text", {
    method: "POST",
    json: { text },
  });

export const getVoiceVocabulary = () =>
  apiFetch("/voice-vocabs", { method: "GET" });

export const createVoiceVocabularyEntry = (group, entry) =>
  apiFetch(`/voice-vocabs/${group}`, {
    method: "POST",
    json: entry,
  });

export const updateVoiceVocabularyEntry = (group, entry) =>
  apiFetch(`/voice-vocabs/${group}`, {
    method: "PUT",
    json: entry,
  });

export const deleteVoiceVocabularyEntry = (group, entry) =>
  apiFetch(`/voice-vocabs/${group}`, {
    method: "DELETE",
    json: entry,
  });
