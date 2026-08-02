import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'

export const emptyHistory = {
  skip: 0,
  take: 50,
  total: 0,
  items: [],
}

export const server = setupServer(
  http.get('*/api/meetings/history', () => HttpResponse.json(emptyHistory)),
  http.get('*/api/meetings/minutes', () => HttpResponse.json({ skip: 0, take: 20, total: 0, items: [] })),
  http.get('*/api/actions', () => HttpResponse.json({ skip: 0, take: 25, total: 0, items: [] })),
)
