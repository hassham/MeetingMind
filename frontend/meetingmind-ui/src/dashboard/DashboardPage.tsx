import ArrowForwardIcon from '@mui/icons-material/ArrowForward'
import RefreshIcon from '@mui/icons-material/Refresh'
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Container,
  Grid,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material'
import axios from 'axios'
import { Children, useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'

type RecentJob = {
  jobId: string
  originalFileName: string
  processingMode: string
  status: string
  stage: string
  progress: number
  createdAt: string
  updatedAt: string
}

type RecentMinutes = {
  jobId: string
  title: string
  originalFileName: string
  processingMode: string
  generatedAt: string
}

type DashboardSummary = {
  timeBasis: string
  totalJobs: number
  jobsByMode: {
    transcriptOnly: number
    fullMeeting: number
    minutesFromTranscript: number
  }
  jobsByStatus: {
    completed: number
    failed: number
    cancelled: number
    active: number
    queued: number
    processing: number
  }
  successRatePercent: number | null
  totalAudioDurationSeconds: number | null
  averageCompletedProcessingDurationSeconds: number | null
  transcriptCount: number
  minutesCount: number
  actions: { open: number; inProgress: number; blocked: number; completed: number; cancelled: number; overdue: number }
  recentJobs: RecentJob[]
  recentMinutes: RecentMinutes[]
}

export default function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const alertRef = useRef<HTMLDivElement>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(false)
    try {
      const response = await axios.get<DashboardSummary>('/api/dashboard/summary')
      setSummary(response.data)
    } catch {
      setError(true)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    const timerId = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timerId)
  }, [load])

  useEffect(() => {
    if (error) alertRef.current?.focus()
  }, [error])

  return (
    <Container component="main" maxWidth="xl" className="route-page">
      <Stack spacing={3}>
        <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}>
          <Box>
            <Typography component="h1" variant="h4" fontWeight={800}>
              Dashboard
            </Typography>
            <Typography color="text.secondary">All-time activity across MeetingMind.</Typography>
          </Box>
          <Button component={Link} to="/process/new" variant="contained">
            New processing job
          </Button>
        </Stack>

        {loading ? <DashboardSkeleton /> : null}
        {error ? (
          <Alert
            ref={alertRef}
            tabIndex={-1}
            severity="error"
            action={
              <Button color="inherit" startIcon={<RefreshIcon />} onClick={() => void load()}>
                Retry
              </Button>
            }
          >
            Dashboard data could not be loaded. Navigation remains available.
          </Alert>
        ) : null}
        {!loading && !error && summary ? <DashboardContent summary={summary} /> : null}
      </Stack>
    </Container>
  )
}

function DashboardContent({ summary }: { summary: DashboardSummary }) {
  const totalActions = summary.actions.open + summary.actions.inProgress + summary.actions.blocked + summary.actions.completed + summary.actions.cancelled
  if (summary.totalJobs === 0 && totalActions === 0) {
    return (
      <Box className="surface empty-dashboard">
        <Typography component="h2" variant="h5" fontWeight={750}>
          Start your first meeting
        </Typography>
        <Typography color="text.secondary">
          Upload audio for a transcript or complete minutes, or turn an existing transcript into minutes.
        </Typography>
        <Button component={Link} to="/process/new" endIcon={<ArrowForwardIcon />}>
          Create a processing job
        </Button>
      </Box>
    )
  }

  const metrics = [
    ['Total jobs', String(summary.totalJobs), '/processing'],
    ['Completed', String(summary.jobsByStatus.completed), '/processing'],
    ['Active', String(summary.jobsByStatus.active), '/processing'],
    ['Failed', String(summary.jobsByStatus.failed), '/processing'],
    ['Cancelled', String(summary.jobsByStatus.cancelled), '/processing'],
    [
      'Success rate',
      summary.successRatePercent === null ? '—' : `${formatNumber(summary.successRatePercent)}%`,
      null,
    ],
    ['Audio processed', formatDuration(summary.totalAudioDurationSeconds), null],
    [
      'Average processing time',
      formatDuration(summary.averageCompletedProcessingDurationSeconds),
      null,
    ],
    ['Transcripts', String(summary.transcriptCount), '/processing'],
    ['Minutes', String(summary.minutesCount), '/meetings'],
    ['Open actions', String(summary.actions.open), '/actions'],
    ['Overdue actions', String(summary.actions.overdue), '/actions'],
  ] as const

  return (
    <Stack spacing={3}>
      <Box>
        <Typography component="h2" variant="h6" fontWeight={750} gutterBottom>
          Activity summary
        </Typography>
        <Grid container spacing={2}>
          {metrics.map(([label, value, destination]) => (
            <Grid key={label} size={{ xs: 6, sm: 4, md: 2.4 }}>
              <MetricCard label={label} value={value} destination={destination} />
            </Grid>
          ))}
        </Grid>
      </Box>

      <Box className="surface">
        <Typography component="h2" variant="h6" fontWeight={750}>
          Jobs by workflow
        </Typography>
        <Stack direction="row" flexWrap="wrap" gap={1} mt={2}>
          <Chip label={`Transcript only ${summary.jobsByMode.transcriptOnly}`} />
          <Chip label={`Transcript and minutes ${summary.jobsByMode.fullMeeting}`} />
          <Chip label={`Minutes from transcript ${summary.jobsByMode.minutesFromTranscript}`} />
        </Stack>
      </Box>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, lg: 6 }}>
          <RecentPanel title="Recent processing" empty="No processing jobs yet.">
            {summary.recentJobs.map((job) => (
              <Button
                component={Link}
                to="/processing"
                className="recent-row"
                key={job.jobId}
                endIcon={<ArrowForwardIcon />}
              >
                <span>
                  <strong>{job.originalFileName}</strong>
                  <small>{job.processingMode} · {job.status} · {formatDate(job.createdAt)}</small>
                </span>
              </Button>
            ))}
          </RecentPanel>
        </Grid>
        <Grid size={{ xs: 12, lg: 6 }}>
          <RecentPanel title="Recently generated minutes" empty="No meeting minutes yet.">
            {summary.recentMinutes.map((minutes) => (
              <Button
                component={Link}
                to={`/meetings/${minutes.jobId}`}
                className="recent-row"
                key={minutes.jobId}
                endIcon={<ArrowForwardIcon />}
              >
                <span>
                  <strong>{minutes.title}</strong>
                  <small>{minutes.originalFileName} · {formatDate(minutes.generatedAt)}</small>
                </span>
              </Button>
            ))}
          </RecentPanel>
        </Grid>
      </Grid>
    </Stack>
  )
}

function MetricCard({
  label,
  value,
  destination,
}: {
  label: string
  value: string
  destination: string | null
}) {
  const content = (
    <CardContent>
      <Typography color="text.secondary" variant="body2">{label}</Typography>
      <Typography component="p" variant="h5" fontWeight={800}>{value}</Typography>
    </CardContent>
  )
  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      {destination ? <CardActionArea component={Link} to={destination}>{content}</CardActionArea> : content}
    </Card>
  )
}

function RecentPanel({
  title,
  empty,
  children,
}: {
  title: string
  empty: string
  children: ReactNode
}) {
  return (
    <Box className="surface recent-panel">
      <Typography component="h2" variant="h6" fontWeight={750}>{title}</Typography>
      <Stack mt={1}>
        {Children.count(children) > 0 ? children : <Typography color="text.secondary">{empty}</Typography>}
      </Stack>
    </Box>
  )
}

function DashboardSkeleton() {
  return (
    <Box role="status" aria-label="Loading dashboard">
      <span className="visually-hidden" aria-live="polite">Loading dashboard</span>
      <Grid container spacing={2}>
        {Array.from({ length: 10 }, (_, index) => (
          <Grid key={index} size={{ xs: 6, sm: 4, md: 2.4 }}>
            <Skeleton variant="rounded" height={92} />
          </Grid>
        ))}
      </Grid>
    </Box>
  )
}

function formatDuration(seconds: number | null) {
  if (seconds === null) return '—'
  const rounded = Math.round(seconds)
  const hours = Math.floor(rounded / 3600)
  const minutes = Math.floor((rounded % 3600) / 60)
  const remainder = rounded % 60
  return hours > 0 ? `${hours}h ${minutes}m` : minutes > 0 ? `${minutes}m ${remainder}s` : `${remainder}s`
}

function formatNumber(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}
