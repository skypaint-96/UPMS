import { useMemo, useState } from 'react';
import { BrowserRouter, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { CGITopNavigation, DrawerProvider, edsTheme } from 'eds-react-app';
import {
  Box,
  CssBaseline,
  Divider,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Tooltip,
  Typography,
} from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import HomeOutlined from '@mui/icons-material/HomeOutlined';
import SellOutlined from '@mui/icons-material/SellOutlined';
import DescriptionOutlined from '@mui/icons-material/DescriptionOutlined';
import AssignmentOutlined from '@mui/icons-material/AssignmentOutlined';
import SettingsOutlined from '@mui/icons-material/SettingsOutlined';
import DatasetOutlined from '@mui/icons-material/DatasetOutlined';
import Inventory2Outlined from '@mui/icons-material/Inventory2Outlined';
import UploadFileOutlined from '@mui/icons-material/UploadFileOutlined';
import WorkHistoryOutlined from '@mui/icons-material/WorkHistoryOutlined';
import ArticleOutlined from '@mui/icons-material/ArticleOutlined';
import { DashboardPage } from './pages/DashboardPage';
import { JobsPage } from './pages/JobsPage';
import { ProblemRequestsPage } from './pages/ProblemRequestsPage';
import { ReportsPage } from './pages/ReportsPage';
import { ReportTemplatesPage } from './pages/ReportTemplatesPage';
import { SnapshotsPage } from './pages/SnapshotsPage';
import { SourcesPage } from './pages/SourcesPage';
import { TicketDetailPage } from './pages/TicketDetailPage';
import { TicketsPage } from './pages/TicketsPage';
import { UploadPage } from './pages/UploadPage';
import './styles/eds-react-app.css';

type AppMenuItem = {
  text: string;
  route: string;
  icon: typeof HomeOutlined;
  helper?: string;
};

const sidebarWidthExpanded = 280;
const sidebarWidthCollapsed = 84;

function NavigationList({
  items,
  collapsed,
  selectedPath,
  onNavigate,
}: {
  items: AppMenuItem[];
  collapsed: boolean;
  selectedPath: string;
  onNavigate: (route: string) => void;
}) {
  return (
    <List sx={{ pt: 1 }}>
      {items.map((item) => {
        const selected = selectedPath === item.route || (item.route !== '/' && selectedPath.startsWith(item.route));
        const icon = <item.icon />;
        const button = (
          <ListItemButton
            selected={selected}
            onClick={() => onNavigate(item.route)}
            sx={{
              mx: 1,
              mb: 0.5,
              minHeight: 52,
              borderRadius: 2,
              justifyContent: collapsed ? 'center' : 'flex-start',
              px: collapsed ? 1.5 : 2,
            }}
          >
            <ListItemIcon sx={{ minWidth: collapsed ? 0 : 40, mr: collapsed ? 0 : 1, justifyContent: 'center' }}>{icon}</ListItemIcon>
            {!collapsed ? (
              <ListItemText
                primary={item.text}
                secondary={item.helper}
                primaryTypographyProps={{ fontWeight: selected ? 700 : 500 }}
              />
            ) : null}
          </ListItemButton>
        );

        return collapsed ? (
          <Tooltip title={item.text} placement="right" key={item.route}>
            {button}
          </Tooltip>
        ) : (
          <Box key={item.route}>{button}</Box>
        );
      })}
    </List>
  );
}

function AppShell() {
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(true);
  const [adminOpen, setAdminOpen] = useState(false);

  const primaryItems = useMemo<AppMenuItem[]>(
    () => [
      { text: 'Dashboard', route: '/', icon: HomeOutlined, helper: 'System overview' },
      { text: 'Tickets', route: '/tickets', icon: SellOutlined, helper: 'Query ticket state' },
      { text: 'Problem Requests', route: '/problem-requests', icon: AssignmentOutlined, helper: 'Submit and triage intake' },
      { text: 'Reports', route: '/reports', icon: DescriptionOutlined, helper: 'Generate from templates' },
    ],
    [],
  );

  const adminItems = useMemo<AppMenuItem[]>(
    () => [
      { text: 'ITSM Sources', route: '/itsm-sources', icon: Inventory2Outlined, helper: 'Create and map sources' },
      { text: 'Upload Snapshots', route: '/upload', icon: UploadFileOutlined, helper: 'Queue single or bulk ingest' },
      { text: 'Snapshots', route: '/snapshots', icon: DatasetOutlined, helper: 'Browse uploaded snapshots' },
      { text: 'Jobs', route: '/jobs', icon: WorkHistoryOutlined, helper: 'Monitor worker jobs' },
      { text: 'Report Templates', route: '/report-templates', icon: ArticleOutlined, helper: 'Curate template library' },
    ],
    [],
  );

  const sidebarWidth = open ? sidebarWidthExpanded : sidebarWidthCollapsed;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <CGITopNavigation
        open={open}
        setOpen={setOpen}
        onClick={() => setOpen((current) => !current)}
        icon={SettingsOutlined}
        handleIconClick={() => setAdminOpen(true)}
        appName="UPMS"
        visibleIconMenu={false}
        visibleUserMenu={false}
        visibleSearch={false}
        visibleSearchBar={false}
      />

      <Box sx={{ display: 'flex', minHeight: 'calc(100vh - 64px)' }}>
        <Box
          component="aside"
          sx={{
            width: sidebarWidth,
            transition: 'width 0.2s ease',
            borderRight: 1,
            borderColor: 'divider',
            bgcolor: 'background.paper',
            overflow: 'hidden',
            display: 'block',
          }}
        >
          <Box sx={{ p: open ? 2 : 1.5 }}>
            {!open ? null : (
              <Typography variant="overline" color="text.secondary">
                Navigation
              </Typography>
            )}
          </Box>
          <NavigationList items={primaryItems} collapsed={!open} selectedPath={location.pathname} onNavigate={navigate} />
        </Box>

        <Box component="main" sx={{ flex: 1, p: { xs: 2, md: 3 } }}>
          <Routes>
            <Route path="/" element={<DashboardPage />} />
            <Route path="/tickets" element={<TicketsPage />} />
            <Route path="/tickets/:company/:ticketKey" element={<TicketDetailPage />} />
            <Route path="/reports" element={<ReportsPage />} />
            <Route path="/problem-requests" element={<ProblemRequestsPage />} />
            <Route path="/itsm-sources" element={<SourcesPage />} />
            <Route path="/upload" element={<UploadPage />} />
            <Route path="/snapshots" element={<SnapshotsPage />} />
            <Route path="/jobs" element={<JobsPage />} />
            <Route path="/report-templates" element={<ReportTemplatesPage />} />
          </Routes>
        </Box>
      </Box>

      <Drawer anchor="right" open={adminOpen} onClose={() => setAdminOpen(false)}>
        <Box sx={{ width: 360, maxWidth: '100vw', p: 2 }} role="presentation">
          <Typography variant="h6" sx={{ mb: 1 }}>
            Power user menu
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Use these admin pages to manage ITSM source definitions, snapshots, jobs, and the shared report-template library.
          </Typography>
          <Divider sx={{ mb: 1 }} />
          <NavigationList
            items={adminItems}
            collapsed={false}
            selectedPath={location.pathname}
            onNavigate={(route) => {
              navigate(route);
              setAdminOpen(false);
            }}
          />
        </Box>
      </Drawer>
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
