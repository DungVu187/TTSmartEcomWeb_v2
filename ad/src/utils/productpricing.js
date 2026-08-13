const parseProductNumber = (value) => {
  if (typeof value === "number") return Number.isFinite(value) ? value : 0;
  const normalized = String(value ?? "")
    .trim()
    .replace(/\./g, "")
    .replace(",", ".");
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
};

const parseOptionalProductNumber = (value) => {
  if (value === null || value === undefined || value === "") return null;
  const normalized = String(value).replace(/\./g, "").replace(",", ".");
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : null;
};

export const calculateSalePrice = (importPrice, earn, fallbackPrice = "") => {
  const importPriceNumber = parseOptionalProductNumber(importPrice);
  if (importPriceNumber === null) return fallbackPrice || "";

  const earnNumber = Number(earn) || 0;
  const rawPrice = importPriceNumber * (1 + earnNumber / 100);
  return String(Math.ceil(rawPrice / 1000) * 1000);
};

export const isContactOnlyVariant = (variant) => {
  if (!variant) return true;

  return (
    Number(variant.earn) === 0 ||
    parseProductNumber(variant.price) <= 0 ||
    Number(variant.quantityForSale || 0) <= 0
  );
};

export const formatVariantPrice = (variant, suffix = "VND") => {
  if (isContactOnlyVariant(variant)) return "Liên hệ";
  const formattedPrice = parseProductNumber(variant.price).toLocaleString("vi-VN");
  return suffix ? `${formattedPrice} ${suffix}` : formattedPrice;
};
