import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import App from './App'
import { server } from './test/server'

const jobId = '11111111-1111-1111-1111-111111111111'
const now = '2026-07-17T00:00:00Z'

function historyItem(
  status: string,
  stage: string,
  progress: number,
  processingDurationSeconds = 65,
  totalDurationSeconds = 185,
) {
  return {
    jobId,
    originalFileName: 'planning.mp3',
    status,
    stage,
    progress,
    errorMessage: status === 'Failed' ? 'Temporary provider failure' : null,
    createdAt: now,
    updatedAt: now,
    startedAt: status === 'Queued' ? null : now,
    completedAt: status === 'Completed' || status === 'Failed' ? now : null,
    processingDurationSeconds,
    totalDurationSeconds,
  }
}

function historyResponse(item = historyItem('Completed', 'Completed', 100)) {
  return {
    skip: 0,
    take: 50,
    total: 1,
    items: [item],
  }
}

function completedStatus() {
  return {
    jobId,
    status: 'Completed',
    stage: 'Completed',
    progress: 100,
    errorMessage: null,
    processingDurationSeconds: 125,
    totalDurationSeconds: 185,
  }
}

function minutesResult() {
  return {
    jobId,
    title: 'Sprint Planning',
    summary: 'The team agreed on the sprint scope.',
    attendees: ['Hasham'],
    discussionPoints: ['Testing strategy'],
    decisions: ['Use isolated PostgreSQL'],
    actionItems: [{ description: 'Add tests', owner: 'Hasham', dueDate: 'Friday' }],
    risks: ['Docker availability'],
    nextSteps: ['Run verification'],
  }
}

function useCompletedResultHandlers() {
  server.use(
    http.get('*/api/meetings/:id/status', () => HttpResponse.json(completedStatus())),
    http.get('*/api/meetings/:id/result', () => HttpResponse.json(minutesResult())),
    http.get('*/api/meetings/:id/transcript/download', () =>
      HttpResponse.text('Transcript content'),
    ),
  )
}

describe('MeetingMind workflow', () => {
  it('loads empty history on startup', async () => {
    render(<App />)

    expect(await screen.findByText('No meeting jobs have been created yet.')).toBeInTheDocument()
    expect(screen.getByText('0 jobs')).toBeInTheDocument()
  })

  it('uploads supported audio and shows the queued job', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('*/api/meetings/upload', () =>
        HttpResponse.json(
          { jobId, status: 'Queued', stage: 'Uploaded' },
          { status: 202 },
        ),
      ),
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(historyItem('Queued', 'Uploaded', 0))),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          jobId,
          status: 'Queued',
          stage: 'Uploaded',
          progress: 0,
          errorMessage: null,
          processingDurationSeconds: 0,
          totalDurationSeconds: 3,
        }),
      ),
    )
    render(<App />)
    await screen.findByText('No meeting jobs have been created yet.')
    const input = document.querySelector<HTMLInputElement>('input[type="file"]')
    expect(input).not.toBeNull()

    await user.upload(input!, new File(['audio'], 'planning.mp3', { type: 'audio/mpeg' }))

    expect(await screen.findByText('Upload accepted. Processing has started.')).toBeInTheDocument()
    expect(screen.getAllByText('planning.mp3').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Uploaded').length).toBeGreaterThan(0)
  })

  it('shows a safe upload error returned by the API', async () => {
    const user = userEvent.setup()
    server.use(
      http.post('*/api/meetings/upload', () =>
        HttpResponse.json({ error: 'Unsupported file MIME type.' }, { status: 400 }),
      ),
    )
    render(<App />)
    await screen.findByText('No meeting jobs have been created yet.')
    const input = document.querySelector<HTMLInputElement>('input[type="file"]')

    await user.upload(input!, new File(['text'], 'planning.mp3', { type: 'text/plain' }))

    expect(await screen.findByText('Unsupported file MIME type.')).toBeInTheDocument()
  })

  it('loads completed minutes, transcript, and download links from history', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () => HttpResponse.json(historyResponse())),
    )
    useCompletedResultHandlers()
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /planning\.mp3/i }))

    expect(await screen.findByText('Sprint Planning')).toBeInTheDocument()
    expect(screen.getByText('Use isolated PostgreSQL')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Transcript' })).toHaveAttribute(
      'href',
      `/api/meetings/${jobId}/transcript/download`,
    )
    expect(screen.getByRole('link', { name: 'Minutes' })).toHaveAttribute(
      'href',
      `/api/meetings/${jobId}/minutes/download`,
    )

    await user.click(screen.getByRole('tab', { name: 'Transcript' }))
    expect(screen.getByText('Transcript content')).toBeInTheDocument()
  })

  it('retries a failed job and presents the queued state', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(historyItem('Failed', 'Transcribing', 25))),
      ),
      http.post('*/api/meetings/:id/retry', () =>
        HttpResponse.json(
          { jobId, status: 'Queued', stage: 'Uploaded' },
          { status: 202 },
        ),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          jobId,
          status: 'Queued',
          stage: 'Uploaded',
          progress: 0,
          errorMessage: null,
          processingDurationSeconds: 65,
          totalDurationSeconds: 190,
        }),
      ),
    )
    render(<App />)

    const retry = await screen.findByRole('button', { name: 'Retry' })
    await user.click(retry)

    expect(await screen.findByText('Retry queued. Processing has restarted.')).toBeInTheDocument()
    expect(screen.getByText('Uploaded')).toBeInTheDocument()
  })

  it('shows an actionable retry error returned by the API', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(historyItem('Failed', 'Transcribing', 25))),
      ),
      http.post('*/api/meetings/:id/retry', () =>
        HttpResponse.json(
          { error: 'Only failed or cancelled meeting jobs can be retried.' },
          { status: 409 },
        ),
      ),
    )
    render(<App />)

    await user.click(await screen.findByRole('button', { name: 'Retry' }))

    expect(
      await screen.findByText('Only failed or cancelled meeting jobs can be retried.'),
    ).toBeInTheDocument()
  })

  it('shows unavailable result messaging when completed artifacts cannot be loaded', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () => HttpResponse.json(historyResponse())),
      http.get('*/api/meetings/:id/status', () => HttpResponse.json(completedStatus())),
      http.get('*/api/meetings/:id/result', () =>
        HttpResponse.json({ error: 'Meeting minutes not found.' }, { status: 404 }),
      ),
      http.get('*/api/meetings/:id/transcript/download', () =>
        HttpResponse.json({ error: 'Meeting transcript not found.' }, { status: 404 }),
      ),
    )
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /planning\.mp3/i }))

    expect(
      await screen.findByText('Minutes are not available for this job.'),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Transcript' }))
    expect(screen.getByText('Transcript is not available for this job.')).toBeInTheDocument()
  })

  it('polls an active job until completed and then loads results', async () => {
    let statusRequests = 0
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(historyItem('Processing', 'Transcribing', 25))),
      ),
      http.get('*/api/meetings/:id/status', () => {
        statusRequests += 1
        return HttpResponse.json(
          statusRequests === 1
            ? {
                jobId,
                status: 'Processing',
                stage: 'Transcribing',
                progress: 25,
                errorMessage: null,
                processingDurationSeconds: 10,
                totalDurationSeconds: 70,
              }
            : completedStatus(),
        )
      }),
      http.get('*/api/meetings/:id/result', () => HttpResponse.json(minutesResult())),
      http.get('*/api/meetings/:id/transcript/download', () =>
        HttpResponse.text('Transcript content'),
      ),
    )
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: /planning\.mp3/i }))
    expect((await screen.findAllByText('Transcribing')).length).toBeGreaterThan(0)
    expect(await screen.findByText('Sprint Planning', {}, { timeout: 7000 })).toBeInTheDocument()
    expect(statusRequests).toBeGreaterThanOrEqual(2)
  })

  it('formats processing and total duration in history and selected details', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(
          historyResponse(historyItem('Completed', 'Completed', 100, 125, 3723)),
        ),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          ...completedStatus(),
          processingDurationSeconds: 125,
          totalDurationSeconds: 3723,
        }),
      ),
      http.get('*/api/meetings/:id/result', () => HttpResponse.json(minutesResult())),
      http.get('*/api/meetings/:id/transcript/download', () =>
        HttpResponse.text('Transcript content'),
      ),
    )
    render(<App />)

    expect(
      await screen.findByText('2m 05s processing · 1h 02m 03s total'),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /planning\.mp3/i }))
    await user.click(screen.getByRole('button', { name: /processing details/i }))

    expect(screen.getByText('Processing duration')).toBeInTheDocument()
    expect(screen.getByText('Total duration')).toBeInTheDocument()
    expect(screen.getAllByText('2m 05s').length).toBeGreaterThan(0)
    expect(screen.getAllByText('1h 02m 03s').length).toBeGreaterThan(0)
  })

  it('ticks active selected duration every second between API polls', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(
          historyResponse(historyItem('Processing', 'Transcribing', 25, 10, 70)),
        ),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          jobId,
          status: 'Processing',
          stage: 'Transcribing',
          progress: 25,
          errorMessage: null,
          processingDurationSeconds: 10,
          totalDurationSeconds: 70,
        }),
      ),
    )
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /planning\.mp3/i }))
    await user.click(screen.getByRole('button', { name: /processing details/i }))
    expect(screen.getByText('10s')).toBeInTheDocument()

    expect(await screen.findByText('11s', {}, { timeout: 2500 })).toBeInTheDocument()
    expect(screen.getByText('1m 11s')).toBeInTheDocument()
  })

  it('shows a history API error without crashing the workspace', async () => {
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json({ error: 'History service unavailable.' }, { status: 503 }),
      ),
    )
    render(<App />)

    expect(await screen.findByText('History service unavailable.')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByText('No job selected')).toBeInTheDocument())
  })
})
