import { Navigate } from "react-router-dom";
import { Box, CircularProgress } from "@mui/material";
import { usePermissions } from "../context/permissioncontext";

const SystemWorkspaceGuard = ({ children }) => {
  const { profile, isLoading } = usePermissions();

  if (isLoading) {
    return <Box sx={{ display: "grid", placeItems: "center", minHeight: "60vh" }}><CircularProgress /></Box>;
  }

  return profile?.isPlatformSuperAdmin ? children : <Navigate to="/product" replace />;
};

export default SystemWorkspaceGuard;
