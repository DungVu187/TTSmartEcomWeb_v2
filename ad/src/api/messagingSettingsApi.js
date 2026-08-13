import { apiFetch } from "./httpClient";

const jsonHeaders = { "Content-Type": "application/json" };

const zaloHeaders = (authToken) => ({
  "auth-token": authToken,
  "Content-Type": "application/json",
});

export const getTelegramSettings = () =>
  apiFetch("/telegram/settings", { headers: jsonHeaders });

export const updateTelegramSettings = (enabled) =>
  apiFetch("/telegram/settings", {
    method: "PUT",
    headers: jsonHeaders,
    json: { enabled },
  });

export const createTelegramRecipient = (recipient) =>
  apiFetch("/telegram/recipients", {
    method: "POST",
    headers: jsonHeaders,
    json: recipient,
  });

export const updateTelegramRecipient = (recipientId, recipient) =>
  apiFetch(`/telegram/recipients/${recipientId}`, {
    method: "PUT",
    headers: jsonHeaders,
    json: recipient,
  });

export const deleteTelegramRecipient = (recipientId) =>
  apiFetch(`/telegram/recipients/${recipientId}`, {
    method: "DELETE",
    headers: jsonHeaders,
  });

export const sendTelegramTestMessage = (chatId) =>
  apiFetch("/telegram/test", {
    method: "POST",
    headers: jsonHeaders,
    json: { chatId },
  });

export const getZaloSettings = (authToken) =>
  apiFetch("/zalo/settings", { headers: zaloHeaders(authToken) });

export const saveZaloSettings = (authToken, settings) =>
  apiFetch("/zalo/settings", {
    method: "POST",
    headers: zaloHeaders(authToken),
    json: settings,
  });

export const getZaloAuthUrl = (authToken) =>
  apiFetch("/zalo/auth-url", { headers: zaloHeaders(authToken) });
