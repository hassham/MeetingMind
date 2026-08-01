import DownloadIcon from '@mui/icons-material/Download'
import { Alert, Box, Button, Chip, CircularProgress, Container, Stack, Typography } from '@mui/material'
import axios from 'axios'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'

type ActionItem = { description: string; owner: string | null; dueDate: string | null }
type Detail = { jobId:string; originalFileName:string; sourceType:string; processingMode:string; createdAt:string; startedAt:string|null; completedAt:string|null; hasTranscript:boolean; title:string; summary:string; attendees:string[]; discussionPoints:string[]; decisions:string[]; actionItems:ActionItem[]; risks:string[]; nextSteps:string[] }
type Transcript = { hasTimestamps:boolean; paragraphs:{text:string;startSeconds:number|null}[] }

export default function MeetingDetailPage() {
  const { jobId } = useParams()
  const [detail,setDetail]=useState<Detail|null>(null); const [transcript,setTranscript]=useState<Transcript|null>(null); const [loading,setLoading]=useState(true); const [notFound,setNotFound]=useState(false); const [error,setError]=useState(false)
  useEffect(()=>{ let cancelled=false; const id=window.setTimeout(async()=>{setLoading(true);setError(false);setNotFound(false);try{const response=await axios.get<Detail>(`/api/meetings/${jobId}/result`);if(cancelled)return;setDetail(response.data);if(response.data.hasTranscript){try{const tr=await axios.get<Transcript>(`/api/meetings/${jobId}/transcript`);if(!cancelled)setTranscript(tr.data)}catch{if(!cancelled)setTranscript(null)}}}catch(e){if(cancelled)return;if(axios.isAxiosError(e)&&e.response?.status===404)setNotFound(true);else setError(true)}finally{if(!cancelled)setLoading(false)}},0);return()=>{cancelled=true;window.clearTimeout(id)}},[jobId])
  if(loading)return <Container component="main" maxWidth="lg" className="route-page"><Box role="status"><CircularProgress/><span className="visually-hidden" aria-live="polite">Loading meeting detail</span></Box></Container>
  if(notFound)return <State title="Meeting minutes unavailable" text="This meeting does not exist or has no generated minutes." />
  if(error||!detail)return <State title="Meeting could not be loaded" text="Try returning to the minutes library and opening it again." />
  return <Container component="main" maxWidth="lg" className="route-page"><Stack spacing={3}>
    <Box><Typography component="h1" variant="h4" fontWeight={800}>{detail.title}</Typography><Stack direction="row" gap={1} flexWrap="wrap" my={1}><Chip label={detail.sourceType}/><Chip variant="outlined" label={formatMode(detail.processingMode)}/></Stack><Typography color="text.secondary">{detail.originalFileName} · created {formatDate(detail.createdAt)} · completed {formatDate(detail.completedAt)}</Typography></Box>
    <Stack direction={{xs:'column',sm:'row'}} gap={1}><Button component="a" href={`/api/meetings/${detail.jobId}/minutes/download`} startIcon={<DownloadIcon/>}>Download minutes</Button>{detail.hasTranscript?<Button component="a" href={`/api/meetings/${detail.jobId}/transcript/download`} startIcon={<DownloadIcon/>}>Download transcript</Button>:null}</Stack>
    <Box className="surface minutes-detail"><Section title="Summary"><Typography>{detail.summary||'No summary was generated.'}</Typography></Section><ListSection title="Attendees" items={detail.attendees}/><ListSection title="Discussion points" items={detail.discussionPoints}/><ListSection title="Decisions" items={detail.decisions}/><Section title="Generated action-items snapshot">{detail.actionItems.length?<Stack component="ul">{detail.actionItems.map((a,i)=><Typography component="li" key={`${a.description}-${i}`}>{a.description}{a.owner?` — ${a.owner}`:''}{a.dueDate?` (${a.dueDate})`:''}</Typography>)}</Stack>:<Typography color="text.secondary">No action items were generated.</Typography>}</Section><ListSection title="Risks" items={detail.risks}/><ListSection title="Next steps" items={detail.nextSteps}/></Box>
    <Box className="surface"><Typography component="h2" variant="h5" fontWeight={750}>Transcript</Typography>{!detail.hasTranscript?<Alert severity="info">A transcript is unavailable for this meeting.</Alert>:transcript?<Stack spacing={2} mt={2}>{transcript.paragraphs.map((p,i)=><Typography key={i}>{p.startSeconds!==null?<strong>[{formatTimestamp(p.startSeconds)}] </strong>:null}{p.text}</Typography>)}</Stack>:<Alert severity="warning">The transcript viewer is unavailable. The download may still be available.</Alert>}</Box>
    <Box className="surface"><Typography component="h2" variant="h5" fontWeight={750}>Actions linked to this meeting</Typography><Typography color="text.secondary">Independent action tracking and meeting-context navigation arrive in P3-07. Generated action items above remain an immutable snapshot.</Typography></Box>
  </Stack></Container>
}
function Section({title,children}:{title:string;children:React.ReactNode}){return <Box component="section"><Typography component="h2" variant="h5" fontWeight={750}>{title}</Typography><Box mt={1}>{children}</Box></Box>}
function ListSection({title,items}:{title:string;items:string[]}){return <Section title={title}>{items.length?<Stack component="ul">{items.map((x,i)=><Typography component="li" key={`${x}-${i}`}>{x}</Typography>)}</Stack>:<Typography color="text.secondary">None recorded.</Typography>}</Section>}
function State({title,text}:{title:string;text:string}){return <Container component="main" maxWidth="md" className="route-page"><Box className="surface placeholder-page"><Typography component="h1" variant="h4">{title}</Typography><Typography>{text}</Typography><Button component={Link} to="/meetings">Back to meeting minutes</Button></Box></Container>}
function formatDate(v:string|null){return v?new Intl.DateTimeFormat(undefined,{dateStyle:'medium',timeStyle:'short'}).format(new Date(v)):'—'}
function formatMode(v:string){return v==='FullMeeting'?'Full meeting':v==='MinutesFromTranscript'?'Minutes from transcript':'Transcript only'}
function formatTimestamp(s:number){const n=Math.floor(s);return `${String(Math.floor(n/60)).padStart(2,'0')}:${String(n%60).padStart(2,'0')}`}
