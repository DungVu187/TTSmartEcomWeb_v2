import { useCallback, useEffect, useState } from "react";
import {
  TextField,
  Button,
  Select,
  MenuItem,
  Box,
  Pagination,
  Typography,
  InputLabel,
  Dialog,
  DialogContent,
  DialogTitle,
} from "@mui/material";
import { Link, useLocation, useNavigate } from "react-router-dom";
import FilterListIcon from "@mui/icons-material/FilterList";
import "./styles/product.css";
import Item from "../components/item";
import { useLanguage } from "../context/language.js";
import { getCustomerProfile } from "../api/customerAccountApi";
import {
  getStorefrontBrands,
  getStorefrontProductTypes,
  getStorefrontSections,
  getStorefrontSectionValues,
  getStorefrontStationsByIds,
  listStorefrontProducts,
} from "../api/storefrontCatalogApi";
const ALL_FILTER_VALUE = "__all__";
const PRODUCT_PAGE_LIMIT = 12;
const filterSelectMenuProps = {
  disableScrollLock: true,
  PaperProps: {
    sx: {
      maxHeight: 320,
      mt: 0.5,
      border: "1px solid #e5eaf0",
      borderRadius: "8px",
      boxShadow: "0 10px 28px rgba(16, 42, 67, 0.14)",
    },
  },
};

function Product() {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const location = useLocation();
  const queryParams = new URLSearchParams(location.search);

  const initialFilters = {
    search: queryParams.get("search") || "",
    brand: queryParams.get("brand") || ALL_FILTER_VALUE,
    type: queryParams.get("type") || ALL_FILTER_VALUE,
    section: queryParams.get("section") || ALL_FILTER_VALUE,
    value: queryParams.get("value") || ALL_FILTER_VALUE,
    sortBy: queryParams.get("sortBy") || "purchaseCount",
    sortOrder: queryParams.get("sortOrder") || "desc",
  };

  const [products, setProducts] = useState([]);
  const [page, setPage] = useState(() => {
    const pageParam = queryParams.get("page");
    return pageParam && !isNaN(parseInt(pageParam)) ? parseInt(pageParam) : 1;
  });
  const [totalPages, setTotalPages] = useState(1);
  const [totalProducts, setTotalProducts] = useState(0);
  const [isLoadingProducts, setIsLoadingProducts] = useState(true);
  const [filters, setFilters] = useState(initialFilters);
  const [openDialog, setOpenDialog] = useState(false);

  const [brands, setBrands] = useState([]);
  const [types, setTypes] = useState([]);
  const [sections, setSections] = useState([]);
  const [values, setValues] = useState([]);
  const [initialSection] = useState(() => queryParams.get("section"));

  // States mới cho việc lọc theo trạm trộn
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [userStations, setUserStations] = useState([]);
  const [selectedStation, setSelectedStation] = useState(queryParams.get("stationId") || ALL_FILTER_VALUE);

  // Check đăng nhập và load danh sách trạm
  useEffect(() => {
    const checkAuthAndLoadStations = async () => {
      try {
        const response = await getCustomerProfile();
        if (response.ok) {
          const userData = await response.json();
          setIsLoggedIn(true);
          const codes = userData.station || []; // these are station IDs!
          if (codes.length > 0) {
            const stationsRes = await getStorefrontStationsByIds(codes);
            if (stationsRes.ok) {
              const stationsData = await stationsRes.json();
              setUserStations(stationsData);
            }
          }
        } else {
          setIsLoggedIn(false);
        }
      } catch (error) {
        console.error("Error loading user profile or stations:", error);
        setIsLoggedIn(false);
      }
    };
    checkAuthAndLoadStations();
  }, []);

  useEffect(() => {
    const currentQueryParams = new URLSearchParams(location.search);
    const updatedFilters = {
      search: currentQueryParams.get("search") || "",
      brand: currentQueryParams.get("brand") || ALL_FILTER_VALUE,
      type: currentQueryParams.get("type") || ALL_FILTER_VALUE,
      section: currentQueryParams.get("section") || ALL_FILTER_VALUE,
      value: currentQueryParams.get("value") || ALL_FILTER_VALUE,
      sortBy: currentQueryParams.get("sortBy") || "purchaseCount",
      sortOrder: currentQueryParams.get("sortOrder") || "desc",
    };
    setFilters(updatedFilters);
    const pageParam = currentQueryParams.get("page");
    const parsedPage = pageParam && !isNaN(parseInt(pageParam)) ? parseInt(pageParam) : 1;
    setPage(parsedPage);

    const stationIdParam = currentQueryParams.get("stationId") || ALL_FILTER_VALUE;
    setSelectedStation(stationIdParam);

    const fetchProductsFromUrl = async () => {
      setIsLoadingProducts(true);
      try {
        const response = await listStorefrontProducts({
          page: parsedPage,
          limit: PRODUCT_PAGE_LIMIT,
          search: updatedFilters.search,
          brand: updatedFilters.brand === ALL_FILTER_VALUE ? "" : updatedFilters.brand,
          type: updatedFilters.type === ALL_FILTER_VALUE ? "" : updatedFilters.type,
          section: updatedFilters.section === ALL_FILTER_VALUE ? "" : updatedFilters.section,
          value: updatedFilters.value === ALL_FILTER_VALUE ? "" : updatedFilters.value,
          sortBy: updatedFilters.sortBy,
          sortOrder: updatedFilters.sortOrder,
          display: "true",
          stationId: stationIdParam === ALL_FILTER_VALUE ? "" : stationIdParam,
        });
        const data = await response.json();
        setProducts(data.products || []);
        setTotalProducts(data.total || 0);
        setTotalPages(Math.ceil((data.total || 0) / PRODUCT_PAGE_LIMIT));
      } catch (error) {
        console.error("Error fetching products:", error);
      } finally {
        setIsLoadingProducts(false);
      }
    };

    fetchProductsFromUrl();
  }, [location.search]);

  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters((prev) => ({
      ...prev,
      [name]: value,
      ...(name === "section" ? { value: ALL_FILTER_VALUE } : {}),
    }));

    if (name === "section" && value !== ALL_FILTER_VALUE) {
      fetchValues(value);
    } else if (name === "section" && value === ALL_FILTER_VALUE) {
      setValues([]);
    }
  };

  const fetchValues = useCallback(async (sectionName) => {
    try {
      const response = await getStorefrontSectionValues(sectionName);
      const data = await response.json();
      setValues(data);
    } catch (error) {
      console.error("Error fetching values:", error);
      setValues([]);
    }
  }, []);

  const handleStationChange = (e) => {
    const stationId = e.target.value;
    setSelectedStation(stationId);

    if (stationId !== ALL_FILTER_VALUE) {
      const selected = userStations.find(s => s._id === stationId);
      if (selected && selected.stationCode) {
        sessionStorage.setItem("activeStationCode", selected.stationCode);
      }
    } else {
      sessionStorage.removeItem("activeStationCode");
    }

    const urlQuery = new URLSearchParams({
      ...filters,
      brand: filters.brand === ALL_FILTER_VALUE ? "" : filters.brand,
      type: filters.type === ALL_FILTER_VALUE ? "" : filters.type,
      section: filters.section === ALL_FILTER_VALUE ? "" : filters.section,
      value: filters.value === ALL_FILTER_VALUE ? "" : filters.value,
      sortBy: filters.sortBy || "purchaseCount",
      sortOrder: filters.sortOrder || "desc",
      stationId: stationId === ALL_FILTER_VALUE ? "" : stationId,
      page: 1,
    }).toString();

    navigate(`/product?${urlQuery}`);
    setPage(1);
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    const urlQuery = new URLSearchParams({
      ...filters,
      brand: filters.brand === ALL_FILTER_VALUE ? "" : filters.brand,
      type: filters.type === ALL_FILTER_VALUE ? "" : filters.type,
      section: filters.section === ALL_FILTER_VALUE ? "" : filters.section,
      value: filters.value === ALL_FILTER_VALUE ? "" : filters.value,
      sortBy: filters.sortBy || "purchaseCount",
      sortOrder: filters.sortOrder || "desc",
      stationId: selectedStation === ALL_FILTER_VALUE ? "" : selectedStation,
      page: 1,
    }).toString();

    navigate(`/product?${urlQuery}`);
    setPage(1);
    setOpenDialog(false);
  };

  const handlePageChange = (event, newPage) => {
    const urlQuery = new URLSearchParams({
      ...filters,
      brand: filters.brand === ALL_FILTER_VALUE ? "" : filters.brand,
      type: filters.type === ALL_FILTER_VALUE ? "" : filters.type,
      section: filters.section === ALL_FILTER_VALUE ? "" : filters.section,
      value: filters.value === ALL_FILTER_VALUE ? "" : filters.value,
      sortBy: filters.sortBy || "purchaseCount",
      sortOrder: filters.sortOrder || "desc",
      stationId: selectedStation === ALL_FILTER_VALUE ? "" : selectedStation,
      page: newPage,
    }).toString();

    setPage(Number(newPage) || 1);
    navigate(`/product?${urlQuery}`);
  };

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [brandsResponse, typesResponse, sectionsResponse] =
          await Promise.all([
            getStorefrontBrands(),
            getStorefrontProductTypes({ cache: "no-store" }),
            getStorefrontSections(),
          ]);
        const brandsData = await brandsResponse.json();
        const typesData = await typesResponse.json();
        const sectionsData = await sectionsResponse.json();
        setBrands(brandsData);
        setTypes(typesData);
        setSections(sectionsData);

        if (initialSection && initialSection !== ALL_FILTER_VALUE) {
          await fetchValues(initialSection);
        }
      } catch (error) {
        console.error("Error fetching data:", error);
      }
    };
    fetchData();
  }, [fetchValues, initialSection]);

  const isValueDisabled = filters.section === ALL_FILTER_VALUE;

  const filterForm = (
    <form onSubmit={handleSubmit} className="filter-product-string">
      <Typography variant="h6">{t("search_products")}</Typography>

      {/* Chọn trạm trộn (Chỉ hiển thị cho khách hàng đã đăng nhập và có trạm) */}
      {isLoggedIn && userStations.length > 0 && (
        <Box sx={{ mb: 2 }}>
          <InputLabel sx={{ fontWeight: "bold", mb: 0.5 }}>{t("select_mixing_station")}</InputLabel>
          <Select
            value={selectedStation}
            onChange={handleStationChange}
            MenuProps={filterSelectMenuProps}
            size="small"
            fullWidth
            sx={{ backgroundColor: "white" }}
          >
            <MenuItem value={ALL_FILTER_VALUE}>{t("all_my_stations")}</MenuItem>
            {userStations.map((station, index) => (
              <MenuItem key={index} value={station._id}>
                {station.stationName || station.stationCode} ({station.stationCode})
              </MenuItem>
            ))}
          </Select>
        </Box>
      )}

      <TextField
        label={t("search_by_name")}
        variant="outlined"
        name="search"
        value={filters.search}
        onChange={handleFilterChange}
        fullWidth
        size="small"
        margin="normal"
      />
      <InputLabel>{t("search_by_brand")}</InputLabel>
      <Select
        value={filters.brand || ALL_FILTER_VALUE}
        MenuProps={filterSelectMenuProps}
        onChange={(e) =>
          handleFilterChange({
            target: { name: "brand", value: e.target.value },
          })
        }
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value={ALL_FILTER_VALUE}>{t("all_brands")}</MenuItem>
        {brands.map((brand, index) => (
          <MenuItem key={index} value={brand.Brand}>
            {brand.Brand}
          </MenuItem>
        ))}
      </Select>
      <InputLabel>{t("search_by_type")}</InputLabel>
      <Select
        value={filters.type || ALL_FILTER_VALUE}
        MenuProps={filterSelectMenuProps}
        onChange={(e) =>
          handleFilterChange({
            target: { name: "type", value: e.target.value },
          })
        }
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value={ALL_FILTER_VALUE}>{t("all_types")}</MenuItem>
        {types.map((type, index) => (
          <MenuItem key={index} value={type.Type}>
            {type.Type}
          </MenuItem>
        ))}
      </Select>
      <InputLabel>{t("search_by_section")}</InputLabel>
      <Select
        value={filters.section || ALL_FILTER_VALUE}
        MenuProps={filterSelectMenuProps}
        onChange={handleFilterChange}
        name="section"
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value={ALL_FILTER_VALUE}>{t("all_sections")}</MenuItem>
        {sections.map((section, index) => (
          <MenuItem key={index} value={section}>
            {section}
          </MenuItem>
        ))}
      </Select>
      <InputLabel>{t("search_by_equipment")}</InputLabel>
      <Select
        value={filters.value || ALL_FILTER_VALUE}
        MenuProps={filterSelectMenuProps}
        onChange={(e) =>
          handleFilterChange({
            target: { name: "value", value: e.target.value },
          })
        }
        disabled={isValueDisabled}
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value={ALL_FILTER_VALUE}>{t("all_equipment")}</MenuItem>
        {values.map((value, index) => (
          <MenuItem key={index} value={value}>
            {value}
          </MenuItem>
        ))}
      </Select>
      <Typography variant="h6">{t("sort_by")}</Typography>
      <Select
        value={filters.sortBy || "purchaseCount"}
        MenuProps={filterSelectMenuProps}
        onChange={(e) =>
          handleFilterChange({
            target: { name: "sortBy", value: e.target.value },
          })
        }
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value="createdAt">{t("created_date")}</MenuItem>
        <MenuItem value="averageReviews">{t("rating")}</MenuItem>
        <MenuItem value="purchaseCount">{t("purchases")}</MenuItem>
      </Select>
      <Select
        value={filters.sortOrder || "desc"}
        MenuProps={filterSelectMenuProps}
        onChange={(e) =>
          handleFilterChange({
            target: { name: "sortOrder", value: e.target.value },
          })
        }
        size="small"
        fullWidth
        sx={{ margin: "8px 0" }}
      >
        <MenuItem value="desc">{t("descending")}</MenuItem>
        <MenuItem value="asc">{t("ascending")}</MenuItem>
      </Select>
      <div
        style={{
          display: "flex",
          justifyContent: "center",
          marginTop: "16px",
          width: "full",
        }}
      >
        <Button type="submit" variant="contained" color="primary" fullWidth>
          {t("search")}
        </Button>
      </div>
    </form>
  );

  const firstProductIndex = totalProducts === 0 ? 0 : (page - 1) * PRODUCT_PAGE_LIMIT + 1;
  const lastProductIndex = Math.min(page * PRODUCT_PAGE_LIMIT, totalProducts);

  return (
    <main className="product-catalog-page">
      <div className="product-catalog-shell">
        <nav className="product-breadcrumb" aria-label={t("breadcrumb")}>
          <Link to="/"><i className="fa-solid fa-house" /> {t("home")}</Link>
          <i className="fa-solid fa-angle-right" />
          <span>{t("products")}</span>
        </nav>

        <div className="product-page-title-row">
          <h1>{t("products")}</h1>
          <Button
            className="filter-button"
            onClick={() => setOpenDialog(true)}
            variant="outlined"
            startIcon={<FilterListIcon />}
          >
            {t("filters")}
          </Button>
        </div>

        <div className="product-filter-main-container">
          <aside className="filter-desktop">{filterForm}</aside>

          <Dialog
            open={openDialog}
            onClose={() => setOpenDialog(false)}
            fullWidth
            maxWidth="sm"
          >
            <DialogTitle>{t("product_filters")}</DialogTitle>
            <DialogContent>{filterForm}</DialogContent>
          </Dialog>

          <section className="product-results-panel">
            <div className="product-results-toolbar">
              <span>
                {isLoadingProducts
                  ? t("loading_products")
                  : t("product_display_range")
                    .replace("{first}", firstProductIndex)
                    .replace("{last}", lastProductIndex)
                    .replace("{total}", totalProducts)}
              </span>
              <div className="product-view-indicator" aria-hidden="true">
                <i className="fa-solid fa-table-cells-large is-active" />
                <i className="fa-solid fa-list" />
              </div>
            </div>

            {isLoadingProducts ? (
              <div className="product-loading-state">
                <span className="product-loading-spinner" />
                {t("loading_products")}
              </div>
            ) : products.length > 0 ? (
              <div className="product-list-container">
                {products.map((product) => (
                  <div key={product._id} className="product-item">
                    <Item product={product} />
                  </div>
                ))}
              </div>
            ) : (
              <Box className="product-empty-state">
                {!isLoggedIn ? (
                  <Box>
                    <Typography variant="h6" sx={{ color: "text.secondary", mb: 2 }}>
                      {t("login_to_view_station_items")}
                    </Typography>
                    <Button variant="contained" onClick={() => navigate("/login")}>
                      {t("login_now")}
                    </Button>
                  </Box>
                ) : userStations.length === 0 ? (
                  <Typography variant="h6" sx={{ color: "text.secondary" }}>
                    {t("no_stations_configured")}
                  </Typography>
                ) : (
                  <Typography variant="h6" sx={{ color: "text.secondary" }}>
                    {t("no_items_configured")}
                  </Typography>
                )}
              </Box>
            )}

            {totalPages > 1 && (
              <div className="product-pagination">
                <Pagination
                  count={totalPages}
                  page={page}
                  onChange={(event, value) => handlePageChange(event, value)}
                  size="medium"
                  color="primary"
                />
              </div>
            )}
          </section>
        </div>
      </div>
    </main>
  );
}

export default Product;
