# Freelancer auth API

Tài liệu cho frontend gọi API xác thực/đăng ký tài khoản freelancer.

Tất cả route dùng tiền tố `/api/v1/auth/freelancers`.

| Method | Path | Mô tả | Rate limit (theo IP) |
| --- | --- | --- | --- |
| `POST` | `/register` | Đăng ký tài khoản freelancer + gửi OTP | 5 request / 10 phút |
| `POST` | `/verify-phone-otp` | Xác thực số điện thoại bằng OTP | 10 request / 1 phút |
| `POST` | `/resend-phone-otp` | Gửi lại OTP xác thực | 3 request / 10 phút |
| `POST` | `/login` | Đăng nhập freelancer | 10 request / 1 phút |

## Quy ước chung

- `Content-Type: application/json` cho tất cả request.
- Các endpoint này **công khai**, không cần `Authorization`.
- `phoneNumber` được chuẩn hóa theo số điện thoại Việt Nam: nhận 10 hoặc 11 chữ số; `+84…` / `84…` được đổi thành `0…`. Chuỗi rỗng/sai định dạng bị từ chối.
- Mọi lỗi trả về theo `ProblemDetails`:

  ```json
  {
    "status": 400,
    "title": "INVALID_OTP",
    "detail": "Mã OTP không chính xác.",
    "code": "INVALID_OTP",
    "traceId": "0HN..."
  }
  ```

  `title` và `code` luôn giống nhau. Lỗi validate model trả về `ValidationProblemDetails` với `code: "VALIDATION_FAILED"` và object `errors`.
- Lỗi `429` do rate limit (khung giờ) hoặc do cooldown gửi lại OTP sẽ kèm header `Retry-After` (giây).

## Luồng đăng ký

```
POST /register  ──►  202 { userId, challengeId, expiresAtUtc, resendAvailableAtUtc }
        │
        ▼
POST /verify-phone-otp  ──►  200 { userId, status: "Active", phoneVerifiedAtUtc }
        │
        ▼
POST /login  ──►  200 { accessToken, tokenType: "Bearer", ... }
```

## Đăng ký

`POST /register` → `202 Accepted`

```json
{
  "fullName": "Nguyễn Thu Hà",
  "phoneNumber": "090 123 4567",
  "password": "abc12345",
  "confirmPassword": "abc12345",
  "acceptTermsAndPrivacy": true
}
```

Ràng buộc:

| Trường | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `fullName` | Có | 2–160 ký tự (được trim) |
| `phoneNumber` | Có | 9–24 ký tự, được chuẩn hóa về 10–11 chữ số VN |
| `password` | Có | 8–128 ký tự, chứa ít nhất 1 chữ cái và 1 chữ số |
| `confirmPassword` | Có | 8–128 ký tự, phải khớp `password` |
| `acceptTermsAndPrivacy` | Có | phải là `true` |

Hành vi:

- Tạo `User` (role `Freelancer`, status `Pending`), `Provider` và `FreelancerApplication` (status `Draft`, step `Personal`) trong một transaction.
- Sinh OTP 6 chữ số: hết hạn sau **5 phút**, được gửi lại sau **45 giây**, tối đa **5 lần gửi / số điện thoại / 24 giờ**.
- Mật khẩu lưu bằng ASP.NET Core password hasher; không bao giờ trả về.

Response `202`:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "challengeId": "00000000-0000-0000-0000-000000000000",
  "phoneNumber": "0901234567",
  "expiresAtUtc": "2026-09-29T10:05:00+00:00",
  "resendAvailableAtUtc": "2026-09-29T10:00:45+00:00",
  "developmentOtpCode": "123456"
}
```

`developmentOtpCode` chỉ có ở môi trường Development khi `Otp:ExposeCodeToClient = true`; production không bao giờ trả mã.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Sai định dạng/thiếu trường |
| 400 | `PASSWORD_CONFIRMATION_MISMATCH` | `confirmPassword` ≠ `password` |
| 400 | `TERMS_NOT_ACCEPTED` | `acceptTermsAndPrivacy` = `false` |
| 400 | `INVALID_FULL_NAME` | Tên < 2 ký tự sau khi trim |
| 400 | `INVALID_PHONE_NUMBER` | Số điện thoại không chuẩn hóa được |
| 409 | `PHONE_ALREADY_REGISTERED` | Số điện thoại đã tồn tại |
| 429 | `OTP_SEND_LIMIT_EXCEEDED` | Vượt 5 lần gửi OTP/ngày |
| 503 | `OTP_DELIVERY_UNAVAILABLE` | Chưa cấu hình dịch vụ gửi OTP |

## Xác thực số điện thoại

`POST /verify-phone-otp` → `200 OK`

```json
{ "challengeId": "00000000-0000-0000-0000-000000000000", "code": "123456" }
```

- `code` phải đúng **6 chữ số**.
- Sai tối đa **5 lần** thì challenge bị khóa và tiêu hủy.
- OTP đã dùng/hết hạn không dùng lại được; phải gọi `/resend-phone-otp`.
- Thành công: challenge bị consume, `PhoneVerifiedAtUtc` được set, user chuyển `Active`.

Response `200`:

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "status": "Active",
  "phoneVerifiedAtUtc": "2026-09-29T10:02:00+00:00"
}
```

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | `code` không đúng 6 chữ số |
| 400 | `INVALID_OTP` | Mã OTP sai (còn lượt thử) |
| 404 | `OTP_CHALLENGE_NOT_FOUND` | Không tìm thấy challenge |
| 409 | `OTP_ALREADY_USED` | OTP đã được dùng |
| 409 | `FREELANCER_REGISTRATION_NOT_PENDING` | Tài khoản không còn ở trạng thái `Pending` |
| 410 | `OTP_EXPIRED` | OTP hết hạn |
| 429 | `OTP_ATTEMPTS_EXCEEDED` | Sai quá 5 lần |

## Gửi lại OTP

`POST /resend-phone-otp` → `202 Accepted`

```json
{ "challengeId": "00000000-0000-0000-0000-000000000000" }
```

- Trả về challenge mới cùng shape với `/register`.
- Áp dụng cooldown **45 giây** và giới hạn **5 lần gửi/ngày** mỗi số điện thoại.
- Challenge cũ chưa dùng bị consume.

Response `202`: giống response của `/register` (có thể kèm `developmentOtpCode` ở Development).

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | `challengeId` không hợp lệ |
| 404 | `OTP_CHALLENGE_NOT_FOUND` | Không tìm thấy challenge |
| 429 | `OTP_RESEND_COOLDOWN` | Chưa hết 45 giây (kèm `Retry-After`) |
| 429 | `OTP_SEND_LIMIT_EXCEEDED` | Vượt 5 lần gửi/ngày |
| 503 | `OTP_DELIVERY_UNAVAILABLE` | Chưa cấu hình dịch vụ gửi OTP |

## Đăng nhập

`POST /login` → `200 OK`

```json
{
  "phoneNumber": "090 123 4567",
  "password": "abc12345"
}
```

Chỉ đăng nhập được khi: đúng số điện thoại + mật khẩu, role là `Freelancer`, tài khoản `Active` và đã xác thực số điện thoại. Sai thông tin, tài khoản chưa kích hoạt hoặc chưa xác thực đều trả cùng một lỗi `401` để tránh dò tài khoản.

Response `200`:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-09-29T10:15:00+00:00",
  "userId": "00000000-0000-0000-0000-000000000000",
  "fullName": "Nguyễn Thu Hà",
  "phoneNumber": "0901234567",
  "role": "Freelancer",
  "onboardingStatus": "Draft",
  "currentStep": "Personal"
}
```

- Access token là JWT, **sống 15 phút**. Gửi kèm ở các API cần xác thực: `Authorization: Bearer <accessToken>`.
- `onboardingStatus` ∈ `Draft`, `Submitted`, `UnderReview`, `ChangesRequested`, `Approved`, `Rejected`.
- `currentStep` ∈ `Personal`, `Identity`, `SupportingDocuments`, `SkillsAndBank`.
- Nếu freelancer chưa có hồ sơ onboarding, `onboardingStatus` = `Draft` và `currentStep` = `Personal`.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu/sai định dạng trường |
| 401 | `INVALID_CREDENTIALS` | Sai số điện thoại/mật khẩu, chưa kích hoạt hoặc chưa xác thực |
| 429 | — | Vượt rate limit theo IP |

## Gợi ý cho frontend

- Lưu `challengeId` sau `/register` và `/resend-phone-otp` để gọi `/verify-phone-otp`.
- Dùng `resendAvailableAtUtc` để đếm ngược nút "Gửi lại OTP"; chỉ bật khi đã qua mốc này.
- Dùng `expiresAtUtc` để hiển thị thời gian hiệu lực còn lại của OTP.
- Sau khi verify thành công, chuyển sang màn đăng nhập (hoặc tự động gọi `/login`).
- Ở màn dev, có thể hiển thị `developmentOtpCode` để test nhanh; không dựa vào field này ở production.
- Token hết hạn sau 15 phút — cần có luồng refresh/đăng nhập lại.
- Điều hướng bước onboarding dựa trên `currentStep` sau khi đăng nhập.
