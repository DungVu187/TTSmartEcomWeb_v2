import { describe, expect, it } from "vitest";
import { getVoiceHistoryDateRange } from "./history";

describe("history Voice date ranges", () => {
  const referenceDate = "2026-08-06T10:00:00+07:00";

  it("maps relative Vietnamese date presets to history filters", () => {
    expect(getVoiceHistoryDateRange("today", referenceDate)).toEqual({
      startDate: "2026-08-06",
      endDate: "2026-08-06",
    });
    expect(getVoiceHistoryDateRange("yesterday", referenceDate)).toEqual({
      startDate: "2026-08-05",
      endDate: "2026-08-05",
    });
    expect(getVoiceHistoryDateRange("this_week", referenceDate)).toEqual({
      startDate: "2026-08-03",
      endDate: "2026-08-09",
    });
    expect(getVoiceHistoryDateRange("this_month", referenceDate)).toEqual({
      startDate: "2026-08-01",
      endDate: "2026-08-31",
    });
    expect(getVoiceHistoryDateRange("custom", referenceDate, {
      startDate: "2026-07-15",
      endDate: "2026-07-31",
    })).toEqual({
      startDate: "2026-07-15",
      endDate: "2026-07-31",
    });
  });
});
