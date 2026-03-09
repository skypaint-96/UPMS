import { Box, CircularProgress, Typography } from '@mui/material';

export function LoadingPanel({ label = 'Loading…' }: { label?: string }) {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, py: 4 }}>
      <CircularProgress size={24} />
      <Typography>{label}</Typography>
    </Box>
  );
}
