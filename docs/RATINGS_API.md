# Customer ratings API

Base URL: `/api/v1`. Timestamps use UTC ISO 8601. Errors use `ProblemDetails` with a stable `code` and `traceId`.

When freelancer work is submitted, the booking moves to `PendingAcceptance`. The customer can call `POST /bookings/{bookingId}/accept-work` to accept it and move it to `Completed`, then review the assigned freelancer once. Reviews publish immediately and contribute to the freelancer's `averageRating` and `ratingCount` shown by the public freelancer directory.

`GET /bookings/{bookingId}` exposes `AcceptWork` in `allowedActions` while the booking is `PendingAcceptance`.

## Create a review

`POST /bookings/{bookingId}/rating` — requires a Customer access token.

```http
POST /api/v1/bookings/00000000-0000-0000-0000-000000000000/rating
Authorization: Bearer <customer-access-token>
Content-Type: application/json

{
  "score": 5,
  "comment": "Đúng giờ, làm việc cẩn thận và sạch sẽ."
}
```

`score` is required and must be from 1 to 5. `comment` is optional and accepts up to 2,000 characters. Success returns `201 Created`:

```json
{
  "id": "...",
  "bookingId": "...",
  "bookingNumber": "AB...",
  "providerId": "...",
  "providerName": "...",
  "score": 5,
  "comment": "Đúng giờ, làm việc cẩn thận và sạch sẽ.",
  "createdAtUtc": "2026-10-20T10:30:00Z",
  "publishedAtUtc": "2026-10-20T10:30:00Z"
}
```

Possible errors:

| HTTP | `code` | Meaning |
| --- | --- | --- |
| 400 | Validation error | Score is outside 1–5 or comment exceeds the length limit. |
| 401 | `INVALID_TOKEN` | Missing or invalid customer identity. |
| 404 | `BOOKING_NOT_FOUND` | Booking does not belong to this customer or does not exist. |
| 409 | `BOOKING_NOT_COMPLETED` | Booking is not completed yet. |
| 409 | `BOOKING_ALREADY_RATED` | This booking already has a review. |

## Get my review

`GET /bookings/{bookingId}/rating` — requires a Customer access token and only returns the current customer's review.

Returns the same `RatingResponse` shape as create. Returns `404 RATING_NOT_FOUND` if the customer has not rated that booking.

## List public reviews

`GET /freelancers/{providerId}/ratings?page=1&pageSize=20` — public, no token required. Only published reviews for approved freelancers are returned. `pageSize` is clamped to 1–50.

```json
{
  "items": [
    {
      "id": "...",
      "bookingId": "...",
      "bookingNumber": "AB...",
      "score": 5,
      "comment": "Dịch vụ rất tốt, tôi sẽ đặt lại.",
      "publishedAtUtc": "2026-10-20T10:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

An unknown or unapproved freelancer returns `404 FREELANCER_NOT_FOUND`.

## Booking detail integration

`GET /bookings/{bookingId}` includes `allowedActions`. When status is `Completed` and the customer has not reviewed, `allowedActions` includes `RateProvider`. After a successful review it no longer includes that action. Use it to show/hide the review form; the server remains authoritative and rejects duplicate or premature submissions.

## Demo data

Run `powershell -File scripts/seed-rating-fixtures.ps1` against the development database after `scripts/seed-fake-freelancers.ps1`. For each of the five approved public demo freelancers it creates one reviewed completed booking (`ABRATE-C002`–`ABRATE-C006`) and one completed booking without a review (`ABUNRV-C002`–`ABUNRV-C006`). Customers are `0920000002`–`0920000006`, password `Customer-dev-2026`. The script is deterministic and safe to rerun.

To load one freelancer's public reviews, use `GET /api/v1/freelancers/{providerId}/ratings?page=1&pageSize=20`; approved demo provider IDs end in `000000000001` through `000000000005`. The ready-to-review bookings can be found with that customer token at `GET /api/v1/bookings?status=Completed` and `GET /api/v1/bookings/{bookingId}`; the detail includes `RateProvider` in `allowedActions` until a review is submitted.
