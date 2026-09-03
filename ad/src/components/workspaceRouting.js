export const getPostLoginDestination = (profile, fallback = "/admin/product") =>
  profile?.isPlatformSuperAdmin ? "/admin/system" : fallback;
