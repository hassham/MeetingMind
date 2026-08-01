import ArrowForwardIcon from '@mui/icons-material/ArrowForward'
import RefreshIcon from '@mui/icons-material/Refresh'
import { Alert, Box, Button, Chip, CircularProgress, Container, Stack, Typography } from '@mui/material'
import axios from 'axios'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'

type MinutesItem = {
  jobId: string
  title: string
  originalFileName: string
  sourceType: string
  processingMode: string
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  minutesCreatedAt: string
}

type MinutesPage = { skip: number; take: number; total: number; items: MinutesItem[] }
const pageSize = 20

export default function MinutesLibraryPage() {
  const [page, setPage] = useState(0)
  const [result, setResult] = useState<MinutesPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(false)
  const headingRef = useRef<HTMLHeadingElement>(null)

  const load = useCallback(async () => {
    setLoading(true); setError(false)
    try {
      const response = await axios.get<MinutesPage>('/api/meetings/minutes', { params: { skip: page * pageSize, take: pageSize } })
      setResult(response.data)
      if (response.data.items.length === 0 && response.data.total > 0 && page > 0) setPage(Math.max(0, Math.ceil(response.data.total / pageSize) - 1))
    } catch { setError(true) } finally { setLoading(false) }
  }, [page])

  useEffect(() => { const id = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(id) }, [load])
  const pages = Math.max(1, Math.ceil((result?.total ?? 0) / pageSize))

  return <Container component="main" maxWidth="lg" className="route-page">
    <Stack spacing={3}>
      <Box><Typography ref={headingRef} tabIndex={-1} component="h1" variant="h4" fontWeight={800}>Meeting minutes</Typography><Typography color="text.secondary">Completed records with generated minutes, newest first.</Typography></Box>
      {loading ? <Box role="status"><CircularProgress size={28} /><span className="visually-hidden" aria-live="polite">Loading meeting minutes</span></Box> : null}
      {error ? <Alert tabIndex={-1} severity="error" action={<Button color="inherit" startIcon={<RefreshIcon />} onClick={() => void load()}>Retry</Button>}>Meeting minutes could not be loaded.</Alert> : null}
      {!loading && !error && result?.total === 0 ? <Box className="surface empty-dashboard"><Typography component="h2" variant="h5">No meeting minutes yet</Typography><Typography color="text.secondary">Completed transcript-only jobs stay in All Processing. Create a full meeting or upload a transcript to generate minutes.</Typography><Button component={Link} to="/process/new">New processing job</Button></Box> : null}
      {!loading && !error && result && result.items.length > 0 ? <>
        <Stack spacing={1.5}>{result.items.map(item => <Box className="surface minutes-row" key={item.jobId}>
          <Box><Typography component="h2" variant="h6" fontWeight={750}>{item.title}</Typography><Typography color="text.secondary">{item.originalFileName} · {formatMode(item.processingMode)} · completed {formatDate(item.completedAt ?? item.minutesCreatedAt)}</Typography><Stack direction="row" gap={1} mt={1}><Chip size="small" label={item.sourceType} /><Chip size="small" variant="outlined" label={formatMode(item.processingMode)} /></Stack></Box>
          <Button component={Link} to={`/meetings/${item.jobId}`} endIcon={<ArrowForwardIcon />}>Open meeting</Button>
        </Box>)}</Stack>
        <Stack className="pagination-bar" direction="row" justifyContent="space-between" alignItems="center"><Button disabled={page===0} onClick={()=>{setPage(p=>p-1);headingRef.current?.focus()}}>Previous</Button><Typography>Page {page+1} of {pages} · {result.total} meetings</Typography><Button disabled={page+1>=pages} onClick={()=>{setPage(p=>p+1);headingRef.current?.focus()}}>Next</Button></Stack>
      </> : null}
    </Stack>
  </Container>
}

function formatDate(value: string) { return new Intl.DateTimeFormat(undefined,{dateStyle:'medium',timeStyle:'short'}).format(new Date(value)) }
function formatMode(value: string) { return value === 'FullMeeting' ? 'Full meeting' : value === 'MinutesFromTranscript' ? 'Minutes from transcript' : 'Transcript only' }
