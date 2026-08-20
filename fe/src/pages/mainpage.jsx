import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useLanguage } from "../context/language.js";
import {
  getStorefrontSectionDocument,
  resolveStorefrontAssetUrl,
} from "../api/storefrontCatalogApi";
import "../components/style/stationdisplay.css";

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

const MainPage = () => {
  const { t } = useLanguage();
  const [sections, setSections] = useState([]);
  const navigate = useNavigate();

  useEffect(() => {
    getStorefrontSectionDocument()
      .then((res) => res.json())
      .then((data) => {
        const fullSections = data.Section || [];
        const filtered = fullSections.filter((sec) => sec.imgUrl);
        const sorted = filtered.sort((a, b) =>
          a.name.localeCompare(b.name, undefined, { numeric: true })
        );
        setSections(sorted);
      })
      .catch((error) => {
        console.error("Lỗi khi fetch section-doc:", error);
      });
  }, []);

  const handleClick = (sectionName) => {
    navigate(`/section/${sectionName}`);
  };

  return (
    <div className="station-detail-container">
      <div className="station-detail-bg-dots-left" />
      <div className="station-detail-bg-dots-right" />

      <section className="station-detail-content-shell">
        <div className="station-detail-grid">
          {sections.map((section, index) => {
            const hasPhoto = !!section.imgUrl;
            const iconClass = getSectionIcon(section.name);

            return (
              <div
                key={index}
                className={`station-detail-card ${hasPhoto ? "has-photo" : "no-photo"}`}
                onClick={() => handleClick(section.name)}
              >
                <div className="station-detail-card-left">
                  <div className="station-detail-card-icon-badge">
                    <i className={iconClass} />
                  </div>

                  <h2 className="station-detail-card-title">{section.name}</h2>
                </div>

                {hasPhoto && (
                  <div className="station-detail-card-right-img">
                    <img src={resolveStorefrontAssetUrl(section.imgUrl)} alt={section.name} />
                    <div className="station-detail-card-img-gradient" />
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

export default MainPage;
