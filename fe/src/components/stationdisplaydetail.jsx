import { useEffect, useState, useContext } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { CircularProgress, Alert } from '@mui/material';
import { ShopContext } from '../context/shop.js';
import { useLanguage } from '../context/language.js';
import { isContactOnlyVariant } from '../utils/productpricing';
import {
  getPublicStorefrontStation,
  getStorefrontProductsByIds,
  resolveStorefrontAssetUrl,
} from '../api/storefrontCatalogApi';
import "./style/stationdisplay.css";

const StationDisplayDetail = () => {
  const { t } = useLanguage();
  const { code, section } = useParams();
  const [values, setValues] = useState([]);
  const [productsByValue, setProductsByValue] = useState({});
  const [stationName, setStationName] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // State to hold quantity for each product
  const [quantities, setQuantities] = useState({});

  const navigate = useNavigate();
  const { addToCart } = useContext(ShopContext);

  useEffect(() => {
    if (code) {
      sessionStorage.setItem("activeStationCode", code);
    }
  }, [code]);

  useEffect(() => {
    const fetchProducts = async () => {
      try {
        // Lấy toàn bộ sản phẩm của trạm
        const resStation = await getPublicStorefrontStation(code);
        if (!resStation.ok) throw new Error("station_not_found");
        const station = await resStation.json();
        setStationName(station.stationName || "");
        const productIds = station.productId || [];

        if (!productIds.length) {
          setLoading(false);
          return;
        }

        const resProducts = await getStorefrontProductsByIds(productIds);

        const data = await resProducts.json();
        const allProducts = data.products || [];

        // Lọc sản phẩm theo section và phân nhóm theo value
        const filtered = allProducts.filter(
          (p) => p.section === section && p.display !== false
        );

        const grouped = {};
        filtered.forEach((p) => {
          const val = p.value || t("unknown");
          if (!grouped[val]) grouped[val] = [];
          grouped[val].push(p);
        });

        setValues(Object.keys(grouped));
        setProductsByValue(grouped);

        // Prepopulate default quantities to 1 for all products
        const defaultQtys = {};
        filtered.forEach((p) => {
          defaultQtys[p._id] = 1;
        });
        setQuantities(defaultQtys);

      } catch (err) {
        console.error('Lỗi khi load dữ liệu:', err);
        setError(err.message === "station_not_found" ? t("station_not_found") : t("unknown_error"));
      } finally {
        setLoading(false);
      }
    };

    fetchProducts();
  }, [code, section, t]);

  // Quantity control helpers
  const handleQuantityChange = (productId, val, maxStock) => {
    let num = parseInt(val, 10);
    if (isNaN(num)) {
      setQuantities(prev => ({ ...prev, [productId]: "" }));
      return;
    }
    if (num < 1) num = 1;
    if (maxStock > 0 && num > maxStock) num = maxStock;
    setQuantities(prev => ({ ...prev, [productId]: num }));
  };

  const handleBlur = (productId, maxStock) => {
    const val = quantities[productId];
    if (val === "" || val === undefined || isNaN(val)) {
      setQuantities(prev => ({ ...prev, [productId]: 1 }));
    } else {
      let num = parseInt(val, 10);
      if (num < 1) num = 1;
      if (maxStock > 0 && num > maxStock) num = maxStock;
      setQuantities(prev => ({ ...prev, [productId]: num }));
    }
  };

  const increment = (productId, maxStock) => {
    const current = quantities[productId] || 1;
    let next = current + 1;
    if (maxStock > 0 && next > maxStock) next = maxStock;
    setQuantities(prev => ({ ...prev, [productId]: next }));
  };

  const decrement = (productId) => {
    const current = quantities[productId] || 1;
    let next = current - 1;
    if (next < 1) next = 1;
    setQuantities(prev => ({ ...prev, [productId]: next }));
  };

  const handleAddToCart = async (product) => {
    const qty = quantities[product._id] || 1;
    await addToCart(product._id, 0, qty);
  };

  if (loading) {
    return (
      <div className="station-detail-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: "400px" }}>
        <CircularProgress />
      </div>
    );
  }

  const totalDevices = Object.values(productsByValue).reduce((total, products) => total + products.length, 0);

  if (error) {
    return (
      <div className="station-detail-container" style={{ padding: "40px" }}>
        <Alert severity="error">{error}</Alert>
      </div>
    );
  }

  return (
    <div className="station-detail-container">
      {/* Header Banner */}
      <section className="station-detail-header-banner">
        <div className="station-detail-header-banner-pattern" />

        <div className="station-detail-breadcrumbs">
          <Link to="/station">{t("my_stations_nav")}</Link>
          <span className="separator">›</span>
          <Link to={`/station/${code}`}>{stationName}</Link>
          <span className="separator">›</span>
          <span>{section}</span>
        </div>

        <div className="station-detail-title-wrapper" style={{ textAlign: "center", marginTop: "12px" }}>
          {stationName && (
            <span className="station-detail-subtitle-banner">{stationName}</span>
          )}
          <h1 className="station-detail-title-banner" style={{ marginTop: "4px" }}>{section}</h1>
        </div>
      </section>

      {/* Main product listing by group */}
      <div className="station-detail-content-shell" style={{ paddingTop: "32px" }}>
        {values.map((value) => {
          const visibleProducts = productsByValue[value];
          if (!visibleProducts || visibleProducts.length === 0) return null;

          return (
            <div key={value} className="station-detail-list-wrapper">
              <h2 className="station-detail-list-title">{value}</h2>

              <div className="station-detail-table-wrapper">
                <table className="station-detail-custom-table">
                  <thead>
                    <tr>
                      <th style={{ width: "90px", textAlign: "center" }}>{t("image_heading")}</th>
                      <th>{t("product_name")}</th>
                      <th style={{ width: "160px", textAlign: "center" }}></th>
                      <th style={{ width: "320px", textAlign: "right" }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleProducts.map((product) => {
                      const maxStock = product.variant?.[0]?.quantityForSale ?? 0;
                      const canPurchase = !isContactOnlyVariant(product.variant?.[0]);
                      const currentQty = quantities[product._id] ?? 1;

                      return (
                        <tr key={product._id}>
                          {/* Image */}
                          <td style={{ textAlign: "center" }}>
                            <div className="station-detail-table-img" style={{ margin: "auto" }}>
                              {product.variant?.[0]?.imgUrl ? (
                                <img src={resolveStorefrontAssetUrl(product.variant[0].imgUrl)} alt={product.name} />
                              ) : (
                                <i className="fa-solid fa-microchip" />
                              )}
                            </div>
                          </td>

                          {/* Name and Stock Info */}
                          <td>
                            <div className="station-detail-name-cell">
                              <span className="station-detail-name-primary">{product.name}</span>
                              {canPurchase ? (
                                <span className="station-detail-name-secondary">
                                  {t("quantity_left", "Số lượng đang còn")}: {maxStock}
                                </span>
                              ) : (
                                <span className="station-detail-name-secondary error">
                                  {t("out_of_stock", "Liên hệ")}
                                </span>
                              )}
                            </div>
                          </td>

                          {/* - 1 + Quantity Selector */}
                          <td style={{ textAlign: "center" }}>
                            <div className="quantity-selector">
                              <button
                                type="button"
                                className="quantity-selector-btn"
                                onClick={() => decrement(product._id)}
                                disabled={!canPurchase || currentQty <= 1}
                              >
                                <i className="fa-solid fa-minus" />
                              </button>

                              <input
                                type="text"
                                className="quantity-selector-input"
                                value={currentQty}
                                onChange={(e) => handleQuantityChange(product._id, e.target.value, maxStock)}
                                onBlur={() => handleBlur(product._id, maxStock)}
                                disabled={!canPurchase}
                              />

                              <button
                                type="button"
                                className="quantity-selector-btn"
                                onClick={() => increment(product._id, maxStock)}
                                disabled={!canPurchase || currentQty >= maxStock}
                              >
                                <i className="fa-solid fa-plus" />
                              </button>
                            </div>
                          </td>

                          {/* Actions */}
                          <td>
                            <div className="action-buttons-group">
                              <a className="btn-action-call" href="tel:0813158383">
                                <i className="fa-solid fa-phone" /> 0813 158 383
                              </a>

                              <button
                                type="button"
                                className="btn-action-cart"
                                onClick={() => handleAddToCart(product)}
                                disabled={!canPurchase}
                              >
                                <i className="fa-solid fa-cart-shopping" /> {t("add_to_cart", "Thêm vào giỏ hàng")}
                              </button>

                              <button
                                type="button"
                                className="btn-action-detail"
                                onClick={() => navigate(`/product/${product._id}`)}
                              >
                                {t("details", "Chi tiết")}
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          );
        })}

        {/* Mock pagination matching the mockup */}
        <div className="station-detail-pagination">
          <span className="station-pagination-info" style={{ color: "#64748b", fontSize: "13px", fontWeight: "500" }}>
            {t("device_display_range")
              .replace("{first}", totalDevices ? 1 : 0)
              .replace("{last}", totalDevices)
              .replace("{total}", totalDevices)}
          </span>

          <div className="station-pagination-controls" style={{ display: "flex", alignItems: "center", gap: "12px" }}>
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
      </div>
    </div>
  );
};

export default StationDisplayDetail;
