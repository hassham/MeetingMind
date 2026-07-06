import CloudUploadOutlinedIcon from '@mui/icons-material/CloudUploadOutlined'
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined'
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined'
import RefreshOutlinedIcon from '@mui/icons-material/RefreshOutlined'
import {
  Box,
  Button,
  Chip,
  Container,
  CssBaseline,
  LinearProgress,
  Stack,
  ThemeProvider,
  Typography,
  createTheme,
} from '@mui/material'
import './App.css'

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#2364aa',
    },
    secondary: {
      main: '#2f855a',
    },
    background: {
      default: '#f7f8fa',
    },
  },
  shape: {
    borderRadius: 8,
  },
  typography: {
    fontFamily:
      'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  },
})

const recentJobs = [
  { title: 'Sprint planning', status: 'Completed', progress: 100 },
  { title: 'Vendor review', status: 'Processing', progress: 64 },
  { title: 'Risk workshop', status: 'Queued', progress: 0 },
]

function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box className="app-shell">
        <Container maxWidth="lg">
          <Stack spacing={3}>
            <Stack
              direction={{ xs: 'column', md: 'row' }}
              justifyContent="space-between"
              spacing={2}
            >
              <Box>
                <Typography variant="h4" component="h1" fontWeight={700}>
                  MeetingMind AI
                </Typography>
                <Typography color="text.secondary">
                  Meeting recordings into transcripts, decisions, and actions.
                </Typography>
              </Box>
              <Stack direction="row" spacing={1}>
                <Button variant="outlined" startIcon={<HistoryOutlinedIcon />}>
                  History
                </Button>
                <Button variant="contained" startIcon={<CloudUploadOutlinedIcon />}>
                  Upload
                </Button>
              </Stack>
            </Stack>

            <Box className="upload-panel">
              <Stack spacing={2} alignItems="center" textAlign="center">
                <CloudUploadOutlinedIcon color="primary" sx={{ fontSize: 44 }} />
                <Box>
                  <Typography variant="h6" fontWeight={700}>
                    Drop a meeting recording
                  </Typography>
                  <Typography color="text.secondary">
                    MP3, WAV, M4A, or AAC
                  </Typography>
                </Box>
                <Button variant="contained" startIcon={<CloudUploadOutlinedIcon />}>
                  Select audio
                </Button>
              </Stack>
            </Box>

            <Box className="content-grid">
              <Box className="surface">
                <Stack spacing={2}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Typography variant="h6" fontWeight={700}>
                      Processing
                    </Typography>
                    <Button size="small" startIcon={<RefreshOutlinedIcon />}>
                      Refresh
                    </Button>
                  </Stack>

                  {recentJobs.map((job) => (
                    <Box className="job-row" key={job.title}>
                      <Stack direction="row" justifyContent="space-between" spacing={2}>
                        <Typography fontWeight={600}>{job.title}</Typography>
                        <Chip label={job.status} size="small" color={job.status === 'Completed' ? 'success' : 'default'} />
                      </Stack>
                      <LinearProgress
                        variant="determinate"
                        value={job.progress}
                        sx={{ height: 8, borderRadius: 4 }}
                      />
                    </Box>
                  ))}
                </Stack>
              </Box>

              <Box className="surface">
                <Stack spacing={2}>
                  <Typography variant="h6" fontWeight={700}>
                    Latest minutes
                  </Typography>
                  <Typography color="text.secondary">
                    Executive summary, decisions, action items, risks, and next steps
                    will appear here after processing.
                  </Typography>
                  <Stack direction="row" spacing={1}>
                    <Button variant="outlined" startIcon={<DownloadOutlinedIcon />}>
                      Transcript
                    </Button>
                    <Button variant="outlined" startIcon={<DownloadOutlinedIcon />}>
                      Minutes
                    </Button>
                  </Stack>
                </Stack>
              </Box>
            </Box>
          </Stack>
        </Container>
      </Box>
    </ThemeProvider>
  )
}

export default App
