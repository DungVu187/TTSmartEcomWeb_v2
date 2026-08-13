import { useEffect, useState, useMemo } from "react";
import { useLanguage } from "../context/language.js";
import { Link, useNavigate } from "react-router-dom";
import "./styles/station.css";
import concreteBannerBg from "../assets/concrete_station_banner_bg.png";
import { getCustomerProfile, getCustomerStations } from "../api/customerAccountApi";
import {
  getStorefrontStationsByIds,
  resolveStorefrontAssetUrl,
} from "../api/storefrontCatalogApi";

const Station = () => {
  const { t } = useLanguage();
  const [stationIds, setStationIds] = useState([]);
  const [stationMap, setStationMap] = useState({});
  const [loading, setLoading] = useState(true);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [error, setError] = useState("");

  // UI States
  const [searchTerm, setSearchTerm] = useState("");
  const [viewMode, setViewMode] = useState("list"); // 'list' or 'grid'

  const navigate = useNavigate();

  useEffect(() => {
    const checkAuthAndFetch = async () => {
      try {
        const authRes = await getCustomerProfile();
        if (!authRes.ok) {
          setIsLoggedIn(false);
          setLoading(false);
          return;
        }

        setIsLoggedIn(true);

        const res = await getCustomerStations();
        if (!res.ok) throw new Error("failed_to_get_user_stations");
        const data = await res.json();
        const ids = data.stations || [];
        setStationIds(ids);

        if (ids.length === 0) {
          setLoading(false);
          return;
        }

        const stationRes = await getStorefrontStationsByIds(ids);

        if (!stationRes.ok) throw new Error("failed_to_get_station_info");
        const stations = await stationRes.json();

        const map = {};
        stations.forEach((s) => (map[s._id] = s));
        setStationMap(map);
      } catch (err) {
        console.error("❌ Lỗi khi tải dữ liệu:", err);
        if (err.message === "failed_to_get_user_stations") {
          setError(t("failed_to_get_user_stations", "Không thể lấy trạm người dùng"));
        } else if (err.message === "failed_to_get_station_info") {
          setError(t("failed_to_get_station_info", "Không thể lấy thông tin trạm"));
        } else {
          setError(t("unknown_error"));
        }
      } finally {
        setLoading(false);
      }
    };

    checkAuthAndFetch();
  }, [t]);

  // Filter stations based on search term
  const filteredStations = useMemo(() => {
    const term = searchTerm.toLowerCase().trim();
    return stationIds
      .map((id) => stationMap[id])
      .filter(Boolean)
      .filter((s) => {
        if (!term) return true;
        return (
          s.stationName?.toLowerCase().includes(term) ||
          s.stationCode?.toLowerCase().includes(term) ||
          s.location?.toLowerCase().includes(term)
        );
      });
  }, [stationIds, stationMap, searchTerm]);

  const primaryStation = stationIds.map((id) => stationMap[id]).find(Boolean);

  if (loading) {
    return (
      <div className="station-page-container">
        <div className="station-shell" style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "400px" }}>
          <div className="home-loading-row" style={{ width: "100%", padding: "40px" }}>{t("loading_station_data")}</div>
        </div>
      </div>
    );
  }

  if (!isLoggedIn) {
    return (
      <div className="station-page-container">
        <div className="station-shell">
          <div className="station-login-box">
            <i className="fa-solid fa-lock" />
            <h2>{t("login_required_title")}</h2>
            <p>{t("login_required_station_desc")}</p>
            <Link className="station-login-btn" to={`/login?redirect=${encodeURIComponent("/station")}`}>{t("login_now")}</Link>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="station-page-container">
        <div className="station-shell">
          <div className="home-loading-row" style={{ color: "#ef4444", padding: "40px" }}>
            <i className="fa-solid fa-triangle-exclamation" style={{ fontSize: "28px", marginBottom: "12px" }} /><br />
            {error}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="station-page-container">
      <div className="station-shell">

        <section className="station-banner" style={{ backgroundImage: `url(${concreteBannerBg})` }}>
          <div className="station-banner-left">
            <div className="station-banner-eyebrow-container">
              <div className="station-banner-icon-badge">
                <i className="fa-solid fa-industry" />
              </div>
              <h1 className="station-banner-eyebrow-title">{t("my_stations_nav")}</h1>
            </div>
            {primaryStation && (
              <h2 className="station-banner-title">
                {primaryStation.stationName}
                {primaryStation.location ? ` - ${primaryStation.location}` : ""}
              </h2>
            )}
            <p style={{ marginTop: "12px" }}>{t("assigned_stations_desc")}</p>
          </div>
        </section>

        {/* Stats Grid */}
        <section className="station-stats-grid">
          <div className="station-stat-card">
            <div className="station-stat-icon-wrapper total">
              <i className="fa-solid fa-network-wired" />
            </div>
            <div className="station-stat-info">
              <span className="station-stat-label">{t("total_stations")}</span>
              <span className="station-stat-number">{stationIds.length}</span>
              <span className="station-stat-subtext">{t("active_stations")}</span>
            </div>
          </div>

          <div className="station-stat-card">
            <div className="station-stat-icon-wrapper active">
              <i className="fa-solid fa-circle-check" />
            </div>
            <div className="station-stat-info">
              <span className="station-stat-label">{t("active_station")}</span>
              <span className="station-stat-number">{stationIds.length}</span>
              <span className="station-stat-subtext">{t("percent_of_total_stations").replace("{percent}", 100)}</span>
            </div>
          </div>

          <div className="station-stat-card">
            <div className="station-stat-icon-wrapper maintenance">
              <i className="fa-solid fa-screwdriver-wrench" />
            </div>
            <div className="station-stat-info">
              <span className="station-stat-label">{t("maintenance_station")}</span>
              <span className="station-stat-number">0</span>
              <span className="station-stat-subtext">{t("percent_of_total_stations").replace("{percent}", 0)}</span>
            </div>
          </div>

          <div className="station-stat-card">
            <div className="station-stat-icon-wrapper stopped">
              <i className="fa-solid fa-circle-pause" />
            </div>
            <div className="station-stat-info">
              <span className="station-stat-label">{t("stopped_station")}</span>
              <span className="station-stat-number">0</span>
              <span className="station-stat-subtext">{t("percent_of_total_stations").replace("{percent}", 0)}</span>
            </div>
          </div>
        </section>

        {/* Main List Card */}
        <section className="station-list-section">

          {/* Header toolbar */}
          <div className="station-list-header">
            <div className="station-list-title-container">
              <h2 className="station-list-title">{t("assigned_station_list")}</h2>
              <span className="station-list-count-badge">{filteredStations.length}</span>
            </div>

            <div className="station-list-actions">
              <div className="station-search-box">
                <i className="fa-solid fa-magnifying-glass" />
                <input
                  type="text"
                  placeholder={t("search_stations")}
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                />
              </div>

              <div className="station-view-toggle">
                <button
                  type="button"
                  className={`station-view-btn ${viewMode === "list" ? "active" : ""}`}
                  onClick={() => setViewMode("list")}
                >
                  <i className="fa-solid fa-list" /> {t("list_view")}
                </button>
                <button
                  type="button"
                  className={`station-view-btn ${viewMode === "grid" ? "active" : ""}`}
                  onClick={() => setViewMode("grid")}
                >
                  <i className="fa-solid fa-grip" /> {t("grid_view")}
                </button>
              </div>
            </div>
          </div>

          {filteredStations.length === 0 ? (
            <div className="home-loading-row" style={{ padding: "40px" }}>
              {t("no_matching_stations")}
            </div>
          ) : viewMode === "list" ? (
            /* Table list view */
            <>
              <div className="station-table-wrapper">
                <table className="station-custom-table">
                <thead>
                  <tr>
                    <th>{t("station_image")}</th>
                    <th>{t("station_name")}</th>
                    <th>{t("station_code")}</th>
                    <th>{t("product_count")}</th>
                    <th>{t("location")}</th>
                    <th>{t("status")}</th>
                    <th>{t("actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredStations.map((station) => (
                    <tr
                      key={station._id}
                      onClick={() => navigate(`/station/${station.inviteCode || station.stationCode}`)}
                    >
                      <td>
                        <div className="station-table-img-container">
                          {station.imgUrl ? (
                            <img src={resolveStorefrontAssetUrl(station.imgUrl)} alt="" />
                          ) : (
                            <i className="fa-solid fa-industry" />
                          )}
                        </div>
                      </td>
                      <td>
                        <div className="station-name-cell">
                          <span className="station-name-primary">{station.stationName}</span>
                          <span className="station-name-secondary">{station.inviteCode || station.stationCode}</span>
                        </div>
                      </td>
                      <td>{station.stationCode}</td>
                      <td>
                        <span className="station-product-count-badge">
                          {station.productId?.length || 0}
                        </span>
                      </td>
                      <td>{station.location || "-"}</td>
                      <td>
                        <span className="station-status-pill active">
                          ● {t("active")}
                        </span>
                      </td>
                      <td>
                        <div className="station-action-cell" onClick={(e) => e.stopPropagation()}>
                          <button
                            type="button"
                            className="station-btn-detail"
                            onClick={() => navigate(`/station/${station.inviteCode || station.stationCode}`)}
                          >
                            <i className="fa-solid fa-arrow-up-right-from-square" /> {t("view_details")}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
                </table>
              </div>

              <div className="station-mobile-list">
                {filteredStations.map((station) => (
                  <button
                    type="button"
                    className="station-mobile-card"
                    key={station._id}
                    onClick={() => navigate(`/station/${station.inviteCode || station.stationCode}`)}
                  >
                    <span className="station-mobile-card-image">
                      {station.imgUrl ? (
                        <img src={resolveStorefrontAssetUrl(station.imgUrl)} alt="" />
                      ) : (
                        <i className="fa-solid fa-industry" />
                      )}
                    </span>
                    <span className="station-mobile-card-content">
                      <strong>{station.stationName}</strong>
                      <small>{station.inviteCode || station.stationCode}</small>
                      <span className="station-status-pill active">● {t("active")}</span>
                    </span>
                    <i className="fa-solid fa-angle-right station-mobile-card-arrow" />
                  </button>
                ))}
              </div>
            </>
          ) : (
            /* Grid bento view */
            <div className="station-grid-wrapper">
              {filteredStations.map((station) => (
                <div
                  className="station-grid-card"
                  key={station._id}
                  onClick={() => navigate(`/station/${station.inviteCode || station.stationCode}`)}
                >
                  <div className="station-grid-img-container">
                    {station.imgUrl ? (
                      <img src={resolveStorefrontAssetUrl(station.imgUrl)} alt="" />
                    ) : (
                      <i className="fa-solid fa-industry" />
                    )}
                    <div className="station-grid-status">
                      <span className="station-status-pill active">● {t("active")}</span>
                    </div>
                  </div>

                  <div className="station-grid-content">
                    <h3 className="station-grid-title">{station.stationName}</h3>
                    <span className="station-grid-code">{t("code_label")} {station.stationCode}</span>

                    <div className="station-grid-meta">
                      <div className="station-grid-meta-item">
                        <span className="station-grid-meta-label">{t("product_count")}</span>
                        <span className="station-grid-meta-value">{station.productId?.length || 0}</span>
                      </div>
                      <div className="station-grid-meta-item">
                        <span className="station-grid-meta-label">{t("location")}</span>
                        <span className="station-grid-meta-value">{station.location || "-"}</span>
                      </div>
                    </div>
                  </div>

                  <div className="station-grid-actions" onClick={(e) => e.stopPropagation()}>
                    <button
                      type="button"
                      className="station-btn-detail"
                      onClick={() => navigate(`/station/${station.inviteCode || station.stationCode}`)}
                    >
                      <i className="fa-solid fa-arrow-up-right-from-square" /> {t("view_details")}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Pagination Footer */}
          <div className="station-pagination-container">
            <span className="station-pagination-info">
              {t("station_display_range")
                .replace("{first}", filteredStations.length ? 1 : 0)
                .replace("{last}", filteredStations.length)
                .replace("{total}", filteredStations.length)}
            </span>

            <div className="station-pagination-controls">
              <select className="station-page-select" defaultValue="10">
                <option value="10">{t("page_size_10")}</option>
                <option value="20">{t("page_size_20")}</option>
                <option value="50">{t("page_size_50")}</option>
              </select>

              <div className="station-page-nav-wrapper">
                <button type="button" className="station-page-nav-btn" disabled>
                  <i className="fa-solid fa-angle-left" />
                </button>
                <button type="button" className="station-page-nav-btn active">1</button>
                <button type="button" className="station-page-nav-btn" disabled>
                  <i className="fa-solid fa-angle-right" />
                </button>
              </div>
            </div>
          </div>

        </section>

      </div>
    </div>
  );
};

export default Station;
