import { Box, CircularProgress } from "@mui/material";
import { usePermissions } from "../context/permissioncontext";
import WorkspaceSelector from "./workspaceSelector";

const WorkspaceGate = ({ children }) => {
  const { profile, isLoading } = usePermissions();

  if (isLoading) {
    return <Box sx={{ display: "grid", placeItems: "center", height: "100vh" }}><CircularProgress /></Box>;
  }

  const requiresSelection = profile?.isControlPlaneIdentity && profile?.requiresWorkspaceSelection;
  if (requiresSelection) {
    return <WorkspaceSelector profile={profile} open required onClose={() => {}} />;
  }

  return children;
};

export default WorkspaceGate;
