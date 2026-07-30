import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import axe from 'axe-core'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import RootApp from './RootApp'
import { server } from './test/server'

const summary = {
  timeBasis: 'All time',
  totalJobs: 4,
  jobsByMode: {
    transcriptOnly: 1,
    fullMeeting: 2,
    minutesFromTranscript: 1,
  },
  jobsByStatus: {
    completed: 2,
    failed: 1,
    cancelled: 0,
    active: 1,
    queued: 1,
    processing: 0,
  },
  successRatePercent: 66.666,
  totalAudioDurationSeconds: 3600,
  averageCompletedProcessingDurationSeconds: 90,
  transcriptCount: 3,
  minutesCount: 2,
  recentJobs: [
    {
      jobId: '11111111-1111-1111-1111-111111111111',
      originalFileName: 'planning.mp3',
      processingMode: 'FullMeeting',
      status: 'Completed',
      stage: 'Completed',
      progress: 100,
      createdAt: '2026-07-30T01:00:00Z',
      updatedAt: '2026-07-30T01:02:00Z',
    },
  ],
  recentMinutes: [
    {
      jobId: '11111111-1111-1111-1111-111111111111',
      title: 'Planning',
      originalFileName: 'planning.mp3',
      processingMode: 'FullMeeting',
      generatedAt: '2026-07-30T01:02:00Z',
    },
  ],
}

function renderRoute(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <RootApp />
    </MemoryRouter>,
  )
}

async function expectNoSeriousAxeViolations(container: HTMLElement) {
  const results = await axe.run(container, {
    rules: { 'color-contrast': { enabled: false } },
  })
  expect(
    results.violations.filter(
      (violation) => violation.impact === 'serious' || violation.impact === 'critical',
    ),
  ).toEqual([])
}

describe('application routes and dashboard', () => {
  it('renders populated all-time metrics and recent links accessibly', async () => {
    server.use(http.get('*/api/dashboard/summary', () => HttpResponse.json(summary)))
    const { container } = renderRoute()

    expect(await screen.findByText('66.7%')).toBeInTheDocument()
    expect(screen.getByText('1h 0m')).toBeInTheDocument()
    expect(screen.getByText('planning.mp3')).toBeInTheDocument()
    expect(screen.getByText('Planning')).toBeInTheDocument()
    await expectNoSeriousAxeViolations(container)
  })

  it('renders the zero-data welcome and unavailable success metrics safely', async () => {
    server.use(
      http.get('*/api/dashboard/summary', () =>
        HttpResponse.json({
          ...summary,
          totalJobs: 0,
          successRatePercent: null,
          totalAudioDurationSeconds: null,
          averageCompletedProcessingDurationSeconds: null,
          transcriptCount: 0,
          minutesCount: 0,
          recentJobs: [],
          recentMinutes: [],
        }),
      ),
    )
    renderRoute()

    expect(await screen.findByRole('heading', { name: 'Start your first meeting' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Create a processing job' })).toHaveAttribute(
      'href',
      '/process/new',
    )
  })

  it('keeps navigation usable on dashboard failure and retries', async () => {
    let attempts = 0
    server.use(
      http.get('*/api/dashboard/summary', () => {
        attempts += 1
        return attempts === 1
          ? HttpResponse.json({}, { status: 500 })
          : HttpResponse.json(summary)
      }),
    )
    renderRoute()

    const retry = await screen.findByRole('button', { name: 'Retry' })
    expect(screen.getAllByRole('link', { name: 'New processing job' }).length).toBeGreaterThan(0)
    await userEvent.click(retry)
    expect(await screen.findByText('66.7%')).toBeInTheDocument()
  })

  it.each([
    ['/process/new', 'New processing job'],
    ['/processing', 'All processing'],
    ['/meetings', 'Meeting minutes'],
    ['/meetings/11111111-1111-1111-1111-111111111111', 'Meeting detail'],
    ['/actions', 'Actions'],
    ['/unknown', 'Page not found'],
  ])('restores direct route %s', async (path, heading) => {
    renderRoute(path)
    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument(),
    )
  })
})
