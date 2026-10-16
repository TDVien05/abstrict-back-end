# Booking API

Luồng đặt lịch của khách hàng với freelancer: địa chỉ → lịch rảnh → báo giá → tạo đơn giữ chỗ → đặt cọc 15% → freelancer xác nhận → mã QR check-in → freelancer check-in → ảnh tiến độ (Before/After) và cập nhật checklist → hoàn tất ca.

Base URL: `/api/v1`. Lỗi trả `ProblemDetails` kèm `code` và `traceId`. Mọi thời điểm là UTC (ISO 8601); ngày lịch tính theo `Asia/Ho_Chi_Minh`. Tiền là số nguyên VND và luôn do server tính.

Chưa hỗ trợ: đối tác công ty, nghiệm thu phía khách, gia hạn ca, đánh giá, khiếu nại, cổng thanh toán thật.

## Tổng hợp endpoint

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/auth/refresh` | cookie | Khôi phục phiên, trả cùng cấu trúc với đăng nhập. |
| POST | `/auth/logout` | cookie | Đăng xuất, thu hồi phiên (204). |
| GET | `/customer/addresses` | Customer | Sổ địa chỉ, mặc định đứng đầu. |
| POST | `/customer/addresses` | Customer | Thêm địa chỉ (tối đa 10). |
| PUT | `/customer/addresses/{id}` | Customer | Sửa địa chỉ. |
| DELETE | `/customer/addresses/{id}` | Customer | Xóa địa chỉ (204). |
| GET | `/freelancers/{providerId}/availability` | public | Lịch rảnh/bận theo ngày. |
| GET | `/catalog/service-categories/{code}/checklist` | public | Checklist của danh mục. |
| POST | `/bookings/quote` | Customer | Báo giá, không tạo đơn. |
| POST | `/bookings` | Customer | Tạo đơn và giữ chỗ (201). |
| GET | `/bookings` | Customer | Đơn của tôi, có phân trang. |
| GET | `/bookings/{id}` | Customer | Chi tiết đơn. |
| POST | `/bookings/{id}/deposit` | Customer | Khởi tạo thanh toán cọc. |
| POST | `/payments/sandbox/{paymentId}/confirm` | Customer | Chốt kết quả thanh toán giả lập. |
| GET | `/bookings/{id}/cancellation-preview` | Customer | Xem trước tiền hoàn/giữ khi hủy. |
| POST | `/bookings/{id}/cancel` | Customer | Hủy đơn. |
| POST | `/bookings/{id}/check-in-token` | Customer | Lấy mã QR check-in. |
| GET | `/freelancer/availability` | Freelancer | Khoảng rảnh của tôi. |
| POST | `/freelancer/availability` | Freelancer | Thêm khoảng rảnh (201). |
| DELETE | `/freelancer/availability/{id}` | Freelancer | Xóa khoảng rảnh (204). |
| GET | `/freelancer/bookings` | Freelancer | Đơn gán cho tôi. |
| GET | `/freelancer/bookings/counts` | Freelancer | Số đơn theo từng trạng thái. |
| GET | `/freelancer/bookings/{id}` | Freelancer | Chi tiết đơn. |
| POST | `/freelancer/bookings/{id}/confirm` | Freelancer | Xác nhận đơn. |
| POST | `/freelancer/bookings/{id}/reject` | Freelancer | Từ chối đơn, khách được hoàn đủ cọc. |
| POST | `/freelancer/bookings/{id}/check-in` | Freelancer | Quét mã QR check-in của khách, bắt đầu ca. |
| PATCH | `/freelancer/bookings/{id}/tasks/{taskId}` | Freelancer | Đổi trạng thái một mục checklist trong ca. |
| POST | `/freelancer/bookings/{id}/photos` | Freelancer | Tải ảnh tiến độ Before/After (`multipart/form-data`). |
| GET | `/freelancer/bookings/{id}/photos/{photoId}/content` | Freelancer | Nội dung ảnh tiến độ. |
| POST | `/freelancer/bookings/{id}/complete` | Freelancer | Hoàn tất ca, chờ khách nghiệm thu. |
| GET | `/bookings/{id}/photos/{photoId}/content` | Customer | Nội dung ảnh tiến độ của đơn tôi đặt. |

Các endpoint ghi (tạo đơn, đặt cọc, chốt sandbox, hủy, lấy mã QR, sửa địa chỉ, sửa lịch rảnh, xác nhận/từ chối) dùng chung giới hạn 30 request / 10 phút / người dùng (429 khi vượt). `POST /bookings/quote` và mọi endpoint GET **không** tính vào giới hạn này, nên FE gọi lại báo giá thoải mái.

## Đăng nhập và phiên

`POST /auth/login` dùng chung cho mọi vai trò. `role` trong response là một trong `Customer`, `Freelancer`, `Company`, `Admin`. Khách hàng nhận kèm `customerProfile` (xem `CUSTOMER_PROFILE_API.md`); công ty nhận kèm `companyOnboarding` (xem `COMPANY_ONBOARDING_API.md`); freelancer nhận kèm `freelancerOnboarding`:

```json
"freelancerOnboarding": { "providerId": "dddddddd-0000-0000-0000-000000000001", "kycStatus": "Approved", "currentStep": "Completed" }
```

| Trường | Giá trị |
| --- | --- |
| `kycStatus` | `NotStarted`, `Draft`, `Submitted`, `UnderReview`, `Approved`, `Rejected`, `Suspended`. |
| `currentStep` | Bước tiếp theo: `Profile` (điền thông tin cá nhân), `Documents` (thiếu hoặc bị từ chối giấy tờ), `Submit` (đủ hồ sơ, chưa gửi duyệt), `AwaitingReview`, `Completed`, `Suspended`. |
| `providerId` | `null` khi chưa bắt đầu KYC. |

Đường dẫn xác thực chính thức của freelancer là các đường dẫn đang có trong Swagger: `POST /auth/register-freelancer`, `POST /auth/verify-phone-otp`, `POST /auth/resend-phone-otp`, `POST /auth/login`. Không có nhóm `/auth/freelancers/*`. Công ty đăng ký qua `POST /auth/register-company` và dùng chung ba đường dẫn còn lại.

**Khôi phục phiên.** Đăng nhập thành công đặt cookie `abstrict_refresh` (`HttpOnly`, `SameSite=Lax`, `Path=/api/v1/auth`, sống 30 ngày, `Secure` ngoài môi trường Development). FE gọi các endpoint auth với `credentials: 'include'`.

- `POST /auth/refresh` (không body): trả cùng cấu trúc với `/auth/login` kèm access token mới. Gọi khi tải lại trang và khi access token (15 phút) sắp hết hạn. Không có cookie, phiên hết hạn hoặc đã đăng xuất → 401 `SESSION_EXPIRED`.
- `POST /auth/logout` (không body): thu hồi phiên và xóa cookie, luôn trả 204.

Refresh token không đổi sau mỗi lần refresh, nên nhiều tab dùng chung một phiên không đá nhau. CORS đã bật `Access-Control-Allow-Credentials` cho các origin trong `Cors:AllowedOrigins`.

## Trạng thái đơn

```text
PendingDeposit ──cọc thành công──▶ PendingProviderConfirmation ──xác nhận──▶ Confirmed
   │ hết giờ giữ chỗ → Expired        │ từ chối → Rejected (hoàn đủ)            │ khách hủy → Cancelled
   │ khách hủy → Cancelled            │ quá hạn xác nhận → Expired (hoàn đủ)
                                      │ khách hủy → Cancelled (hoàn đủ)
```

Freelancer bật tự nhận đơn thì cọc thành công chuyển thẳng sang `Confirmed`. Sau đó: freelancer quét mã check-in → `CheckedIn`; ảnh Before đầu tiên → `InProgress`; hoàn tất ca (có ảnh After) → `PendingAcceptance`. FE nên dựa vào `allowedActions` thay vì tự suy luận từ `status`: phía khách là `PayDeposit`, `Cancel`, `ShowCheckInQr` (trong chi tiết đơn); phía freelancer là `Confirm`, `Reject`, `CheckIn`, `UploadPhotos`, `CompleteWork` (trong cả danh sách và chi tiết).

Mặc định (cấu hình ở mục `Booking`, các giá trị chưa chốt chính sách có `TODO: POLICY` trong `BookingOptions`): bước giờ 30 phút, ca 2–12 giờ, đặt trước 60 phút đến 30 ngày, giữ chỗ 15 phút, freelancer có 30 phút để xác nhận, mỗi khách tối đa 3 đơn đang chờ cọc.

## Địa chỉ

```json
{
  "label": "Nhà",
  "apartmentNumber": "A-1205",
  "floor": "12",
  "buildingName": "Vinhomes",
  "streetAddress": "12 Lê Lợi",
  "ward": "Bến Nghé",
  "district": "Quận 1",
  "city": "Hồ Chí Minh",
  "accessInstructions": "Gọi lễ tân",
  "isDefault": true
}
```

`city` và `district` phải khớp chính xác một khu vực trong `GET /catalog/service-areas` thì mới đặt được với freelancer phục vụ khu vực đó. Địa chỉ đầu tiên tự là mặc định. Xóa địa chỉ không ảnh hưởng đơn đã đặt vì đơn lưu bản chụp địa chỉ.

### GET `/catalog/administrative-divisions`

Public. Tỉnh/thành và quận/huyện của Việt Nam cho dropdown Thành phố / Quận-Huyện của form địa chỉ. Lọc theo tỉnh/thành bằng `?city=Hồ Chí Minh`.

```json
[
  {
    "code": 79,
    "name": "Hồ Chí Minh",
    "districts": [
      { "code": 760, "name": "Quận 1", "isServed": true },
      { "code": 783, "name": "Huyện Củ Chi", "isServed": true }
    ]
  }
]
```

`name` của tỉnh/thành bỏ tiền tố "Thành phố"/"Tỉnh"; gửi `name` của tỉnh/thành và của quận/huyện lên `city` và `district` khi lưu địa chỉ. `isServed` là `true` khi nền tảng có khu vực dịch vụ đang hoạt động ở quận/huyện đó (hiện là 22 quận/huyện của TP. Hồ Chí Minh); địa chỉ ở nơi `false` vẫn lưu được nhưng chưa đặt lịch được. Response có `Cache-Control: public, max-age=3600`.

### GET `/catalog/administrative-divisions/provinces/{provinceCode}/districts/{districtCode}/wards`

Public. Phường/xã của một quận/huyện, ví dụ `/provinces/79/districts/760/wards` → `[{ "code": "26734", "name": "Phường Tân Định" }, …]`. Gửi `name` lên `ward` khi lưu địa chỉ. Mã quận/huyện chỉ duy nhất trong một tỉnh (một số mã xuất hiện ở nhiều tỉnh) nên luôn truyền kèm mã tỉnh. Mã không có trả 404 `DISTRICT_NOT_FOUND`; trả `[]` khi chưa tải được dữ liệu phường/xã, lúc đó FE cho nhập tay.

**Nguồn dữ liệu.** Server lấy danh mục từ [Cas AddressKit](https://addresskit.cas.so/) (dữ liệu Cục Thống kê) và giữ trong bộ nhớ 24 giờ, FE không gọi thẳng sang AddressKit (có giới hạn tốc độ). Nếu AddressKit lỗi, server giữ bản đã tải trước đó; chưa tải được lần nào thì trả danh mục nhúng (63 tỉnh và quận/huyện, không có phường/xã) và thử lại sau 5 phút.

**Ngày hiệu lực.** Từ 1/7/2025 cả nước bỏ cấp quận/huyện, bản `latest` của AddressKit chỉ có 34 tỉnh và xã/phường (TP.HCM mới đã gồm cả Bình Dương và Bà Rịa - Vũng Tàu). Hệ thống đặt lịch vẫn theo quận/huyện nên mặc định dùng danh mục ngày `2025-06-30` (3 cấp). Đổi bằng cấu hình `AddressKit:EffectiveDate`; đặt `latest` thì danh sách quận/huyện sẽ rỗng và cần chuyển khu vực dịch vụ sang phường/xã.

## GET `/freelancers/{providerId}/availability`

Query: `from` (`YYYY-MM-DD`, mặc định hôm nay giờ VN), `days` (1–14, mặc định 7).

```json
{
  "providerId": "dddddddd-0000-0000-0000-000000000001",
  "timeZone": "Asia/Ho_Chi_Minh",
  "slotStepMinutes": 30,
  "minimumDurationMinutes": 120,
  "days": [
    {
      "date": "2026-10-13",
      "free": [{ "startsAtUtc": "2026-10-13T01:00:00+00:00", "endsAtUtc": "2026-10-13T05:00:00+00:00" }],
      "busy": [{ "startsAtUtc": "2026-10-13T06:00:00+00:00", "endsAtUtc": "2026-10-13T08:00:00+00:00" }]
    }
  ]
}
```

`free` đã trừ khoảng bận và phần quá sát giờ, bỏ mảnh ngắn hơn 2 giờ. Khách chọn giờ bắt đầu/kết thúc bất kỳ theo bước 30 phút trong một khoảng `free`. `busy` không kèm thông tin khách hay đơn. Freelancer chưa duyệt trả 404 `FREELANCER_NOT_FOUND`.

## POST `/bookings/quote` và POST `/bookings`

Cùng một body:

```json
{
  "providerId": "dddddddd-0000-0000-0000-000000000001",
  "serviceCategoryId": "aaaaaaaa-0000-0000-0000-000000000001",
  "customerAddressId": "d0e60914-0692-4d59-aae3-80997db7ee9e",
  "startsAtUtc": "2026-10-13T01:00:00Z",
  "endsAtUtc": "2026-10-13T03:30:00Z",
  "checklistItemIds": ["c1000000-0000-0000-0001-000000000004"],
  "customerNote": "Có thú cưng"
}
```

`checklistItemIds` là các mục tùy chọn; mục bắt buộc của checklist luôn được thêm. Checklist không làm thay đổi giá.

Báo giá:

```json
{
  "unitPriceVnd": 80000,
  "durationMinutes": 150,
  "baseAmountVnd": 200000,
  "surchargeAmountVnd": 0,
  "depositAmountVnd": 30000,
  "depositPercent": 15,
  "remainingAmountVnd": 170000,
  "holdMinutes": 15
}
```

Giá gốc = đơn giá theo giờ × thời lượng. Cọc = 15% giá gốc, làm tròn lên tới đồng. Tạo đơn trả 201 kèm chi tiết đơn ở trạng thái `PendingDeposit`; chỗ được giữ tới `holdExpiresAtUtc`.

## Chi tiết đơn

```json
{
  "id": "d05169e0-9eae-4e8c-9494-8fedcb975ade",
  "bookingNumber": "AB261012HXQDC2",
  "status": "PendingDeposit",
  "providerId": "dddddddd-0000-0000-0000-000000000001",
  "providerDisplayName": "Nguyễn Thị Lan",
  "serviceCategoryCode": "CLEANING",
  "serviceCategoryName": "Dọn dẹp nhà cửa",
  "startsAtUtc": "2026-10-13T01:00:00+00:00",
  "endsAtUtc": "2026-10-13T03:30:00+00:00",
  "baseAmountVnd": 200000,
  "depositAmountVnd": 30000,
  "holdExpiresAtUtc": "2026-10-12T07:23:20+00:00",
  "addressSnapshot": "A-1205, Vinhomes, 12 Lê Lợi, Bến Nghé, Quận 1, Hồ Chí Minh (Ghi chú: Gọi lễ tân)",
  "customerNote": "Có thú cưng",
  "unitPriceVnd": 80000,
  "surchargeAmountVnd": 0,
  "extensionAmountVnd": 0,
  "remainingAmountVnd": 170000,
  "providerConfirmationDeadlineUtc": null,
  "cancelledAtUtc": null,
  "cancellationReason": null,
  "depositPaymentStatus": "Pending",
  "depositPayment": {
    "paymentId": "e3bc5193-8ddd-4eda-8152-5e5a24e305d5",
    "status": "Pending",
    "amountVnd": 30000,
    "method": "Momo",
    "expiresAtUtc": "2026-10-12T07:23:20+00:00",
    "checkoutUrl": null
  },
  "tasks": [{ "id": "…", "areaName": "Phòng khách", "description": "Quét và lau sàn", "sortOrder": 1, "status": "Pending" }],
  "timeline": [{ "fromStatus": "PendingDeposit", "toStatus": "PendingDeposit", "reason": "Created", "note": null, "atUtc": "2026-10-12T07:08:20+00:00" }],
  "allowedActions": ["PayDeposit", "Cancel"],
  "serverTimeUtc": "2026-10-12T07:08:21+00:00"
}
```

- `depositPayment` là giao dịch cọc gần nhất (`null` khi chưa khởi tạo), cùng cấu trúc với response của `/deposit`. Sau khi tải lại trang, FE lấy `paymentId` ở đây để tiếp tục giao dịch đang `Pending`. `depositPaymentStatus` giữ lại để tương thích.
- `serverTimeUtc` là giờ server lúc trả response, dùng để hiệu chỉnh đồng hồ đếm ngược `holdExpiresAtUtc`.
- `timeline[].reason` là mã ổn định; `timeline[].note` là ghi chú tự do của người hủy/từ chối (có thể `null`).

| `reason` | Ý nghĩa |
| --- | --- |
| `Created` | Khách tạo đơn. |
| `DepositPaid` | Đặt cọc thành công, chờ người làm xác nhận. |
| `AutoConfirmed` | Đặt cọc thành công và người làm bật tự nhận đơn. |
| `ProviderConfirmed` | Người làm xác nhận. |
| `ProviderRejected` | Người làm từ chối (`note` là lý do nếu có). |
| `CheckedIn` | Người làm quét mã QR check-in, bắt đầu ca. |
| `WorkStarted` | Người làm tải ảnh Before đầu tiên. |
| `WorkFinished` | Người làm hoàn tất ca, chờ khách nghiệm thu. |
| `CustomerCancelled` | Khách hủy (`note` là lý do nếu có). |
| `HoldExpired` | Hết thời gian giữ chỗ khi chưa cọc. |
| `ConfirmationExpired` | Người làm không phản hồi trước hạn. |

Danh sách mã sẽ dài thêm khi có các bước sau `Confirmed`; FE nên có nhánh mặc định cho mã chưa biết.

`GET /bookings` nhận `status`, `page`, `pageSize` (tối đa 50) và trả `{ items, page, pageSize, totalItems, totalPages }` với các trường tóm tắt, sắp theo `startsAtUtc` giảm dần. `status` là tên enum dạng chuỗi, ví dụ `status=PendingProviderConfirmation` (đúng cho cả `/bookings` và `/freelancer/bookings`); giá trị không hợp lệ trả 400 `VALIDATION_FAILED`.

## Đặt cọc

`POST /bookings/{id}/deposit` với header bắt buộc `Idempotency-Key` (1–100 ký tự) và body `{ "method": "Momo" }` (`Momo`, `VietQr`, `ZaloPay`, `Card`, `BankTransfer`, …).

```json
{
  "paymentId": "e3bc5193-8ddd-4eda-8152-5e5a24e305d5",
  "status": "Pending",
  "amountVnd": 30000,
  "method": "Momo",
  "expiresAtUtc": "2026-10-12T07:23:20+00:00",
  "checkoutUrl": null
}
```

Gọi lại cùng `Idempotency-Key` trả về cùng giao dịch. Mỗi đơn chỉ có một giao dịch cọc đang chờ: nếu đơn đã có giao dịch `Pending` mà FE gọi `/deposit` với `Idempotency-Key` mới, server **trả lại giao dịch đang chờ** (200, cùng `paymentId`), không tạo thêm và không báo lỗi; `method` của request mới bị bỏ qua. Giao dịch mới chỉ được tạo sau khi giao dịch trước đã `Failed`.

Hiện chỉ có cổng giả lập. Để chốt kết quả, gọi `POST /payments/sandbox/{paymentId}/confirm` với `{ "succeeded": true }` hoặc `false`. Endpoint này chỉ tồn tại khi `Payments:SandboxEnabled = true` (mặc định bật ở môi trường Development), ngược lại trả 404. Thanh toán thất bại giữ đơn ở `PendingDeposit` để thử lại trong thời gian giữ chỗ. Gọi chốt nhiều lần không đổi kết quả đã chốt.

## Hủy đơn

`GET /bookings/{id}/cancellation-preview`:

```json
{ "freeCancellation": false, "refundAmountVnd": 0, "retainedAmountVnd": 24000, "freeCancellationUntilUtc": "2026-10-12T05:30:00+00:00" }
```

`POST /bookings/{id}/cancel` với body `{ "reason": "Đổi lịch" }` (có thể gửi `{}`), trả chi tiết đơn.

| Trạng thái lúc hủy | Kết quả |
| --- | --- |
| `PendingDeposit` | Không phát sinh tiền. |
| `PendingProviderConfirmation` | Hoàn đủ cọc. |
| `Confirmed`, còn từ 2 giờ trở lên trước giờ bắt đầu | Hoàn đủ cọc. |
| `Confirmed`, còn dưới 2 giờ | Không hoàn cọc: 10% giá gốc cho người làm, phần còn lại cho nền tảng. |
| Khác | 409 `BOOKING_NOT_CANCELLABLE`. |

## Mã QR check-in

`POST /bookings/{id}/check-in-token` trả `{ "token": "…", "expiresAtUtc": "…", "serverTimeUtc": "…" }` (`serverTimeUtc` để hiệu chỉnh đồng hồ đếm ngược). Chỉ dùng được khi đơn `Confirmed` và trong khoảng từ 30 phút trước giờ bắt đầu đến giờ kết thúc. Mã sống 120 giây; lấy mã mới làm mã cũ hết hạn ngay. FE tự vẽ QR từ `token` và gọi lại khi sắp hết hạn. Server chỉ lưu hash của mã.

## Check-in, ảnh tiến độ và hoàn tất ca (phía freelancer)

### POST `/freelancer/bookings/{id}/check-in`

Body `{ "token": "…" }` — token quét từ QR của khách. Chỉ nhận khi đơn `Confirmed` và trong khung check-in (từ 30 phút trước giờ bắt đầu đến giờ kết thúc). Token dùng một lần; hết hạn hoặc đã dùng trả 409. Thành công: đơn chuyển `CheckedIn`, lưu `checkedInAtUtc`, thông báo khách.

### PATCH `/freelancer/bookings/{id}/tasks/{taskId}`

Body `{ "status": "Completed" }` — chỉ nhận `Pending`, `InProgress`, `Completed`. Chỉ đổi được khi đơn `CheckedIn` hoặc `InProgress`. Đặt `Completed` ghi `completedAtUtc`; các trạng thái khác xóa mốc này. Mục không thuộc đơn trả 404 `BOOKING_TASK_NOT_FOUND`.

### POST `/freelancer/bookings/{id}/photos`

`multipart/form-data` gồm `file` (JPG/PNG thật, ≤ 5 MB, nhận diện bằng magic bytes), `type` (`Before` hoặc `After`), `bookingTaskId` (tùy chọn, gắn ảnh vào một mục checklist) và `caption` (tùy chọn, ≤ 300 ký tự). Chỉ tải được khi đơn `CheckedIn` hoặc `InProgress`. **Ảnh `Before` đầu tiên** chuyển đơn sang `InProgress` và thông báo khách. Ảnh lưu ngoài `wwwroot`, chỉ phát qua endpoint có phân quyền; `photos[].contentUrl` trong chi tiết đơn là đường dẫn tương đối cần access token.

### POST `/freelancer/bookings/{id}/complete`

Không body. Chỉ nhận khi đơn `CheckedIn` hoặc `InProgress` **và đã có ít nhất một ảnh `After`** (nếu chưa có trả 409 `AFTER_PHOTO_REQUIRED`). Không bắt buộc mọi mục checklist đã `Completed`. Thành công: đơn chuyển `PendingAcceptance` (lý do `WorkFinished`) và thông báo khách chờ nghiệm thu.

## Phía freelancer

- `POST /freelancer/availability` body `{ "startsAtUtc": "…", "endsAtUtc": "…" }`: theo bước 30 phút, tối thiểu 2 giờ, ở tương lai, không chồng khoảng rảnh khác. Chỉ hồ sơ đã duyệt.
- `GET /freelancer/availability?from=YYYY-MM-DD&days=14` (tối đa 60 ngày).
- `DELETE /freelancer/availability/{id}`: không xóa được khi có đơn đang chiếm chỗ trong khoảng đó.
- `GET /freelancer/bookings`: không gồm đơn chưa từng có cọc. Trả `customerName`, `areaLabel` ("Quận 1, Hồ Chí Minh"), hạn xác nhận và `allowedActions`.
- `GET /freelancer/bookings/{id}`: `addressSnapshot` chỉ có giá trị từ khi đơn `Confirmed`; trước đó là `null`. Không bao giờ có số điện thoại khách. Có `allowedActions`, `timeline` và `serverTimeUtc`.
- `allowedActions` phía freelancer: `Confirm` và `Reject` khi đơn `PendingProviderConfirmation`; quá hạn xác nhận thì chỉ còn `Reject` cho tới khi job chuyển đơn sang `Expired`. `CheckIn` khi đơn `Confirmed` và trong khung check-in. `UploadPhotos` và `CompleteWork` khi đơn `CheckedIn` hoặc `InProgress`. Các trạng thái khác trả mảng rỗng.
- `GET /freelancer/bookings/counts`: số đơn theo từng trạng thái cho các tab, cùng phạm vi với danh sách (không có `PendingDeposit`):

  ```json
  { "counts": { "PendingProviderConfirmation": 1, "Confirmed": 0, "CheckedIn": 0, "InProgress": 0, "PendingAcceptance": 0, "Completed": 0, "Expired": 1, "Rejected": 1, "Cancelled": 2, "Disputed": 0 }, "total": 5 }
  ```
- `POST /freelancer/bookings/{id}/reject` body `{ "reason": "…" }` (có thể gửi `{}`).

## Mã lỗi

| HTTP | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Body sai định dạng. |
| 400 | `INVALID_TIME_RANGE` | Giờ lệch bước 30 phút hoặc kết thúc trước bắt đầu. |
| 400 | `DURATION_TOO_SHORT` / `DURATION_TOO_LONG` | Thời lượng ngoài giới hạn. |
| 400 | `START_TOO_SOON` / `START_TOO_FAR` | Giờ bắt đầu quá sát hoặc quá xa. |
| 400 | `INVALID_CHECKLIST_ITEM` | Mục checklist không thuộc dịch vụ. |
| 400 | `IDEMPOTENCY_KEY_REQUIRED` | Thiếu header `Idempotency-Key`. |
| 401 | `SESSION_EXPIRED` | `/auth/refresh` không có cookie, phiên hết hạn hoặc đã đăng xuất. |
| 404 | `BOOKING_NOT_FOUND` | Không có đơn, hoặc đơn không thuộc về người gọi. |
| 404 | `ADDRESS_NOT_FOUND`, `PAYMENT_NOT_FOUND`, `AVAILABILITY_NOT_FOUND`, `CHECKLIST_NOT_FOUND`, `FREELANCER_NOT_FOUND` | Không tìm thấy tài nguyên. |
| 409 | `PROVIDER_NOT_BOOKABLE` | Đối tác chưa duyệt, ngừng nhận đơn hoặc là công ty. |
| 409 | `SERVICE_NOT_OFFERED` | Đối tác không cung cấp dịch vụ. |
| 409 | `ADDRESS_OUT_OF_SERVICE_AREA` | Địa chỉ ngoài khu vực phục vụ. |
| 409 | `OUTSIDE_AVAILABILITY` | Khung giờ không nằm trọn trong lịch rảnh. |
| 409 | `SLOT_UNAVAILABLE` | Khung giờ đã có người đặt. |
| 409 | `TOO_MANY_PENDING_BOOKINGS` | Quá số đơn đang chờ cọc. |
| 409 | `ADDRESS_LIMIT_REACHED` | Đã đủ 10 địa chỉ. |
| 409 | `HOLD_EXPIRED` | Hết thời gian giữ chỗ khi đặt cọc. |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Khóa đã dùng cho đơn khác. |
| 409 | `BOOKING_INVALID_STATE` | Thao tác không hợp với trạng thái đơn. |
| 409 | `BOOKING_NOT_CANCELLABLE` | Đơn không còn hủy được. |
| 409 | `CONFIRMATION_EXPIRED` | Freelancer xác nhận sau hạn. |
| 409 | `CHECK_IN_NOT_OPEN` | Chưa tới hoặc đã qua khung giờ check-in. |
| 409 | `CHECK_IN_TOKEN_INVALID` / `CHECK_IN_TOKEN_EXPIRED` | Token check-in không hợp lệ, đã dùng hoặc hết hạn. |
| 409 | `AFTER_PHOTO_REQUIRED` | Hoàn tất ca khi chưa có ảnh After. |
| 400 | `INVALID_TASK_STATUS` | Trạng thái mục checklist không hợp lệ (chỉ `Pending`/`InProgress`/`Completed`). |
| 400 | `INVALID_PHOTO_TYPE` | Loại ảnh không phải `Before`/`After`. |
| 400 | `EMPTY_FILE` | Tệp ảnh rỗng. |
| 413 | `FILE_TOO_LARGE` | Ảnh vượt 5 MB. |
| 415 | `UNSUPPORTED_IMAGE` | Tệp không phải JPG/PNG thật. |
| 404 | `BOOKING_TASK_NOT_FOUND`, `BOOKING_PHOTO_NOT_FOUND` | Không tìm thấy mục checklist hoặc ảnh trong đơn. |
| 409 | `PROVIDER_NOT_APPROVED`, `AVAILABILITY_OVERLAP`, `AVAILABILITY_IN_USE` | Lỗi khi freelancer quản lý lịch rảnh. |

## Dữ liệu thử

```powershell
powershell -File scripts/seed-fake-freelancers.ps1    # 8 freelancer, 5 người đã duyệt
powershell -File scripts/seed-booking-fixtures.ps1    # lịch rảnh 08-12h và 13-18h giờ VN trong 14 ngày + khách hàng thử
```

- Khách hàng thử: số `0920000001`, mật khẩu `Customer-dev-2026`, có sẵn một địa chỉ ở Quận 1, Hồ Chí Minh. Không cần qua OTP.
- Freelancer thử: số `0910000001`, mật khẩu `Freelancer-dev-2026`, phục vụ Quận 1 / Quận 3 / Quận 7 (Hồ Chí Minh).
- Checklist có sẵn cho `CLEANING`, `DEEP_CLEANING`, `COOKING`, `LAUNDRY`.

`GET /freelancers/{providerId}` trả `services[].serviceCategoryId`, dùng thẳng làm `serviceCategoryId` khi báo giá và tạo đơn.

## Trả lời `BOOKING_BACKEND_REQUESTS.md`

| # | Yêu cầu | Trạng thái |
| --- | --- | --- |
| 1 | Khôi phục phiên | Xong: `POST /auth/refresh` bằng cookie `HttpOnly`, kèm `POST /auth/logout`. Xem "Đăng nhập và phiên". |
| 2 | Endpoint xác thực freelancer | Đã chốt: dùng các đường dẫn trong Swagger, không có `/auth/freelancers/*`. Login trả `freelancerOnboarding.currentStep` và `kycStatus`. |
| 3 | Domain API production | Chưa xử lý: đây là việc cấu hình Railway/DNS, không nằm trong code. |
| 4 | Giao dịch cọc trong chi tiết đơn | Xong: `depositPayment`. Gọi `/deposit` với khóa mới khi đang có giao dịch `Pending` thì nhận lại giao dịch đó. |
| 5 | `id` danh mục trong hồ sơ freelancer | Xong: `services[].serviceCategoryId`. |
| 6 | `allowedActions` phía freelancer | Xong ở cả danh sách và chi tiết: `Confirm`, `Reject`. |
| 7 | Giới hạn request cho báo giá | `POST /bookings/quote` không bị giới hạn tần suất. |
| 8 | Danh sách giá trị | `role`: `Customer`, `Freelancer`, `Company`, `Admin`. `timeline[].reason`: bảng mã ở mục "Chi tiết đơn". `status` trong query: tên enum dạng chuỗi, đã thử ở cả hai phía. |
| 9 | Tài khoản khách hàng thử | Xong: `0920000001` trong `seed-booking-fixtures.ps1`. |
| 10 | Các bước sau `Confirmed` | Xong: check-in QR, ảnh Before/After, cập nhật checklist, hoàn tất ca (→ `PendingAcceptance`). Còn lại: nghiệm thu phía khách (→ `Completed`) và gia hạn ca. |
| 11 | Cổng thanh toán thật | Chưa làm, PSP chưa chốt. `/payments/sandbox/*` trả 404 khi `Payments:SandboxEnabled` tắt. |
| 12 | Cải thiện nhỏ | Xong: `serverTimeUtc` (chi tiết đơn, mã QR), `GET /freelancer/bookings/counts`, `customerProfile.avatarUrl` trong login. Chưa có: kênh đẩy SSE/WebSocket (FE tiếp tục gọi lại định kỳ), hạng thành viên (chưa có trong mô hình dữ liệu). |

Thay đổi có thể ảnh hưởng FE hiện tại: `timeline[].reason` giờ là mã thay cho câu tiếng Việt, và lý do tự do của người dùng chuyển sang `timeline[].note`.
