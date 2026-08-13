import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { beforeEach, describe, expect, it, vi } from "vitest";

const apiFetchMock = vi.hoisted(() => vi.fn());

vi.mock("./httpClient", () => ({
  apiFetch: apiFetchMock,
}));

import {
  createVoiceVocabularyEntry,
  deleteVoiceVocabularyEntry,
  getVoiceVocabulary,
  queryProductsByVoice,
  queryProductsByVoiceText,
  updateVoiceVocabularyEntry,
} from "./voiceApi";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));

describe("voiceApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiFetchMock.mockResolvedValue({ ok: true, status: 200 });
  });

  it("keeps audio and text voice-query contracts", async () => {
    const audioBlob = new Blob(["audio"], { type: "audio/webm" });

    await queryProductsByVoice(audioBlob);
    await queryProductsByVoiceText("tìm PLC Siemens");

    const audioOptions = apiFetchMock.mock.calls[0][1];
    expect(apiFetchMock.mock.calls[0][0]).toBe("/products/voice-query");
    expect(audioOptions.method).toBe("POST");
    expect(audioOptions.body).toBeInstanceOf(FormData);
    const uploadedAudio = audioOptions.body.get("audio");
    expect(uploadedAudio).toBeInstanceOf(File);
    expect(uploadedAudio.name).toBe("query.webm");
    expect(uploadedAudio.type).toBe("audio/webm");
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/products/voice-query-text", {
      method: "POST",
      json: { text: "tìm PLC Siemens" },
    });
  });

  it("maps vocabulary read and mutation contracts", async () => {
    await getVoiceVocabulary();
    await createVoiceVocabularyEntry("brands", { value: "Siemens" });
    await updateVoiceVocabularyEntry("brands", {
      oldValue: "Siemen",
      newValue: "Siemens",
    });
    await deleteVoiceVocabularyEntry("brands", { value: "Siemens" });

    expect(apiFetchMock).toHaveBeenNthCalledWith(1, "/voice-vocabs", {
      method: "GET",
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(2, "/voice-vocabs/brands", {
      method: "POST",
      json: { value: "Siemens" },
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(3, "/voice-vocabs/brands", {
      method: "PUT",
      json: { oldValue: "Siemen", newValue: "Siemens" },
    });
    expect(apiFetchMock).toHaveBeenNthCalledWith(4, "/voice-vocabs/brands", {
      method: "DELETE",
      json: { value: "Siemens" },
    });
  });

  it("keeps active Voice components free of direct HTTP transport", () => {
    for (const componentName of ["VoiceSearchFAB.jsx", "voicevocab.jsx"]) {
      const source = fs.readFileSync(
        path.join(currentDirectory, "..", "components", componentName),
        "utf8",
      );
      expect(source).toContain("voiceApi");
      expect(source).not.toContain("fetch(");
      expect(source).not.toContain("VITE_API_URL");
      expect(source).not.toContain("new FormData");
    }
  });
});
