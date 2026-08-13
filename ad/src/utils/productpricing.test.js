import { describe, expect, it } from "vitest";
import {
  calculateSalePrice,
  formatVariantPrice,
  isContactOnlyVariant,
} from "./productpricing";

describe("product pricing", () => {
  it.each([
    { price: "5480000", earn: 0, quantityForSale: 18 },
    { price: "", earn: 25, quantityForSale: 18 },
    { price: "5480000", earn: 25, quantityForSale: 0 },
  ])("shows contact when the variant is contact-only", (variant) => {
    expect(isContactOnlyVariant(variant)).toBe(true);
    expect(formatVariantPrice(variant)).toBe("Liên hệ");
  });

  it("formats a normal selling price", () => {
    const variant = { price: "5480000", earn: 25, quantityForSale: 18 };
    expect(isContactOnlyVariant(variant)).toBe(false);
    expect(formatVariantPrice(variant)).toBe("5.480.000 VND");
  });

  it.each([
    ["100000", 25, "125000"],
    ["100001", 25, "126000"],
    ["100000", 0, "100000"],
    ["", 25, ""],
  ])("calculates and rounds the sale price", (importPrice, earn, expectedPrice) => {
    expect(calculateSalePrice(importPrice, earn)).toBe(expectedPrice);
  });
});
