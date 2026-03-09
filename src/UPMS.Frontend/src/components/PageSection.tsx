import { Box, Paper, Typography } from '@mui/material';
import { ReactNode } from 'react';

export function PageSection({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <Paper sx={{ p: 3, mb: 3 }}>
      <Box sx={{ mb: 2 }}>
        <Typography variant="h5">{title}</Typography>
        {description ? <Typography color="text.secondary">{description}</Typography> : null}
      </Box>
      {children}
    </Paper>
  );
}
