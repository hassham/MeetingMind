import MenuIcon from '@mui/icons-material/Menu'
import {
  AppBar,
  Box,
  Button,
  Container,
  CssBaseline,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  ThemeProvider,
  Toolbar,
  Typography,
  createTheme,
} from '@mui/material'
import { useState } from 'react'
import { Link, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import App from './App'
import DashboardPage from './dashboard/DashboardPage'

const theme = createTheme({
  palette: {
    primary: { main: '#285f8f' },
    secondary: { main: '#2f7d59' },
    background: { default: '#f6f7f9' },
    warning: { main: '#a86620' },
  },
  shape: { borderRadius: 8 },
  typography: {
    fontFamily:
      'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  },
})

const destinations = [
  ['Dashboard', '/'],
  ['New processing job', '/process/new'],
  ['Meeting minutes', '/meetings'],
  ['Actions', '/actions'],
  ['All processing', '/processing'],
] as const

export default function RootApp() {
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()

  const navigation = (
    <List aria-label="Primary navigation">
      {destinations.map(([label, path]) => (
        <ListItemButton
          component={NavLink}
          key={path}
          selected={path === '/' ? location.pathname === '/' : location.pathname.startsWith(path)}
          to={path}
          onClick={() => setMenuOpen(false)}
        >
          <ListItemText primary={label} />
        </ListItemButton>
      ))}
    </List>
  )

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box className="route-shell">
        <AppBar position="static" color="inherit" elevation={0} className="site-header">
          <Container maxWidth="xl">
            <Toolbar disableGutters>
              <Typography
                component={Link}
                to="/"
                variant="h6"
                color="text.primary"
                sx={{ textDecoration: 'none', fontWeight: 800, flexGrow: { xs: 1, lg: 0 } }}
              >
                MeetingMind AI
              </Typography>
              <Box component="nav" sx={{ display: { xs: 'none', lg: 'block' }, ml: 4, flexGrow: 1 }}>
                <Stack direction="row" spacing={0.5}>
                  {destinations.map(([label, path]) => (
                    <Button
                      component={NavLink}
                      key={path}
                      to={path}
                      aria-current={
                        path === '/'
                          ? location.pathname === '/'
                            ? 'page'
                            : undefined
                          : location.pathname.startsWith(path)
                            ? 'page'
                            : undefined
                      }
                    >
                      {label}
                    </Button>
                  ))}
                </Stack>
              </Box>
              <Button
                component={Link}
                to="/process/new"
                variant="contained"
                sx={{ display: { xs: 'none', sm: 'inline-flex' } }}
              >
                New processing job
              </Button>
              <IconButton
                aria-label="Open navigation"
                onClick={() => setMenuOpen(true)}
                sx={{ display: { lg: 'none' }, ml: 1 }}
              >
                <MenuIcon />
              </IconButton>
            </Toolbar>
          </Container>
        </AppBar>
        <Drawer open={menuOpen} onClose={() => setMenuOpen(false)}>
          <Box sx={{ width: 280, pt: 2 }}>{navigation}</Box>
        </Drawer>
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/process/new" element={<App pageMode="new" />} />
          <Route path="/processing" element={<App pageMode="processing" />} />
          <Route
            path="/meetings"
            element={
              <PlaceholderPage
                title="Meeting minutes"
                description="The searchable meeting-minutes library arrives in P3-06."
              />
            }
          />
          <Route
            path="/meetings/:jobId"
            element={
              <PlaceholderPage
                title="Meeting detail"
                description="Deep-linked meeting details arrive in P3-06. This URL is already reserved and refresh-safe."
              />
            }
          />
          <Route
            path="/actions"
            element={
              <PlaceholderPage
                title="Actions"
                description="Independent action tracking arrives in P3-07."
              />
            }
          />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </Box>
    </ThemeProvider>
  )
}

function PlaceholderPage({
  title,
  description,
}: {
  title: string
  description: string
}) {
  return (
    <Container component="main" maxWidth="lg" className="route-page">
      <Box className="surface placeholder-page">
        <Typography component="h1" variant="h4" fontWeight={800}>
          {title}
        </Typography>
        <Typography color="text.secondary">{description}</Typography>
        <Button component={Link} to="/" variant="outlined">
          Return to dashboard
        </Button>
      </Box>
    </Container>
  )
}

function NotFoundPage() {
  return (
    <Container component="main" maxWidth="md" className="route-page">
      <Box className="surface placeholder-page">
        <Typography component="h1" variant="h4" fontWeight={800}>
          Page not found
        </Typography>
        <Typography color="text.secondary">
          The address may be incorrect or the page may have moved.
        </Typography>
        <Button component={Link} to="/">
          Go to dashboard
        </Button>
      </Box>
    </Container>
  )
}
