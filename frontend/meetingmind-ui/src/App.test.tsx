import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import axe from 'axe-core'
import axios from 'axios'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
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
  overrides: Record<string, unknown> = {},
) {
  return {
    jobId,
    originalFileName: 'planning.mp3',
    status,
    stage,
    progress,
    errorCode: status === 'Failed' ? 'provider_unavailable' : null,
    errorMessage: status === 'Failed' ? 'Temporary provider failure' : null,
    automaticRetryCount: 0,
    automaticRetryLimit: 3,
    nextRetryAt: null,
    createdAt: now,
    updatedAt: now,
    startedAt: status === 'Queued' ? null : now,
    completedAt: status === 'Completed' || status === 'Failed' ? now : null,
    processingDurationSeconds,
    totalDurationSeconds,
    ...overrides,
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
    errorCode: null,
    errorMessage: null,
    automaticRetryCount: 0,
    automaticRetryLimit: 3,
    nextRetryAt: null,
    processingDurationSeconds: 125,
    totalDurationSeconds: 185,
  }
}

async function expectNoSeriousAxeViolations(container: HTMLElement) {
  const results = await axe.run(container, {
    rules: { 'color-contrast': { enabled: false } },
  })
  const blockingViolations = results.violations.filter(
    (violation) => violation.impact === 'serious' || violation.impact === 'critical',
  )

  expect(
    blockingViolations.map((violation) => ({
      id: violation.id,
      impact: violation.impact,
      targets: violation.nodes.map((node) => node.target),
    })),
  ).toEqual([])
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
    vi.spyOn(axios, 'post').mockResolvedValue({
      data: { jobId, status: 'Queued', stage: 'Uploaded' },
    })
    server.use(
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

    fireEvent.change(input!, {
      target: {
        files: [new File(['audio'], 'planning.mp3', { type: 'audio/mpeg' })],
      },
    })

    expect(await screen.findByText('Upload accepted. Processing has started.')).toBeInTheDocument()
    expect(screen.getAllByText('planning.mp3').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Uploaded').length).toBeGreaterThan(0)
  })

  it('shows a safe upload error returned by the API', async () => {
    vi.spyOn(axios, 'post').mockRejectedValue({
      isAxiosError: true,
      message: 'Request failed with status code 400',
      response: { data: { error: 'Unsupported file MIME type.' }, status: 400 },
    })
    render(<App />)
    await screen.findByText('No meeting jobs have been created yet.')
    const input = document.querySelector<HTMLInputElement>('input[type="file"]')

    fireEvent.change(input!, {
      target: {
        files: [new File(['text'], 'planning.mp3', { type: 'text/plain' })],
      },
    })

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
        HttpResponse.json(
          historyResponse(
            historyItem('Failed', 'Transcribing', 25, 65, 185, {
              automaticRetryCount: 2,
              automaticRetryLimit: 3,
            }),
          ),
        ),
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
          errorCode: null,
          errorMessage: null,
          automaticRetryCount: 0,
          automaticRetryLimit: 3,
          nextRetryAt: null,
          processingDurationSeconds: 65,
          totalDurationSeconds: 190,
        }),
      ),
    )
    render(<App />)

    const retry = await screen.findByRole('button', { name: 'Retry' })
    await user.click(retry)

    expect(await screen.findByText('Retry queued. Processing has restarted.')).toBeInTheDocument()
    expect(screen.getAllByText('Uploaded')).toHaveLength(2)
    expect(screen.queryByText('Retry 2/3')).not.toBeInTheDocument()
  })

  it('opens the file chooser from the keyboard-accessible upload button', async () => {
    const user = userEvent.setup()
    const inputClick = vi
      .spyOn(HTMLInputElement.prototype, 'click')
      .mockImplementation(() => undefined)

    render(<App />)
    await screen.findByText('No meeting jobs have been created yet.')

    const selectFile = screen.getByRole('button', { name: 'Select file' })
    selectFile.focus()
    await user.keyboard('{Enter}')

    expect(inputClick).toHaveBeenCalledTimes(1)
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
      await screen.findByText(
        'Minutes could not be loaded even though processing completed. Refresh and try again.',
      ),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Transcript' }))
    expect(
      screen.getByText(
        'Transcript could not be loaded even though processing completed. Refresh and try again.',
      ),
    ).toBeInTheDocument()
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

  it('keeps the selected history card synchronized with status polling', async () => {
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(historyItem('Queued', 'Uploaded', 0, 0, 5))),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          jobId,
          status: 'Processing',
          stage: 'Validating',
          progress: 0,
          errorMessage: null,
          processingDurationSeconds: 5,
          totalDurationSeconds: 10,
        }),
      ),
    )
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: /planning\.mp3/i }))

    await waitFor(() => {
      expect(screen.getAllByText('Processing')).toHaveLength(2)
      expect(screen.getAllByText('Validating')).toHaveLength(2)
      expect(screen.queryByText('Queued')).not.toBeInTheDocument()
      expect(screen.queryByText('Uploaded')).not.toBeInTheDocument()
    })
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

    const alert = await screen.findByText('History service unavailable.')
    expect(alert).toBeInTheDocument()
    await waitFor(() => expect(alert.closest('[role="alert"]')).toHaveFocus())
    await waitFor(() => expect(screen.getByText('No job selected')).toBeInTheDocument())
  })

  it('navigates first, middle, and last history pages while retaining selection', async () => {
    const user = userEvent.setup()
    server.use(
      http.get('*/api/meetings/history', ({ request }) => {
        const skip = Number(new URL(request.url).searchParams.get('skip') ?? 0)
        return HttpResponse.json({
          skip,
          take: 20,
          total: 45,
          items: [
            historyItem('Processing', 'Transcribing', 25, 10, 70, {
              jobId: `${skip.toString().padStart(8, '0')}-1111-1111-1111-111111111111`,
              originalFileName: `page-${skip / 20 + 1}.mp3`,
            }),
          ],
        })
      }),
      http.get('*/api/meetings/:id/status', ({ params }) =>
        HttpResponse.json({
          jobId: params.id,
          status: 'Processing',
          stage: 'Transcribing',
          progress: 25,
          errorCode: null,
          errorMessage: null,
          automaticRetryCount: 0,
          automaticRetryLimit: 3,
          nextRetryAt: null,
          processingDurationSeconds: 10,
          totalDurationSeconds: 70,
        }),
      ),
    )
    render(<App />)

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: /page-1\.mp3/i }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'page-1.mp3' })).toHaveFocus())

    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText('Page 2 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
    expect(screen.getByRole('heading', { name: 'page-1.mp3' })).toBeInTheDocument()
    await waitFor(() => expect(screen.getByRole('heading', { name: 'History' })).toHaveFocus())

    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('shows scheduled automatic retry state and a single polite status announcement', async () => {
    const user = userEvent.setup()
    const nextRetryAt = new Date(Date.now() + 65_000).toISOString()
    const retryingItem = historyItem('Queued', 'Uploaded', 0, 5, 20, {
      automaticRetryCount: 1,
      automaticRetryLimit: 3,
      nextRetryAt,
    })
    server.use(
      http.get('*/api/meetings/history', () =>
        HttpResponse.json(historyResponse(retryingItem)),
      ),
      http.get('*/api/meetings/:id/status', () =>
        HttpResponse.json({
          ...retryingItem,
          originalFileName: undefined,
          createdAt: undefined,
          updatedAt: undefined,
          startedAt: undefined,
          completedAt: undefined,
        }),
      ),
    )
    render(<App />)

    expect(await screen.findByText('Retry 1/3')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /planning\.mp3/i }))

    expect(await screen.findByText(/Automatic retry 1 of 3 is scheduled in/)).toBeInTheDocument()
    const liveRegions = screen.getAllByRole('status')
    expect(liveRegions).toHaveLength(1)
    expect(liveRegions[0]).toHaveTextContent('Status Queued. Stage Uploaded. Progress 0 percent.')
  })

  it('explains exhausted retries and maps a safe recovery action', async () => {
    const user = userEvent.setup()
    const failedItem = historyItem('Failed', 'Transcribing', 25, 65, 185, {
      automaticRetryCount: 3,
      automaticRetryLimit: 3,
      nextRetryAt: null,
      errorCode: 'storage_full',
      errorMessage: 'Local result storage is full.',
    })
    server.use(
      http.get('*/api/meetings/history', () => HttpResponse.json(historyResponse(failedItem))),
      http.get('*/api/meetings/:id/status', () => HttpResponse.json(failedItem)),
    )
    render(<App />)

    await user.click(await screen.findByRole('button', { name: /planning\.mp3/i }))

    expect(screen.getByText('Automatic retries are exhausted. Review the failure and retry manually when ready.')).toBeInTheDocument()
    expect(screen.getByText('Free local storage space before retrying this meeting.')).toBeInTheDocument()
    const failureAlert = screen.getByText('Local result storage is full.').closest('[role="alert"]')
    await waitFor(() => expect(failureAlert).toHaveFocus())
  })

  it('scrolls and focuses selected details on a narrow screen', async () => {
    const user = userEvent.setup()
    const originalMatchMedia = window.matchMedia
    const originalScrollIntoView = HTMLElement.prototype.scrollIntoView
    const scrollIntoView = vi.fn()
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      value: vi.fn().mockReturnValue({ matches: true }),
    })
    HTMLElement.prototype.scrollIntoView = scrollIntoView

    try {
      server.use(
        http.get('*/api/meetings/history', () => HttpResponse.json(historyResponse())),
      )
      useCompletedResultHandlers()
      render(<App />)

      await user.click(await screen.findByRole('button', { name: /planning\.mp3/i }))

      await waitFor(() => expect(screen.getByRole('heading', { name: 'planning.mp3' })).toHaveFocus())
      expect(scrollIntoView).toHaveBeenCalledWith({ block: 'start', behavior: 'smooth' })
    } finally {
      Object.defineProperty(window, 'matchMedia', {
        configurable: true,
        value: originalMatchMedia,
      })
      HTMLElement.prototype.scrollIntoView = originalScrollIntoView
    }
  })

  it(
    'keeps status polling non-overlapping when a request is still in flight',
    async () => {
      let statusRequests = 0
      let releasePoll: (() => void) | undefined
      server.use(
        http.get('*/api/meetings/history', () =>
          HttpResponse.json(historyResponse(historyItem('Processing', 'Transcribing', 25))),
        ),
        http.get('*/api/meetings/:id/status', () => {
          statusRequests += 1
          const response = HttpResponse.json({
            ...historyItem('Processing', 'Transcribing', 25),
            originalFileName: undefined,
          })
          if (statusRequests === 1) {
            return response
          }

          return new Promise<Response>((resolve) => {
            releasePoll = () => resolve(response)
          })
        }),
      )
      render(<App />)

      fireEvent.click(await screen.findByRole('button', { name: /planning\.mp3/i }))
      await waitFor(() => expect(statusRequests).toBe(1))
      await new Promise((resolve) => window.setTimeout(resolve, 10_200))

      expect(statusRequests).toBe(2)
      releasePoll?.()
    },
    15_000,
  )

  it('has no serious or critical axe violations in primary frontend states', async () => {
    const states = [
      null,
      historyItem('Processing', 'Transcribing', 25),
      historyItem('Completed', 'Completed', 100),
      historyItem('Failed', 'Transcribing', 25),
      historyItem('Queued', 'Uploaded', 0),
    ]

    for (const [index, item] of states.entries()) {
      server.use(
        http.get('*/api/meetings/history', () =>
          HttpResponse.json(
            item
              ? { ...historyResponse(item), total: index === 4 ? 45 : 1, take: 20 }
              : { ...historyResponse(), total: 0, items: [], take: 20 },
          ),
        ),
      )
      const view = render(<App />)
      await screen.findByText(item ? item.originalFileName : 'No meeting jobs have been created yet.')

      await expectNoSeriousAxeViolations(view.container)
      view.unmount()
    }
  })
})
