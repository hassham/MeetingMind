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
)
