import { useMemo, useState } from 'react';
import { BrowserRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  CGITopNavigation,
  CGILeftNavigation,
  DrawerProvider,
  edsTheme,
} from 'eds-react-app';
import { CssBaseline, Box } from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import HomeOutlined from '@mui/icons-material/HomeOutlined';
import DatasetOutlined from '@mui/icons-material/DatasetOutlined';
import SellOutlined from '@mui/icons-material/SellOutlined';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import UploadFileOutlined from '@mui/icons-material/UploadFileOutlined';
import DescriptionOutlined from '@mui/icons-material/DescriptionOutlined';
import WorkHistoryOutlined from '@mui/icons-material/WorkHistoryOutlined';
import { DashboardPage } from './pages/DashboardPage';
import { JobsPage } from './pages/JobsPage';
import { ReportsPage } from './pages/ReportsPage';
import { SnapshotsPage } from './pages/SnapshotsPage';
import { SourcesPage } from './pages/SourcesPage';
import { TicketDetailPage } from './pages/TicketDetailPage';
import { TicketsPage } from './pages/TicketsPage';
import { UploadPage } from './pages/UploadPage';
import 'eds-react-app/dist/style.css';

function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(true);

  const menuItems = useMemo(
    () => [
      { text: 'Dashboard', route: '/', icon: HomeOutlined },
      { text: 'Snapshots', route: '/snapshots', icon: DatasetOutlined },
      { text: 'Tickets', route: '/tickets', icon: SellOutlined },
      { text: 'ITSM Sources', route: '/itsm-sources', icon: Inventory2Outlined },
      { text: 'Upload', route: '/upload', icon: UploadFileOutlined },
      { text: 'Reports', route: '/reports', icon: DescriptionOutlined },
      { text: 'Jobs', route: '/jobs', icon: WorkHistoryOutlined },
    ],
    [],
  );

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <CGITopNavigation
        open={open}
        setOpen={setOpen}
        appName="UPMS"
        visibleIconMenu={true}
        visibleUserMenu={false}
        visibleSearch={false}
        visibleSearchBar={false}
        leftMenuList={menuItems}
        linkHandle={(route) => navigate(route)}
        navigationRoute={(route) => navigate(route)}
      />
      <Box sx={{ display: 'flex' }}>
        <Box sx={{ minWidth: 280, display: { xs: 'none', md: 'block' } }}>
          <CGILeftNavigation leftMenuList={menuItems} location={location as any} linkHandle={(route) => navigate(route)} />
        </Box>
        <Box component="main" sx={{ flex: 1, p: 3 }}>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/snapshots" element={<SnapshotsPage />} />
            <Route path="/tickets" element={<TicketsPage />} />
            <Route path="/tickets/:company/:ticketKey" element={<TicketDetailPage />} />
            <Route path="/itsm-sources" element={<SourcesPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/jobs" element={<JobsPage />} />
          </Routes>
        </Box>
      </Box>
    </Box>
  );
}

export default function App() {
  return (
    <ThemeProvider theme={edsTheme as any}>
      <CssBaseline />
      <DrawerProvider>
        <BrowserRouter>
          <AppShell />
        </BrowserRouter>
      </DrawerProvider>
    </ThemeProvider>
  );
}
