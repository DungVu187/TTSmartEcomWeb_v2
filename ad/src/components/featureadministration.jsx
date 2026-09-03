import { useEffect, useState } from "react";
import { Alert, Box, FormControl, FormControlLabel, InputLabel, MenuItem, Paper, Select, Stack, Switch, Typography } from "@mui/material";
import toast from "react-hot-toast";
import { getFeatureSettings, getPlatformBranches, getPlatformCompanies, setFeatureSetting } from "../api/accountApi";

const FeatureAdministration = () => {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [branches, setBranches] = useState([]);
  const [branchId, setBranchId] = useState("");
  const [features, setFeatures] = useState([]);
  const loadFeatures = async (selectedCompany = companyId, selectedBranch = branchId) => {
    if (!selectedCompany) return;
    try { setFeatures((await getFeatureSettings({ companyId: selectedCompany, branchId: selectedBranch })).features || []); }
    catch (error) { toast.error(error.message); }
  };
  useEffect(() => { getPlatformCompanies().then((result) => { setCompanies(result.companies || []); setCompanyId(result.companies?.[0]?.companyId || ""); }).catch((error) => toast.error(error.message)); }, []);
  useEffect(() => {
    if (!companyId) return;
    setBranchId("");
    getPlatformBranches(companyId).then((result) => setBranches(result.branches || [])).catch((error) => toast.error(error.message));
    loadFeatures(companyId, "");
  }, [companyId]);
  useEffect(() => { if (companyId) loadFeatures(companyId, branchId); }, [branchId]);
  return <Box sx={{ p: 2, maxWidth: 1000, mx: "auto" }}>
    <Typography variant="h4">Chức năng theo công ty</Typography>
    <Typography color="text.secondary" mb={2}>Bật chức năng cho công ty; chi nhánh chỉ có thể bị giới hạn thêm.</Typography>
    <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} mb={2}>
      <FormControl fullWidth><InputLabel>Công ty</InputLabel><Select label="Công ty" value={companyId} onChange={(e) => setCompanyId(e.target.value)}>
        {companies.map((company) => <MenuItem key={company.companyId} value={company.companyId}>{company.name}</MenuItem>)}
      </Select></FormControl>
      <FormControl fullWidth><InputLabel>Giới hạn tại chi nhánh</InputLabel><Select label="Giới hạn tại chi nhánh" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
        <MenuItem value="">Toàn công ty</MenuItem>{branches.map((branch) => <MenuItem key={branch.branchId} value={branch.branchId}>{branch.name}</MenuItem>)}
      </Select></FormControl>
    </Stack>
    <Stack spacing={1}>{features.map((feature) => {
      const checked = branchId ? feature.companyEnabled && feature.branchEnabled !== false : feature.companyEnabled;
      return <Paper key={feature.featureId} variant="outlined" sx={{ p: 1.5 }}><FormControlLabel
        control={<Switch checked={checked} disabled={Boolean(branchId) && !feature.companyEnabled} onChange={async (event) => {
          try { await setFeatureSetting({ companyId, branchId, featureId: feature.featureId, isEnabled: event.target.checked }); await loadFeatures(); toast.success("Đã cập nhật chức năng"); }
          catch (error) { toast.error(error.message); }
        }} />}
        label={<Typography fontWeight={700}>{feature.name}</Typography>} />
      </Paper>;
    })}{features.length === 0 && <Alert severity="warning">Chưa có dữ liệu chức năng được seed trong hệ thống.</Alert>}</Stack>
  </Box>;
};

export default FeatureAdministration;
