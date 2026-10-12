# Company Onboarding API

Luồng: đăng ký tài khoản công ty (OTP) → đăng nhập → khai pháp nhân → khai người đại diện → chọn khu vực nhận ca → tải giấy tờ → gửi duyệt → admin duyệt / yêu cầu chỉnh sửa / từ chối.

Base URL: `/api/v1`. Lỗi trả `ProblemDetails` kèm `code` và `traceId`. Một tài khoản quản lý đúng một công ty.

Chưa hỗ trợ: quản lý người làm của công ty, bảng giá/dịch vụ, tài khoản ngân hàng, hiển thị công ty trong danh bạ công khai và đặt lịch với công ty. Hồ sơ được duyệt vẫn chưa nhận đơn (`isAcceptingBookings = false`).

## Tổng hợp endpoint

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/auth/register-company` | public | Cùng body/response với `/auth/register`; tạo tài khoản `Company`. `fullName` là họ tên người quản lý tài khoản. |
| POST | `/auth/verify-phone-otp`, `/auth/resend-phone-otp` | public | Dùng chung với khách hàng và freelancer. |
| POST | `/auth/login`, `/auth/refresh` | public / cookie | Trả `role = "Company"` kèm `companyOnboarding`. |
| GET | `/company/onboarding` | Company | Toàn bộ trạng thái hồ sơ. |
| PUT | `/company/onboarding/company` | Company | Lưu thông tin pháp nhân. |
| PUT | `/company/onboarding/representative` | Company | Lưu người đại diện. |
| PUT | `/company/onboarding/service-areas` | Company | Thay danh sách khu vực nhận ca. |
| PUT | `/company/onboarding/documents/{type}` | Company | Tải lên/thay một giấy tờ (`multipart/form-data`, trường `file`). |
| GET | `/company/onboarding/documents/{documentId}/content` | Company | Nội dung giấy tờ của chính mình. |
| POST | `/company/onboarding/submit` | Company | Gửi duyệt. |
| GET | `/admin/companies?status=&page=&pageSize=` | Admin | Bỏ trống `status` = hàng chờ (Submitted + UnderReview), cũ nhất trước. |
| GET | `/admin/companies/{providerId}` | Admin | Chi tiết, kèm số CCCD người đại diện đã giải mã. Ghi audit. |
| POST | `/admin/companies/{providerId}/start-review` | Admin | Submitted → UnderReview. |
| POST | `/admin/companies/{providerId}/decision` | Admin | Duyệt / từ chối / yêu cầu chỉnh sửa. |
| GET | `/admin/companies/{providerId}/documents/{documentId}/content` | Admin | Nội dung giấy tờ. Ghi audit. |

Các `PUT` và `submit` của công ty dùng chung giới hạn 30 request / 10 phút / người dùng.

## Trạng thái và bước

```text
NotStarted ──lưu pháp nhân──▶ Draft ──gửi duyệt──▶ Submitted ──admin mở──▶ UnderReview ──▶ Approved | Rejected
                               ▲                                                │
                               └──────────── ChangesRequested ◀─────────────────┘
```

Chỉ sửa được khi `Draft` hoặc `Rejected` (`canEdit = true`); trạng thái khác trả 409 `COMPANY_NOT_EDITABLE`. FE điều hướng theo `currentStep`, có trong cả response đăng nhập và mọi response onboarding:

| `currentStep` | Ý nghĩa |
| --- | --- |
| `Company` | Chưa khai thông tin pháp nhân. |
| `Representative` | Chưa khai người đại diện. |
| `ServiceAreas` | Chưa chọn khu vực nhận ca. |
| `Documents` | Thiếu giấy tờ bắt buộc, hoặc có giấy tờ bị từ chối cần tải lại. |
| `Submit` | Đủ hồ sơ, chưa gửi duyệt. |
| `AwaitingReview` | Đã gửi, chờ admin. |
| `Completed` | Đã duyệt. |
| `Suspended` | Bị tạm ngưng. |

Khi đăng nhập:

```json
"companyOnboarding": { "providerId": "bf406817-39ff-4439-ad61-79b1f25bb40a", "status": "Draft", "currentStep": "Documents" }
```

`status` là `NotStarted` hoặc `Draft`, `Submitted`, `UnderReview`, `Approved`, `Rejected`, `Suspended`. `providerId` là `null` khi chưa bắt đầu.

## Response onboarding

Mọi endpoint `/company/onboarding/*` (trừ tải nội dung file) trả cùng một cấu trúc:

```json
{
  "status": "Draft",
  "currentStep": "Documents",
  "canEdit": true,
  "providerId": "bf406817-39ff-4439-ad61-79b1f25bb40a",
  "company": {
    "legalName": "Công ty TNHH Sạch Nhà",
    "displayName": "Sạch Nhà",
    "taxCode": "0312345678",
    "hotline": "19001234",
    "headquartersAddress": "12 Lê Lợi, Quận 1, Hồ Chí Minh",
    "workforceSize": "TenToThirty",
    "liabilityCoverageVnd": null
  },
  "representative": {
    "fullName": "Trần Văn An",
    "jobTitle": "Giám đốc",
    "workEmail": "an@sachnha.vn",
    "phoneNumber": "0901234567",
    "identityNumberLast4": "1234"
  },
  "serviceAreas": [{ "id": "bbbbbbbb-0000-0000-0000-000000000001", "city": "Hồ Chí Minh", "district": "Quận 1", "wardOrComplex": null }],
  "requiredDocumentTypes": ["BusinessRegistration", "CitizenIdFront", "CitizenIdBack"],
  "documents": [
    { "id": "…", "type": "BusinessRegistration", "status": "Rejected", "originalFileName": "dkkd.pdf", "contentType": "application/pdf", "uploadedAtUtc": "…", "rejectionReason": "Bản ĐKKD bị mờ, vui lòng tải lại." }
  ],
  "feedback": { "decision": "ChangesRequested", "reason": "Bản ĐKKD bị mờ, vui lòng tải lại.", "decidedAtUtc": "…" },
  "submittedAtUtc": "…"
}
```

Khi chưa có hồ sơ: `status = "NotStarted"`, các object là `null`, các mảng rỗng. Số CCCD của người đại diện không bao giờ được trả đầy đủ cho công ty.

## Các bước khai hồ sơ

### PUT `/company/onboarding/company`

```json
{
  "legalName": "Công ty TNHH Sạch Nhà",
  "displayName": "Sạch Nhà",
  "taxCode": "0312345678",
  "hotline": "1900 1234",
  "headquartersAddress": "12 Lê Lợi, Quận 1, Hồ Chí Minh",
  "workforceSize": "TenToThirty",
  "liabilityCoverageVnd": 500000000
}
```

| Trường | Quy tắc |
| --- | --- |
| `legalName` | 2–200 ký tự. |
| `displayName` | 2–120 ký tự, là tên hiển thị với khách. |
| `taxCode` | 10 chữ số, hoặc 10 chữ số + `-` + 3 chữ số (chi nhánh). Khoảng trắng được bỏ. Trùng công ty khác → 409 `TAX_CODE_ALREADY_USED`. |
| `hotline` | Số di động/cố định Việt Nam, hoặc tổng đài `1800`/`1900` (8–10 chữ số). |
| `headquartersAddress` | 5–300 ký tự. |
| `workforceSize` | `UnderTen`, `TenToThirty`, `OverThirty`. |
| `liabilityCoverageVnd` | Tùy chọn. Lớn hơn 0 thì giấy tờ `LiabilityInsurance` thành bắt buộc. |

Lần lưu đầu tiên tạo hồ sơ ở trạng thái `Draft`. Các bước sau cần có bước này trước, nếu không trả 409 `COMPANY_PROFILE_REQUIRED`.

### PUT `/company/onboarding/representative`

```json
{ "fullName": "Trần Văn An", "jobTitle": "Giám đốc", "workEmail": "an@sachnha.vn", "phoneNumber": "0901234567", "identityNumber": "079195001234" }
```

`identityNumber` là CCCD đúng 12 chữ số, được mã hóa khi lưu. Mỗi lần sửa phải gửi lại đủ các trường, kể cả số CCCD.

### PUT `/company/onboarding/service-areas`

```json
{ "serviceAreaIds": ["bbbbbbbb-0000-0000-0000-000000000001", "bbbbbbbb-0000-0000-0000-000000000002"] }
```

1–20 id lấy từ `GET /catalog/service-areas`. Danh sách gửi lên thay toàn bộ danh sách cũ.

### PUT `/company/onboarding/documents/{type}`

| `type` | Bắt buộc | Định dạng |
| --- | --- | --- |
| `BusinessRegistration` | Có | JPG, PNG hoặc PDF |
| `CitizenIdFront` | Có (người đại diện) | JPG hoặc PNG |
| `CitizenIdBack` | Có (người đại diện) | JPG hoặc PNG |
| `LiabilityInsurance` | Khi `liabilityCoverageVnd > 0` | JPG, PNG hoặc PDF |

Tối đa 10 MB mỗi file. Định dạng được xác định từ nội dung file, không dựa vào đuôi file. Tải lại cùng `type` sẽ thay bản cũ. `requiredDocumentTypes` trong response cho biết danh sách bắt buộc hiện tại.

`GET /company/onboarding/documents/{documentId}/content` trả `image/jpeg`, `image/png` hoặc `application/pdf`; cần access token nên FE `fetch` kèm header rồi dùng blob URL.

### POST `/company/onboarding/submit`

Không body. Chỉ gửi được khi `currentStep = Submit`; thiếu phần nào trả 409 với mã tương ứng.

## Admin

`POST /admin/companies/{providerId}/decision`:

```json
{ "decision": "ChangesRequested", "reason": "Bản ĐKKD bị mờ, vui lòng tải lại.", "rejectedDocumentTypes": ["BusinessRegistration"] }
```

- `decision`: `Approved`, `Rejected` hoặc `ChangesRequested`. Lý do tối thiểu 10 ký tự trừ khi duyệt.
- `Approved`: mọi giấy tờ thành `Verified`, hồ sơ thành `Approved`.
- `ChangesRequested`: hồ sơ về `Draft`; `Rejected`: hồ sơ thành `Rejected`. Cả hai đánh dấu các loại trong `rejectedDocumentTypes` là `Rejected` kèm lý do, và công ty được sửa rồi gửi lại.
- Chỉ quyết định được khi hồ sơ `Submitted` hoặc `UnderReview`.

Chi tiết cho admin có thêm `ownerPhoneNumber`, `ownerFullName` và `representative.identityNumber` (số CCCD đầy đủ). `/admin/kyc/*` chỉ trả freelancer, `/admin/companies/*` chỉ trả công ty.

## Mã lỗi

| HTTP | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Body sai định dạng hoặc độ dài. |
| 400 | `INVALID_TAX_CODE`, `INVALID_HOTLINE`, `INVALID_PHONE_NUMBER`, `INVALID_COMPANY_NAME` | Giá trị không hợp lệ. |
| 400 | `INVALID_SERVICE_AREA` | Khu vực không tồn tại, ngừng hoạt động, hoặc sai số lượng. |
| 400 | `INVALID_DOCUMENT_TYPE`, `EMPTY_FILE` | Loại giấy tờ không thuộc luồng công ty; file rỗng. |
| 400 | `INVALID_DECISION`, `REASON_REQUIRED` | Quyết định của admin không hợp lệ hoặc thiếu lý do. |
| 404 | `COMPANY_NOT_FOUND`, `DOCUMENT_NOT_FOUND` | Không tìm thấy hồ sơ/giấy tờ, hoặc không thuộc về người gọi. |
| 409 | `TAX_CODE_ALREADY_USED` | Mã số thuế đã thuộc công ty khác. |
| 409 | `COMPANY_NOT_EDITABLE` | Hồ sơ đã gửi duyệt hoặc đã duyệt. |
| 409 | `COMPANY_PROFILE_REQUIRED`, `COMPANY_REPRESENTATIVE_REQUIRED`, `COMPANY_SERVICE_AREAS_REQUIRED` | Thiếu bước trước. |
| 409 | `COMPANY_DOCUMENTS_MISSING`, `COMPANY_DOCUMENT_REJECTED` | Thiếu giấy tờ bắt buộc; còn giấy tờ bị từ chối chưa tải lại. |
| 409 | `COMPANY_REVIEW_INVALID_STATE` | Admin thao tác khi hồ sơ không ở trạng thái chờ duyệt. |
| 413 | `FILE_TOO_LARGE` | File lớn hơn 10 MB. |
| 415 | `UNSUPPORTED_DOCUMENT` | Nội dung không phải định dạng cho phép của loại giấy tờ đó. |

## Bảo mật

- Số CCCD người đại diện mã hóa bằng ASP.NET Data Protection (cùng cơ chế với KYC freelancer, xem `FREELANCER_KYC_API.md`); không ghi vào log hay audit.
- Giấy tờ lưu ngoài wwwroot qua `IFileStorage`, chỉ phát qua endpoint có phân quyền với `Cache-Control: private, no-store`.
- Audit: `company.submitted`, `company.viewed`, `company.review_started`, `company.document_viewed`, `company.approved`, `company.rejected`, `company.changesrequested`.

## Thử nhanh

Môi trường Development trả `developmentOtpCode` khi đăng ký nên không cần SMS. Admin thử: số `0900000001`, mật khẩu `Abstrict-dev-admin-2026`.
