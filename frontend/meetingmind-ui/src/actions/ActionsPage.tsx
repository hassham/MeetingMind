import AddIcon from '@mui/icons-material/Add'
import DownloadIcon from '@mui/icons-material/Download'
import { Alert, Box, Button, Card, CardActions, CardContent, Chip, Container, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Pagination, Select, Stack, TextField, Typography } from '@mui/material'
import axios from 'axios'
import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'

type Status = 'Open' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled'
type ActionItem = { id: string; description: string; assignee: string | null; notes: string | null; dueDate: string | null; status: Status; source: 'Generated' | 'Manual'; meetingId: string | null; meetingTitle: string | null; sourceFileName: string | null; version: string; isOverdue: boolean }
type ActionPage = { items: ActionItem[]; skip: number; take: number; total: number }
type Draft = { description: string; assignee: string; notes: string; dueDate: string; status: Status; meetingId: string; version: string }
const blank: Draft = { description: '', assignee: '', notes: '', dueDate: '', status: 'Open', meetingId: '', version: '' }

export default function ActionsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const meetingId = searchParams.get('meetingId') ?? ''
  const [data, setData] = useState<ActionPage | null>(null)
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState('')
  const [assignee, setAssignee] = useState('')
  const [due, setDue] = useState('')
  const [source, setSource] = useState('')
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<ActionItem | null | 'new'>(null)
  const [draft, setDraft] = useState<Draft>(blank)
  const [deleting, setDeleting] = useState<ActionItem | null>(null)

  const load = useCallback(async () => {
    try {
      const response = await axios.get<ActionPage>('/api/actions', { params: { skip: (page - 1) * 25, take: 25, status: status || undefined, assignee: assignee || undefined, due: due || undefined, source: source || undefined, meetingId: meetingId || undefined } })
      setData(response.data); setError('')
    } catch { setError('Actions could not be loaded.') }
  }, [page, status, assignee, due, source, meetingId])
  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  const open = (item?: ActionItem) => { setEditing(item ?? 'new'); setDraft(item ? { description: item.description, assignee: item.assignee ?? '', notes: item.notes ?? '', dueDate: item.dueDate ?? '', status: item.status, meetingId: item.meetingId ?? '', version: item.version } : blank); setError('') }
  const save = async () => {
    const payload = { ...draft, assignee: draft.assignee || null, notes: draft.notes || null, dueDate: draft.dueDate || null, meetingId: draft.meetingId || null }
    try {
      if (editing === 'new') await axios.post('/api/actions', payload)
      else await axios.patch(`/api/actions/${editing?.id}`, payload)
      setEditing(null); await load()
    } catch (reason) {
      if (axios.isAxiosError(reason) && reason.response?.status === 409) setError('This action changed elsewhere. Your draft is preserved; close and reopen it to load the latest version.')
      else setError('The action could not be saved. Check the fields and meeting ID.')
    }
  }
  const remove = async () => { if (!deleting) return; try { await axios.delete(`/api/actions/${deleting.id}`); setDeleting(null); await load() } catch { setError('The action could not be deleted.') } }
  const exportActions = (format: 'csv' | 'json') => { const params = new URLSearchParams({ format }); if (status) params.set('status', status); if (assignee) params.set('assignee', assignee); if (due) params.set('due', due); if (source) params.set('source', source); if (meetingId) params.set('meetingId', meetingId); window.location.assign(`/api/actions/export?${params}`) }

  return <Container component="main" maxWidth="xl" className="route-page"><Stack spacing={3}>
    <Stack direction={{ xs: 'column', sm: 'row' }} justifyContent="space-between" gap={2}><Box><Typography component="h1" variant="h4" fontWeight={800}>Actions</Typography><Typography color="text.secondary">Track generated and manual work independently from meetings.</Typography></Box><Stack direction="row" gap={1}><Button startIcon={<DownloadIcon />} onClick={() => exportActions('csv')}>CSV</Button><Button startIcon={<DownloadIcon />} onClick={() => exportActions('json')}>JSON</Button><Button variant="contained" startIcon={<AddIcon />} onClick={() => open()}>New action</Button></Stack></Stack>
    {error && !editing ? <Alert severity="error">{error}</Alert> : null}
    {meetingId ? <Alert severity="info" action={<Button color="inherit" onClick={() => { setSearchParams({}); setPage(1) }}>Show all actions</Button>}>Showing actions linked to meeting {meetingId}.</Alert> : null}
    <Stack direction={{ xs: 'column', md: 'row' }} gap={2} className="surface" p={2}>
      <FormControl size="small" sx={{ minWidth: 150 }}><InputLabel>Status</InputLabel><Select label="Status" value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><MenuItem value="">All</MenuItem>{statuses.map(x => <MenuItem key={x} value={x}>{label(x)}</MenuItem>)}</Select></FormControl>
      <TextField size="small" label="Assignee contains" value={assignee} onChange={e => { setAssignee(e.target.value); setPage(1) }} />
      <FormControl size="small" sx={{ minWidth: 150 }}><InputLabel>Due</InputLabel><Select label="Due" value={due} onChange={e => { setDue(e.target.value); setPage(1) }}><MenuItem value="">Any</MenuItem><MenuItem value="overdue">Overdue</MenuItem><MenuItem value="due">Has due date</MenuItem><MenuItem value="none">No due date</MenuItem></Select></FormControl>
      <FormControl size="small" sx={{ minWidth: 150 }}><InputLabel>Source</InputLabel><Select label="Source" value={source} onChange={e => { setSource(e.target.value); setPage(1) }}><MenuItem value="">All</MenuItem><MenuItem value="Generated">Generated</MenuItem><MenuItem value="Manual">Manual</MenuItem></Select></FormControl>
    </Stack>
    {data?.items.length === 0 ? <Box className="surface" p={4}><Typography variant="h6">No actions match these filters</Typography><Button onClick={() => { setStatus(''); setAssignee(''); setDue(''); setSource('') }}>Clear filters</Button></Box> : null}
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, 1fr)' }, gap: 2 }}>{data?.items.map(item => <Card key={item.id} variant="outlined"><CardContent><Stack direction="row" gap={1} flexWrap="wrap"><Chip size="small" label={label(item.status)} color={item.isOverdue ? 'error' : 'default'} /><Chip size="small" label={item.source} variant="outlined" /></Stack><Typography variant="h6" mt={1}>{item.description}</Typography><Typography color="text.secondary">{item.assignee || 'Unassigned'} · {item.dueDate ? `Due ${item.dueDate}` : 'No due date'}</Typography>{item.meetingId ? <Button component={Link} to={`/meetings/${item.meetingId}`} size="small">{item.meetingTitle || item.sourceFileName || 'Linked meeting'}</Button> : item.meetingTitle ? <Typography variant="body2">Former meeting: {item.meetingTitle}</Typography> : null}</CardContent><CardActions><Button onClick={() => open(item)}>Edit</Button><Button color="error" onClick={() => setDeleting(item)}>Delete</Button></CardActions></Card>)}</Box>
    {data && data.total > 25 ? <Pagination page={page} count={Math.ceil(data.total / 25)} onChange={(_, value) => setPage(value)} /> : null}
    <ActionDialog open={editing !== null} draft={draft} setDraft={setDraft} error={editing ? error : ''} onClose={() => { setEditing(null); setError('') }} onSave={() => void save()} isNew={editing === 'new'} />
    <Dialog open={!!deleting} onClose={() => setDeleting(null)}><DialogTitle>Delete action?</DialogTitle><DialogContent><Typography>This permanently deletes “{deleting?.description}”. The linked meeting and its minutes will not change.</Typography></DialogContent><DialogActions><Button autoFocus onClick={() => setDeleting(null)}>Cancel</Button><Button color="error" onClick={() => void remove()}>Delete</Button></DialogActions></Dialog>
  </Stack></Container>
}

const statuses: Status[] = ['Open', 'InProgress', 'Blocked', 'Completed', 'Cancelled']
const label = (value: string) => value === 'InProgress' ? 'In progress' : value
function ActionDialog({ open, draft, setDraft, error, onClose, onSave, isNew }: { open: boolean; draft: Draft; setDraft: (d: Draft) => void; error: string; onClose: () => void; onSave: () => void; isNew: boolean }) {
  const change = (field: keyof Draft, value: string) => setDraft({ ...draft, [field]: value })
  return <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm"><DialogTitle>{isNew ? 'Create action' : 'Edit action'}</DialogTitle><DialogContent><Stack spacing={2} pt={1}>{error ? <Alert severity="warning">{error}</Alert> : null}<TextField required autoFocus label="Description" value={draft.description} inputProps={{ maxLength: 2000 }} onChange={e => change('description', e.target.value)} multiline /><TextField label="Assignee" value={draft.assignee} inputProps={{ maxLength: 200 }} onChange={e => change('assignee', e.target.value)} /><TextField label="Due date" type="date" value={draft.dueDate} onChange={e => change('dueDate', e.target.value)} InputLabelProps={{ shrink: true }} /><FormControl><InputLabel>Status</InputLabel><Select label="Status" value={draft.status} onChange={e => change('status', e.target.value)}>{statuses.map(x => <MenuItem key={x} value={x}>{label(x)}</MenuItem>)}</Select></FormControl><TextField label="Meeting ID (optional)" value={draft.meetingId} onChange={e => change('meetingId', e.target.value)} /><TextField label="Notes" value={draft.notes} inputProps={{ maxLength: 10000 }} onChange={e => change('notes', e.target.value)} multiline minRows={3} /></Stack></DialogContent><DialogActions><Button onClick={onClose}>Cancel</Button><Button variant="contained" disabled={!draft.description.trim()} onClick={onSave}>Save</Button></DialogActions></Dialog>
}
