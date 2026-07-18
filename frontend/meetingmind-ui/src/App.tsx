import CloudUploadOutlinedIcon from '@mui/icons-material/CloudUploadOutlined'
import DownloadOutlinedIcon from '@mui/icons-material/DownloadOutlined'
import ExpandMoreOutlinedIcon from '@mui/icons-material/ExpandMoreOutlined'
import HistoryOutlinedIcon from '@mui/icons-material/HistoryOutlined'
import RefreshOutlinedIcon from '@mui/icons-material/RefreshOutlined'
import ReplayOutlinedIcon from '@mui/icons-material/ReplayOutlined'
import {
  Alert,
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Box,
  Button,
  Chip,
  Container,
  CssBaseline,
  Divider,
  LinearProgress,
  Stack,
  Tab,
  Tabs,
  ThemeProvider,
  Typography,
  createTheme,
} from '@mui/material'
import axios from 'axios'
import { type ChangeEvent, useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'

type UploadResponse = {
  jobId: string
  status: string
  stage: string
}

type JobStatusResponse = {
  jobId: string
  status: string
  stage: string
  progress: number
  errorMessage: string | null
  processingDurationSeconds: number
  totalDurationSeconds: number
}

type HistoryItem = {
  jobId: string
  originalFileName: string
  status: string
  stage: string
  progress: number
  errorMessage: string | null
  createdAt: string
  updatedAt: string
  startedAt: string | null
  completedAt: string | null
  processingDurationSeconds: number
  totalDurationSeconds: number
}

type HistoryResponse = {
  skip: number
  take: number
  total: number
  items: HistoryItem[]
}

type ActionItem = {
  description: string
  owner: string | null
  dueDate: string | null
}

type MinutesResult = {
  jobId: string
  title: string
  summary: string
  attendees: string[]
  discussionPoints: string[]
  decisions: string[]
  actionItems: ActionItem[]
  risks: string[]
  nextSteps: string[]
}

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#285f8f',
    },
    secondary: {
      main: '#2f7d59',
    },
    background: {
      default: '#f6f7f9',
    },
    warning: {
      main: '#a86620',
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

const activeStatuses = new Set(['Queued', 'Processing'])
const retryableStatuses = new Set(['Failed', 'Cancelled'])

function App() {
  const [history, setHistory] = useState<HistoryItem[]>([])
  const [historyTotal, setHistoryTotal] = useState(0)
  const [selectedJob, setSelectedJob] = useState<JobStatusResponse | null>(null)
  const [selectedHistoryItem, setSelectedHistoryItem] = useState<HistoryItem | null>(null)
  const [minutes, setMinutes] = useState<MinutesResult | null>(null)
  const [transcript, setTranscript] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState(0)
  const [isUploading, setIsUploading] = useState(false)
  const [isHistoryLoading, setIsHistoryLoading] = useState(false)
  const [isResultLoading, setIsResultLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [liveDurationOffsetSeconds, setLiveDurationOffsetSeconds] = useState(0)

  const selectedJobId = selectedJob?.jobId ?? selectedHistoryItem?.jobId ?? null
  const canRetry = selectedJob ? retryableStatuses.has(selectedJob.status) : false
  const isPolling = selectedJob ? activeStatuses.has(selectedJob.status) : false
  const selectedProcessingDurationSeconds = selectedJob
    ? selectedJob.processingDurationSeconds +
      (selectedJob.status === 'Processing' ? liveDurationOffsetSeconds : 0)
    : 0
  const selectedTotalDurationSeconds = selectedJob
    ? selectedJob.totalDurationSeconds + (isPolling ? liveDurationOffsetSeconds : 0)
    : 0

  const selectedFileName = useMemo(() => {
    if (!selectedJobId) {
      return null
    }

    return (
      history.find((item) => item.jobId === selectedJobId)?.originalFileName ??
      selectedHistoryItem?.originalFileName ??
      'Uploaded meeting'
    )
  }, [history, selectedHistoryItem?.originalFileName, selectedJobId])

  const loadHistory = useCallback(async () => {
    setIsHistoryLoading(true)
    setError(null)

    try {
      const response = await axios.get<HistoryResponse>('/api/meetings/history', {
        params: { skip: 0, take: 50 },
      })

      setHistory(response.data.items)
      setHistoryTotal(response.data.total)
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, 'History could not be loaded.'))
    } finally {
      setIsHistoryLoading(false)
    }
  }, [])

  const loadResults = useCallback(async (jobId: string) => {
    setIsResultLoading(true)

    try {
      const [minutesResponse, transcriptResponse] = await Promise.allSettled([
        axios.get<MinutesResult>(`/api/meetings/${jobId}/result`),
        axios.get<string>(`/api/meetings/${jobId}/transcript/download`, {
          responseType: 'text',
        }),
      ])

      setMinutes(minutesResponse.status === 'fulfilled' ? minutesResponse.value.data : null)
      setTranscript(
        transcriptResponse.status === 'fulfilled' ? transcriptResponse.value.data : null,
      )
    } finally {
      setIsResultLoading(false)
    }
  }, [])

  const loadStatus = useCallback(
    async (jobId: string) => {
      const response = await axios.get<JobStatusResponse>(`/api/meetings/${jobId}/status`)
      setSelectedJob(response.data)
      setLiveDurationOffsetSeconds(0)

      if (response.data.status === 'Completed') {
        await loadResults(jobId)
      }

      return response.data
    },
    [loadResults],
  )

  async function selectHistoryJob(item: HistoryItem) {
    setSelectedHistoryItem(item)
    setSelectedJob({
      jobId: item.jobId,
      status: item.status,
      stage: item.stage,
      progress: item.progress,
      errorMessage: item.errorMessage,
      processingDurationSeconds: item.processingDurationSeconds,
      totalDurationSeconds: item.totalDurationSeconds,
    })
    setLiveDurationOffsetSeconds(0)
    setMinutes(null)
    setTranscript(null)
    setMessage(null)
    setError(null)
    await loadStatus(item.jobId)
  }

  async function handleUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''

    if (!file) {
      return
    }

    const formData = new FormData()
    formData.append('file', file)
    setIsUploading(true)
    setMinutes(null)
    setTranscript(null)
    setMessage(null)
    setError(null)

    try {
      const response = await axios.post<UploadResponse>('/api/meetings/upload', formData)
      const job = response.data
      setSelectedHistoryItem({
        jobId: job.jobId,
        originalFileName: file.name,
        status: job.status,
        stage: job.stage,
        progress: 0,
        errorMessage: null,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        startedAt: null,
        completedAt: null,
        processingDurationSeconds: 0,
        totalDurationSeconds: 0,
      })
      setSelectedJob({
        jobId: job.jobId,
        status: job.status,
        stage: job.stage,
        progress: 0,
        errorMessage: null,
        processingDurationSeconds: 0,
        totalDurationSeconds: 0,
      })
      setLiveDurationOffsetSeconds(0)
      setMessage('Upload accepted. Processing has started.')
      await loadHistory()
      await loadStatus(job.jobId)
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, 'Upload failed.'))
    } finally {
      setIsUploading(false)
    }
  }

  async function retryJob(jobId: string) {
    setMessage(null)
    setError(null)

    try {
      const response = await axios.post<UploadResponse>(`/api/meetings/${jobId}/retry`)
      const previousJob =
        selectedJob?.jobId === jobId
          ? selectedJob
          : history.find((item) => item.jobId === jobId)
      setSelectedJob({
        jobId: response.data.jobId,
        status: response.data.status,
        stage: response.data.stage,
        progress: 0,
        errorMessage: null,
        processingDurationSeconds: previousJob?.processingDurationSeconds ?? 0,
        totalDurationSeconds: previousJob?.totalDurationSeconds ?? 0,
      })
      setLiveDurationOffsetSeconds(0)
      setMinutes(null)
      setTranscript(null)
      setMessage('Retry queued. Processing has restarted.')
      await loadHistory()
      await loadStatus(jobId)
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, 'Retry failed.'))
    }
  }

  useEffect(() => {
    const timerId = window.setTimeout(() => {
      void loadHistory()
    }, 0)

    return () => window.clearTimeout(timerId)
  }, [loadHistory])

  useEffect(() => {
    if (!selectedJobId || !isPolling) {
      return
    }

    const timerId = window.setInterval(() => {
      void loadStatus(selectedJobId)
    }, 5000)

    return () => window.clearInterval(timerId)
  }, [isPolling, loadStatus, selectedJobId])

  useEffect(() => {
    if (!selectedJobId || !isPolling) {
      return
    }

    const timerId = window.setInterval(() => {
      setLiveDurationOffsetSeconds((current) => current + 1)
    }, 1000)

    return () => window.clearInterval(timerId)
  }, [isPolling, selectedJobId])

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box className="app-shell">
        <Container maxWidth="xl">
          <Stack spacing={3}>
            <Stack
              className="top-bar"
              direction={{ xs: 'column', md: 'row' }}
              justifyContent="space-between"
              spacing={2}
            >
              <Box>
                <Typography variant="h4" component="h1" fontWeight={700}>
                  MeetingMind AI
                </Typography>
                <Typography color="text.secondary">
                  Convert meeting recordings into transcript, decisions, and action items.
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} flexWrap="wrap">
                <Button
                  variant="outlined"
                  startIcon={<RefreshOutlinedIcon />}
                  onClick={() => void loadHistory()}
                  disabled={isHistoryLoading}
                >
                  Refresh
                </Button>
                <Button
                  component="label"
                  variant="contained"
                  startIcon={<CloudUploadOutlinedIcon />}
                  disabled={isUploading}
                >
                  {isUploading ? 'Uploading' : 'Upload'}
                  <input
                    hidden
                    type="file"
                    accept=".mp3,.wav,.m4a,.aac,audio/mpeg,audio/wav,audio/mp4,audio/aac"
                    onChange={(event) => void handleUpload(event)}
                  />
                </Button>
              </Stack>
            </Stack>

            {message ? <Alert severity="success">{message}</Alert> : null}
            {error ? <Alert severity="error">{error}</Alert> : null}

            <Box className="workspace-grid">
              <Box className="surface history-panel">
                <Stack spacing={2}>
                  <Stack direction="row" justifyContent="space-between" alignItems="center">
                    <Stack direction="row" spacing={1} alignItems="center">
                      <HistoryOutlinedIcon color="primary" />
                      <Typography variant="h6" fontWeight={700}>
                        History
                      </Typography>
                    </Stack>
                    <Chip label={`${historyTotal} jobs`} size="small" variant="outlined" />
                  </Stack>

                  <Stack spacing={1.25}>
                    {history.length === 0 ? (
                      <Typography color="text.secondary">
                        No meeting jobs have been created yet.
                      </Typography>
                    ) : (
                      history.map((item) => (
                        <Box
                          className={
                            item.jobId === selectedJobId ? 'history-row selected' : 'history-row'
                          }
                          key={item.jobId}
                        >
                          <button
                            className="history-select"
                            type="button"
                            onClick={() => void selectHistoryJob(item)}
                          >
                            <Stack spacing={1}>
                              <Stack
                                direction="row"
                                justifyContent="space-between"
                                alignItems="center"
                                spacing={1}
                              >
                                <Typography fontWeight={700} noWrap>
                                  {item.originalFileName}
                                </Typography>
                                <StatusChip status={item.status} />
                              </Stack>
                              <LinearProgress
                                variant="determinate"
                                value={item.progress}
                                sx={{ height: 7, borderRadius: 4 }}
                              />
                              <Stack direction="row" justifyContent="space-between" spacing={1}>
                                <Typography variant="body2" color="text.secondary">
                                  {item.stage}
                                </Typography>
                                <Typography variant="body2" color="text.secondary">
                                  {formatDate(item.createdAt)}
                                </Typography>
                              </Stack>
                              <Typography variant="body2" color="text.secondary">
                                {formatDuration(item.processingDurationSeconds)} processing ·{' '}
                                {formatDuration(item.totalDurationSeconds)} total
                              </Typography>
                            </Stack>
                          </button>

                          {retryableStatuses.has(item.status) ? (
                            <Button
                              size="small"
                              startIcon={<ReplayOutlinedIcon />}
                              onClick={() => void retryJob(item.jobId)}
                            >
                              Retry
                            </Button>
                          ) : null}
                        </Box>
                      ))
                    )}
                  </Stack>
                </Stack>
              </Box>

              <Box className="detail-stack">
                <UploadPanel isUploading={isUploading} onUpload={handleUpload} />
                <Box className="surface">
                  {selectedJob ? (
                    <Stack spacing={2.5}>
                      <Stack
                        direction={{ xs: 'column', md: 'row' }}
                        justifyContent="space-between"
                        spacing={2}
                      >
                        <Box>
                          <Typography variant="h6" fontWeight={700}>
                            {selectedFileName}
                          </Typography>
                          <Typography className="job-id" color="text.secondary">
                            {selectedJob.jobId}
                          </Typography>
                        </Box>
                        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap">
                          <StatusChip status={selectedJob.status} />
                          {canRetry ? (
                            <Button
                              size="small"
                              variant="outlined"
                              startIcon={<ReplayOutlinedIcon />}
                              onClick={() => void retryJob(selectedJob.jobId)}
                            >
                              Retry
                            </Button>
                          ) : null}
                        </Stack>
                      </Stack>

                      <Box>
                        <Stack direction="row" justifyContent="space-between" spacing={2}>
                          <Typography fontWeight={700}>{selectedJob.stage}</Typography>
                          <Typography fontWeight={700}>{selectedJob.progress}%</Typography>
                        </Stack>
                        <LinearProgress
                          variant="determinate"
                          value={selectedJob.progress}
                          sx={{ height: 9, borderRadius: 5, mt: 1 }}
                        />
                      </Box>

                      {selectedJob.errorMessage ? (
                        <Alert severity="error">{selectedJob.errorMessage}</Alert>
                      ) : null}

                      <Accordion className="processing-details" disableGutters elevation={0}>
                        <AccordionSummary
                          expandIcon={<ExpandMoreOutlinedIcon />}
                          aria-controls="processing-details-content"
                          id="processing-details-header"
                        >
                          <Box>
                            <Typography fontWeight={700}>Processing details</Typography>
                            <Typography variant="body2" color="text.secondary">
                              Processing and total elapsed time with lifecycle dates
                            </Typography>
                          </Box>
                        </AccordionSummary>
                        <AccordionDetails id="processing-details-content">
                          <Stack className="metadata-grid">
                            <Metadata
                              label="Processing duration"
                              value={formatDuration(selectedProcessingDurationSeconds)}
                            />
                            <Metadata
                              label="Total duration"
                              value={formatDuration(selectedTotalDurationSeconds)}
                            />
                            <Metadata label="Created" value={formatDate(selectedHistoryItem?.createdAt)} />
                            <Metadata label="Started" value={formatDate(selectedHistoryItem?.startedAt)} />
                            <Metadata label="Completed" value={formatDate(selectedHistoryItem?.completedAt)} />
                            <Metadata label="Updated" value={formatDate(selectedHistoryItem?.updatedAt)} />
                          </Stack>
                        </AccordionDetails>
                      </Accordion>

                      <Divider />

                      <Stack direction="row" spacing={1} flexWrap="wrap">
                        <Button
                          component="a"
                          href={`/api/meetings/${selectedJob.jobId}/transcript/download`}
                          variant="outlined"
                          startIcon={<DownloadOutlinedIcon />}
                        >
                          Transcript
                        </Button>
                        <Button
                          component="a"
                          href={`/api/meetings/${selectedJob.jobId}/minutes/download`}
                          variant="outlined"
                          startIcon={<DownloadOutlinedIcon />}
                        >
                          Minutes
                        </Button>
                      </Stack>

                      <Tabs
                        value={activeTab}
                        onChange={(_, nextValue: number) => setActiveTab(nextValue)}
                      >
                        <Tab label="Minutes" />
                        <Tab label="Transcript" />
                      </Tabs>

                      {activeTab === 0 ? (
                        <MinutesPanel
                          isLoading={isResultLoading}
                          minutes={minutes}
                          status={selectedJob.status}
                        />
                      ) : (
                        <TranscriptPanel
                          isLoading={isResultLoading}
                          transcript={transcript}
                          status={selectedJob.status}
                        />
                      )}
                    </Stack>
                  ) : (
                    <Stack spacing={1}>
                      <Typography variant="h6" fontWeight={700}>
                        No job selected
                      </Typography>
                      <Typography color="text.secondary">
                        Upload a recording or choose a job from history.
                      </Typography>
                    </Stack>
                  )}
                </Box>
              </Box>
            </Box>
          </Stack>
        </Container>
      </Box>
    </ThemeProvider>
  )
}

function UploadPanel({
  isUploading,
  onUpload,
}: {
  isUploading: boolean
  onUpload: (event: ChangeEvent<HTMLInputElement>) => void
}) {
  return (
    <Box className="upload-panel">
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'stretch', md: 'center' }}
        spacing={2}
      >
        <Stack direction="row" spacing={2} alignItems="center">
          <Box className="upload-icon">
            <CloudUploadOutlinedIcon color="primary" />
          </Box>
          <Box>
            <Typography fontWeight={700}>Upload meeting audio</Typography>
            <Typography color="text.secondary">MP3, WAV, M4A, AAC</Typography>
          </Box>
        </Stack>
        <Button
          component="label"
          variant="contained"
          startIcon={<CloudUploadOutlinedIcon />}
          disabled={isUploading}
        >
          {isUploading ? 'Uploading' : 'Select file'}
          <input
            hidden
            type="file"
            accept=".mp3,.wav,.m4a,.aac,audio/mpeg,audio/wav,audio/mp4,audio/aac"
            onChange={(event) => onUpload(event)}
          />
        </Button>
      </Stack>
    </Box>
  )
}

function MinutesPanel({
  isLoading,
  minutes,
  status,
}: {
  isLoading: boolean
  minutes: MinutesResult | null
  status: string
}) {
  if (isLoading) {
    return <Typography color="text.secondary">Loading minutes...</Typography>
  }

  if (!minutes) {
    return (
      <Typography color="text.secondary">
        {status === 'Completed'
          ? 'Minutes are not available for this job.'
          : 'Minutes will appear when processing is completed.'}
      </Typography>
    )
  }

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h6" fontWeight={700}>
          {minutes.title}
        </Typography>
        <Typography color="text.secondary">{minutes.summary}</Typography>
      </Box>
      <ResultSection title="Attendees" items={minutes.attendees} />
      <ResultSection title="Discussion Points" items={minutes.discussionPoints} />
      <ResultSection title="Decisions" items={minutes.decisions} />
      <ActionItemsSection items={minutes.actionItems} />
      <ResultSection title="Risks / Blockers" items={minutes.risks} />
      <ResultSection title="Next Steps" items={minutes.nextSteps} />
    </Stack>
  )
}

function TranscriptPanel({
  isLoading,
  transcript,
  status,
}: {
  isLoading: boolean
  transcript: string | null
  status: string
}) {
  if (isLoading) {
    return <Typography color="text.secondary">Loading transcript...</Typography>
  }

  if (!transcript) {
    return (
      <Typography color="text.secondary">
        {status === 'Completed'
          ? 'Transcript is not available for this job.'
          : 'Transcript will appear when processing is completed.'}
      </Typography>
    )
  }

  return <Box className="transcript-box">{transcript}</Box>
}

function ResultSection({ title, items }: { title: string; items: string[] }) {
  return (
    <Box className="result-section">
      <Typography fontWeight={700}>{title}</Typography>
      {items.length === 0 ? (
        <Typography color="text.secondary">None identified</Typography>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
    </Box>
  )
}

function ActionItemsSection({ items }: { items: ActionItem[] }) {
  return (
    <Box className="result-section">
      <Typography fontWeight={700}>Action Items</Typography>
      {items.length === 0 ? (
        <Typography color="text.secondary">None identified</Typography>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={`${item.description}-${item.owner ?? ''}-${item.dueDate ?? ''}`}>
              {item.description}
              {item.owner ? ` - ${item.owner}` : ''}
              {item.dueDate ? ` (${item.dueDate})` : ''}
            </li>
          ))}
        </ul>
      )}
    </Box>
  )
}

function Metadata({ label, value }: { label: string; value: string }) {
  return (
    <Box className="metadata-item">
      <Typography variant="body2" color="text.secondary">
        {label}
      </Typography>
      <Typography fontWeight={700}>{value}</Typography>
    </Box>
  )
}

function StatusChip({ status }: { status: string }) {
  const color =
    status === 'Completed'
      ? 'success'
      : status === 'Failed' || status === 'Cancelled'
        ? 'error'
        : status === 'Processing'
          ? 'info'
          : 'warning'

  return <Chip label={status} size="small" color={color} />
}

function formatDate(value: string | null | undefined) {
  if (!value) {
    return '-'
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function formatDuration(totalSeconds: number) {
  const normalizedSeconds = Math.max(0, Math.floor(totalSeconds || 0))
  const hours = Math.floor(normalizedSeconds / 3600)
  const minutes = Math.floor((normalizedSeconds % 3600) / 60)
  const seconds = normalizedSeconds % 60

  if (hours > 0) {
    return `${hours}h ${minutes.toString().padStart(2, '0')}m ${seconds
      .toString()
      .padStart(2, '0')}s`
  }

  if (minutes > 0) {
    return `${minutes}m ${seconds.toString().padStart(2, '0')}s`
  }

  return `${seconds}s`
}

function getRequestErrorMessage(requestError: unknown, fallback: string) {
  if (axios.isAxiosError<{ error?: string }>(requestError)) {
    return requestError.response?.data?.error ?? requestError.message ?? fallback
  }

  return fallback
}

export default App
