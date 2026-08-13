import { useEffect, useState } from "react";
import { CircularProgress, Alert } from "@mui/material";
import { useParams, useNavigate } from "react-router-dom";
import { useLanguage } from "../context/language.js";
import {
  getPublicStorefrontStation,
  getStorefrontProductsByIds,
  getStorefrontSectionImages,
  resolveStorefrontAssetUrl,
} from "../api/storefrontCatalogApi";
import "./style/stationdisplay.css";

const getSectionIcon = (sectionName) => {
  const name = String(sectionName || "").toLowerCase().trim();
  if (name.includes("trung tâm")) return "fa-solid fa-house";
  if (name.includes("băng")) return "fa-solid fa-layer-group";
  if (name.includes("cối")) return "fa-solid fa-boxes-stacked";
  if (name.includes("cốt liệu")) return "fa-solid fa-warehouse";
  if (name.includes("silo")) return "fa-solid fa-building-columns";
  if (name.includes("tủ điện") || name.includes("tủ điều khiển") || name.includes("cabinet")) return "fa-solid fa-microchip";
  if (name.includes("trạm trộn") || name.includes("mixer")) return "fa-solid fa-building";
  if (name.includes("bơm") || name.includes("pump")) return "fa-solid fa-faucet-drip";
  if (name.includes("cân") || name.includes("scale") || name.includes("loadcell")) return "fa-solid fa-scale-balanced";
  if (name.includes("lọc") || name.includes("filter")) return "fa-solid fa-filter";
  if (name.includes("khí") || name.includes("nén") || name.includes("air")) return "fa-solid fa-wind";
  if (name.includes("động cơ") || name.includes("motor")) return "fa-solid fa-bolt";
  if (name.includes("van") || name.includes("valve")) return "fa-solid fa-circle-notch";
  return "fa-solid fa-industry";
};

const StationDisplay = () => {
  const { t } = useLanguage();
  const { code } = useParams();
  const navigate = useNavigate();

  const [sections, setSections] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (code) {
      sessionStorage.setItem("activeStationCode", code);
    }
  }, [code]);

  useEffect(() => {
    const fetchStationSections = async () => {
      try {
        const res = await getPublicStorefrontStation(code);
        if (!res.ok) throw new Error("failed_to_get_station_info");
        const data = await res.json();
        const productIds = data.productId || [];

        if (productIds.length === 0) {
          setSections([]);
          setLoading(false);
          return;
        }

        const resProduct = await getStorefrontProductsByIds(productIds);
        const dataProduct = await resProduct.json();
        const products = dataProduct.products || [];

        const sectionSet = new Set();
        products.forEach(p => {
          if (p.display !== false && p.section) {
            sectionSet.add(p.section);
          }
        });

        const uniqueSections = Array.from(sectionSet);

        // Fetch section image URLs
        const resImages = await getStorefrontSectionImages(uniqueSections);

        const imageData = await resImages.json();

        const resultSections = uniqueSections.map((name) => ({
          name,
          imgUrl: imageData[name] || ""
        }));

        setSections(resultSections);
      } catch (err) {
        setError(err.message === "failed_to_get_station_info"
          ? t("failed_to_get_station_info")
          : t("unknown_error"));
      } finally {
        setLoading(false);
      }
    };

    fetchStationSections();
  }, [code, t]);

  const handleClick = (sectionName) => {
    navigate(`/station/${code}/${sectionName}`);
  };

  if (loading) {
    return (
      <div className="station-detail-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "400px" }}>
        <CircularProgress />
      </div>
    );
  }

  if (error) {
    return (
      <div className="station-detail-container" style={{ padding: "40px" }}>
        <Alert severity="error">{error}</Alert>
      </div>
    );
  }

  return (
    <div className="station-detail-container">
      {/* Background Dot Decorative Overlays */}
      <div className="station-detail-bg-dots-left" />
      <div className="station-detail-bg-dots-right" />

      {/* Main Bento grid content */}
      <section className="station-detail-content-shell">
        <div className="station-detail-grid">
          {sections.map((section, index) => {
            const hasPhoto = !!section.imgUrl;
            const iconClass = getSectionIcon(section.name);
            const isTramTron = String(section.name || "").toLowerCase().includes("trạm trộn");

            return (
              <div
                key={index}
                className={`station-detail-card ${hasPhoto ? "has-photo" : "no-photo"} ${isTramTron ? "is-tram-tron" : ""}`}
                onClick={() => handleClick(section.name)}
              >
                <div className="station-detail-card-left">
                  <div className="station-detail-card-icon-badge">
                    <i className={iconClass} />
                  </div>

                  <h2 className="station-detail-card-title">{section.name}</h2>

                  {isTramTron && (
                    <div className="station-detail-table-preview">
                      <div className="station-detail-table-headers">
                        <span>{t("item_label")}</span>
                        <span>{t("button_count")}</span>
                      </div>
                    </div>
                  )}
                </div>

                {hasPhoto && (
                  <div className="station-detail-card-right-img">
                    <img src={resolveStorefrontAssetUrl(section.imgUrl)} alt={section.name} />
                    <div className="station-detail-card-img-gradient" />
                  </div>
                )}

                {isTramTron && (
                  <div className="station-detail-rows-select">
                    <span>{t("rows_per_page_10")}</span>
                    <i className="fa-solid fa-angle-down" />
                  </div>
                )}

                <button className="station-detail-card-btn" type="button" aria-label={t("view_details")}>
                  <i className="fa-solid fa-arrow-right" />
                </button>
              </div>
            );
          })}
        </div>
      </section>
    </div>
  );
};

export default StationDisplay;
