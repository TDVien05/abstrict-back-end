# Freelancer onboarding API

Tài liệu cho frontend gọi API **onboarding & KYC** của freelancer (wizard F1). Đăng ký tài khoản, OTP và đăng nhập nằm ở [`FREELANCER_AUTH_API.md`](./FREELANCER_AUTH_API.md).

Tất cả route dùng tiền tố `/api/v1/freelancers/me/onboarding` và **bắt buộc** JWT role `Freelancer`. Owner (`userId`) luôn suy ra từ token; client **không** gửi `userId`/`providerId`.

## Tổng quan endpoint

| Method | Path | Mô tả | Rate limit (theo IP) |
| --- | --- | --- | --- |
| `GET` | `/` | Lấy hồ sơ onboarding hiện tại (nháp + trạng thái + trường còn thiếu) | — |
| `PATCH` | `/` | Cập nhật một phần hồ sơ (thông tin cá nhân, kỹ năng, ngân hàng) | — |
| `POST` | `/consents` | Ghi nhận đồng thuận KYC | — |
| `POST` | `/documents` | Tải lên tài liệu xác minh (multipart) | — |
| `GET` | `/documents/{id}/content` | Tải nội dung tệp tài liệu | — |
| `DELETE` | `/documents/{id}` | Xóa tài liệu khỏi hồ sơ nháp | — |
| `POST` | `/identity/ocr` | Bắt đầu OCR CCCD (mặt trước + mặt sau) | 20 request / 1 phút |
| `POST` | `/identity/confirm` | Xác nhận (và đính chính) dữ liệu OCR | — |
| `POST` | `/identity/face-match` | Bắt đầu so khớp khuôn mặt selfie ↔ CCCD | 20 request / 1 phút |
| `GET` | `/operations/{id}` | Lấy trạng thái thao tác KYC bất đồng bộ | — |
| `POST` | `/submit` | Gửi hồ sơ để chờ duyệt | — |
| `POST` | `/reopen` | Mở lại hồ sơ bị từ chối/yêu cầu chỉnh sửa | — |

> Rate limit `kyc-operation` (20/1 phút/IP) chỉ áp cho `/identity/ocr` và `/identity/face-match`. Các endpoint còn lại không có limit riêng.

## Quy ước chung

- `Content-Type: application/json` cho mọi request JSON; riêng `/documents` dùng `multipart/form-data`.
- Gửi kèm ở mọi request: `Authorization: Bearer <accessToken>`.
- **Kiểm soát đồng thời (`PATCH`, `DELETE /documents/{id}`, `/identity/ocr`, `/submit`)**: gửi header `If-Match` với **số `version`** hiện tại của hồ sơ (số nguyên, có thể kèm dấu ngoặc kép — server tự bỏ). Nếu không khớp version hiện tại → `409 STALE_APPLICATION_VERSION`. Khuyến nghị luôn gửi; nếu bỏ qua, server không kiểm tra version.
- **Idempotency**: `/identity/ocr` và `/identity/face-match` **bắt buộc** header `Idempotency-Key` (chuỗi do client sinh, ví dụ UUID). Dùng lại cùng key với cùng body → trả lại operation cũ; dùng lại cùng key với body khác → `409 IDEMPOTENCY_KEY_REUSED`. Thiếu key → `400 VALIDATION_FAILED`.
- Hồ sơ chỉ cho sửa khi `status` ∈ `Draft`, `ChangesRequested`; ở trạng thái khác trả `409 APPLICATION_LOCKED`.
- Mọi lỗi trả về theo `ProblemDetails`:

  ```json
  {
    "status": 409,
    "title": "APPLICATION_LOCKED",
    "detail": "Hồ sơ đã được gửi và đang chờ thẩm định.",
    "code": "APPLICATION_LOCKED",
    "traceId": "0HN..."
  }
  ```

  `title` và `code` luôn giống nhau. Lỗi validate model trả `ValidationProblemDetails` với `code: "VALIDATION_FAILED"` và object `errors`.
- Lỗi `429` do rate limit sẽ kèm header `Retry-After` (giây).
- Nếu key mã hóa KYC chưa cấu hình, thao tác lưu số tài khoản/CCCD trả `503 KYC_SECURITY_NOT_CONFIGURED`.
- Thời gian là chuỗi ISO-8601 UTC; ngày là `YYYY-MM-DD` (`DateOnly`).

## Kiểu dữ liệu & enum

Các enum trong response là **chuỗi** (enum name), ví dụ `"Draft"`:

| Field | Giá trị |
| --- | --- |
| `status` (hồ sơ) | `Draft`, `Submitted`, `UnderReview`, `ChangesRequested`, `Approved`, `Rejected` |
| `currentStep` | `Personal`, `Identity`, `SupportingDocuments`, `SkillsAndBank` |
| `identity.ocrState` | `NotStarted`, `Processing`, `Extracted`, `NeedsReview`, `Failed` |
| `identity.faceState` | `NotStarted`, `Processing`, `Matched`, `NotMatched`, `NeedsReview`, `TechnicalError` |
| `identity.livenessState` | `NotPerformed`, `Performed`, `Failed` |
| `documents[].type` | `CitizenIdFront`, `CitizenIdBack`, `FaceVerification`, `HealthCertificate`, `CriminalRecord`, `BusinessRegistration`, `LiabilityInsurance`, `Other` |
| `documents[].status` | `Pending`, `Verified`, `Rejected`, `Expired` |
| `documents[].scanStatus` | `Pending`, `Scanning`, `Clean`, `Infected`, `Rejected` |
| `operations.type` | `IdentityOcr`, `FaceMatch` |
| `operations.state` | `Queued`, `Processing`, `Succeeded`, `Failed`, `Superseded` |
| `latestSubmission.status` | như `status` hồ sơ |
| `latestSubmission.decision` | `Pending`, `Approved`, `Rejected`, `ChangesRequested` |
| `personal.gender` | `Female`, `Male`, `Other`, `PreferNotToSay` |

## Luồng tổng thể

```text
GET /                          ──► 200 hồ sơ (version, status, missingFields, requirements)
PATCH /                        ──► 200 version mới          [If-Match]
POST /consents                 ──► 204 (IdentityProcessing)
POST /documents                ──► 201 documentId (CCCD trước/sau, selfie, giấy tờ)
POST /identity/ocr             ──► 202 operationId           [Idempotency-Key, If-Match]
GET  /operations/{id}          ──► poll tới Succeeded
POST /identity/confirm         ──► 200 (xác nhận dữ liệu OCR)
POST /identity/face-match      ──► 202 operationId           [Idempotency-Key]
GET  /operations/{id}          ──► poll tới Succeeded
POST /consents                 ──► 204 (FinalSubmission, đúng contentVersion)
POST /submit                   ──► 202 submissionId         [If-Match, finalConsentVersion]
```

## Lấy hồ sơ onboarding

`GET /` → `200 OK`

Trả về toàn bộ hồ sơ nháp kèm trạng thái, danh sách trường còn thiếu (`missingFields`) và `requirements` theo policy. Đây là nguồn sự thật để FE dựng wizard và điều hướng bước.

Response `200`:

```json
{
  "applicationId": "00000000-0000-0000-0000-000000000000",
  "version": 3,
  "status": "Draft",
  "currentStep": "Identity",
  "personal": {
    "legalFullName": "Nguyễn Thu Hà",
    "dateOfBirth": "1995-04-12",
    "gender": "Female",
    "permanentAddress": "12 Lê Lợi, Quận 1, TP.HCM",
    "currentAddress": "45 Nguyễn Huệ, Quận 1, TP.HCM",
    "experienceYears": 4
  },
  "identity": {
    "attemptId": "00000000-0000-0000-0000-000000000000",
    "frontDocumentId": "00000000-0000-0000-0000-000000000000",
    "backDocumentId": "00000000-0000-0000-0000-000000000000",
    "selfieDocumentId": null,
    "ocrState": "Extracted",
    "faceState": "NotStarted",
    "livenessState": "NotPerformed",
    "documentNumberMasked": "********1234",
    "fullName": "NGUYỄN THU HÀ",
    "dateOfBirth": "1995-04-12",
    "gender": "Female",
    "permanentAddress": "12 Lê Lợi, Quận 1, TP.HCM",
    "issuedOn": "2021-06-01",
    "issuedPlace": "Cục Cảnh sát QLHC về TTXH",
    "expiresOn": "2031-06-01",
    "faceScore": null,
    "faceThreshold": null,
    "resultCode": null,
    "confirmedAtUtc": null,
    "confirmationRequired": true
  },
  "documents": [
    {
      "documentId": "00000000-0000-0000-0000-000000000000",
      "type": "CitizenIdFront",
      "revision": 1,
      "status": "Pending",
      "scanStatus": "Clean",
      "originalFileName": "cccd-truoc.jpg",
      "contentType": "image/jpeg",
      "byteSize": 184320,
      "uploadedAtUtc": "2026-09-29T10:06:00+00:00",
      "isCurrent": true
    }
  ],
  "skillsAndBank": {
    "serviceCategoryIds": ["00000000-0000-0000-0000-000000000000"],
    "serviceAreaIds": ["00000000-0000-0000-0000-000000000000"],
    "bank": {
      "bankCode": "VCB",
      "accountNumberMasked": "****1234",
      "accountHolderName": "NGUYỄN THU HÀ",
      "accountHolderNameOverridden": false,
      "isVerified": false
    }
  },
  "missingFields": ["identityConfirmation", "faceMatch", "healthCertificate"],
  "requirements": {
    "policyVersion": "draft-v1",
    "requireHealthCertificate": true,
    "requireCriminalRecord": true,
    "allowManualReview": true,
    "maxFaceAttemptsPerDay": 5,
    "livenessMode": "NotImplemented"
  },
  "latestSubmission": null,
  "lastDecisionReason": null
}
```

- `identity.confirmationRequired = true` khi OCR đã xong (`Extracted`/`NeedsReview`) nhưng chưa xác nhận (`confirmedAtUtc = null`).
- `missingFields` dùng đúng tên field để FE chỉ ra chỗ cần bổ sung:
  `legalFullName`, `dateOfBirth`, `gender`, `permanentAddress`, `currentAddress`, `serviceCategoryIds`, `serviceAreaIds`, `bankCode`, `bankAccountNumber`, `citizenIdFront`, `citizenIdBack`, `identityOcr`, `identityConfirmation`, `faceMatch`, `healthCertificate`, `criminalRecord`.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Tài khoản chưa có hồ sơ freelancer |

## Cập nhật hồ sơ (nháp)

`PATCH /` → `200 OK`

Cập nhật **một phần**: chỉ gửi các field cần thay đổi. Trả về hồ sơ đầy đủ với `version` đã tăng 1.

Request (chỉ gửi field cần đổi):

```json
{
  "legalFullName": "Nguyễn Thu Hà",
  "dateOfBirth": "1995-04-12",
  "gender": "Female",
  "permanentAddress": "12 Lê Lợi, Quận 1, TP.HCM",
  "currentAddress": "45 Nguyễn Huệ, Quận 1, TP.HCM",
  "experienceYears": 4,
  "serviceCategoryIds": ["00000000-0000-0000-0000-000000000000"],
  "serviceAreaIds": ["00000000-0000-0000-0000-000000000000"],
  "currentStep": "SkillsAndBank",
  "bankCode": "VCB",
  "bankAccountNumber": "0011002233445",
  "bankAccountHolderName": "NGUYỄN THU HÀ",
  "bankAccountHolderNameOverrideReason": null
}
```

Ràng buộc:

| Trường | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `legalFullName` | Không | 2–160 ký tự (trim) |
| `dateOfBirth` | Không | `YYYY-MM-DD`, **không** ở tương lai |
| `gender` | Không | `Female`, `Male`, `Other`, `PreferNotToSay` |
| `permanentAddress` | Không | tối đa 400 ký tự |
| `currentAddress` | Không | tối đa 400 ký tự |
| `experienceYears` | Không | số nguyên 0–80 |
| `serviceCategoryIds` | Không | phải có ≥ 1 id; tất cả id phải tồn tại và đang hoạt động |
| `serviceAreaIds` | Không | phải có ≥ 1 id; tất cả id phải tồn tại và đang hoạt động |
| `currentStep` | Không | `Personal`, `Identity`, `SupportingDocuments`, `SkillsAndBank` |
| `bankCode` | Không | tối đa 40 ký tự |
| `bankAccountNumber` | Không | 6–64 ký tự, **chỉ chữ số** (giữ số 0 đầu) |
| `bankAccountHolderName` | Không | tối đa 160 ký tự |
| `bankAccountHolderNameOverrideReason` | Có điều kiện | tối đa 400 ký tự; bắt buộc khi tên chủ tài khoản khác danh tính đã xác nhận (so khớp không phân biệt hoa/thường) |

Hành vi:

- Nếu chưa nhập `bankAccountHolderName` mà đã xác nhận danh tính, server tự điền tên đã xác nhận.
- `accountNumberMasked` chỉ trả `****<4 số cuối>`; số đầy đủ lưu mã hóa và không bao giờ trả về.
- `currentStep` là gợi ý lưu vị trí wizard; có thể cập nhật ở bất kỳ lần PATCH nào.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Sai định dạng/giới hạn; ngày sinh tương lai; số tài khoản sai; danh mục không hợp lệ; thiếu lý do override |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `STALE_APPLICATION_VERSION` | `If-Match` không khớp version hiện tại |
| 503 | `KYC_SECURITY_NOT_CONFIGURED` | Chưa cấu hình key mã hóa (khi lưu số tài khoản) |

## Ghi nhận đồng thuận KYC

`POST /consents` → `204 No Content`

```json
{
  "consentType": "IdentityProcessing",
  "contentVersion": "identity-v1",
  "accepted": true
}
```

Ràng buộc:

| Trường | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `consentType` | Có | `IdentityProcessing` hoặc `FinalSubmission` |
| `contentVersion` | Có | 1–60 ký tự |
| `accepted` | Không | mặc định `true`; `false` sẽ **rút lại** đồng thuận đang hiệu lực cùng loại |

Hành vi:

- Thời điểm chấp nhận và IP được ghi ở server; client không gửi timestamp.
- `IdentityProcessing` là **điều kiện bắt buộc** trước khi gọi `/identity/ocr` và `/identity/face-match`.
- `FinalSubmission` với đúng `contentVersion` là điều kiện bắt buộc khi `/submit`.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu `consentType`/`contentVersion` |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |

## Tải lên tài liệu xác minh

`POST /documents` → `201 Created`

`Content-Type: multipart/form-data`. Tối đa **20 MB/request**; vượt giới hạn này trả `413` trước khi vào service.

Các part:

| Part | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `file` | Có | tệp nhị phân (IFormFile) |
| `type` | Có | `CitizenIdFront`, `CitizenIdBack`, `FaceVerification`, `HealthCertificate`, `CriminalRecord`, `Other` |
| `issuer` | Không | tối đa 200 ký tự |
| `issuedOn` | Không | `YYYY-MM-DD` |
| `expiresOn` | Không | `YYYY-MM-DD` |

Quy tắc tệp theo loại:

| Nhóm loại | Định dạng cho phép | Dung lượng tối đa |
| --- | --- | --- |
| `CitizenIdFront`, `CitizenIdBack` (ảnh CCCD) | JPEG, PNG | 5 MB |
| `FaceVerification` (selfie) | JPEG, PNG | 5 MB |
| `HealthCertificate`, `CriminalRecord`, `Other` | JPEG, PNG, **PDF** | 15 MB |

- Server kiểm tra **magic byte** thật của tệp; `Content-Type` gửi lên (nếu có) phải khớp nội dung, lệch → `415 UNSUPPORTED_FILE_TYPE`.
- Upload tài liệu CCCD mới (mặt trước/sau) sẽ **hủy hiệu lực** phiên OCR/face match cũ. Upload selfie mới hủy kết quả face match cũ. FE phải yêu cầu làm lại phần phụ thuộc.
- Mỗi lần upload cùng `type` tạo **revision** mới, revision cũ bị đánh `superseded` (`isCurrent: false`).

Response `201`:

```json
{
  "documentId": "00000000-0000-0000-0000-000000000000",
  "type": "CitizenIdFront",
  "revision": 1,
  "status": "Pending",
  "scanStatus": "Clean",
  "originalFileName": "cccd-truoc.jpg",
  "contentType": "image/jpeg",
  "byteSize": 184320,
  "uploadedAtUtc": "2026-09-29T10:06:00+00:00",
  "isCurrent": true
}
```

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu `file` hoặc `type`; tệp rỗng |
| 400 | `DOCUMENT_SIDE_INVALID` | Loại tài liệu không hỗ trợ trong luồng đăng ký |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 413 | `FILE_TOO_LARGE` | Vượt giới hạn dung lượng của loại tài liệu |
| 415 | `UNSUPPORTED_FILE_TYPE` | Sai định dạng hoặc MIME không khớp nội dung |

## Tải nội dung tài liệu

`GET /documents/{id}/content` → `200 OK`

Stream nội dung tệp (kiểm tra owner). Trả về `404` (không kèm ProblemDetails) nếu tài liệu không tồn tại hoặc không thuộc hồ sơ của người gọi.

## Xóa tài liệu

`DELETE /documents/{id}` → `204 No Content`

Đánh dấu tài liệu là `superseded` (không xóa cứng) và hủy kết quả phụ thuộc (OCR/face match). Gửi `If-Match` với version hiện tại.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `DOCUMENT_NOT_FOUND` | Không tìm thấy tài liệu |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `STALE_APPLICATION_VERSION` | `If-Match` không khớp version hiện tại |

## Bắt đầu OCR CCCD

`POST /identity/ocr` → `202 Accepted`

Headers bắt buộc: `Idempotency-Key` (và nên gửi `If-Match`).

```json
{
  "frontDocumentId": "00000000-0000-0000-0000-000000000000",
  "backDocumentId": "00000000-0000-0000-0000-000000000000"
}
```

- `frontDocumentId` phải là tài liệu `CitizenIdFront` **hiện hành**; `backDocumentId` phải là `CitizenIdBack` hiện hành, cùng hồ sơ của người gọi.
- Phải có đồng thuận `IdentityProcessing` đang hiệu lực.
- Tạo phiên OCR mới (phiên cũ bị supersede) và xếp job xử lý nền; FE polling qua `/operations/{id}`.

Response `202`:

```json
{
  "operationId": "00000000-0000-0000-0000-000000000000",
  "type": "IdentityOcr",
  "state": "Queued",
  "errorCode": null,
  "nextPollAfterSeconds": 2,
  "createdAtUtc": "2026-09-29T10:07:00+00:00",
  "completedAtUtc": null
}
```

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu `Idempotency-Key` hoặc thiếu document id |
| 400 | `DOCUMENT_SIDE_INVALID` | Ảnh không đúng mặt/hết hiệu lực |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `STALE_APPLICATION_VERSION` | `If-Match` không khớp version hiện tại |
| 409 | `CONSENT_REQUIRED` | Thiếu đồng thuận `IdentityProcessing` |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Key đã dùng với body khác |
| 429 | — | Vượt rate limit `kyc-operation` (kèm `Retry-After`) |

## Xác nhận thông tin OCR

`POST /identity/confirm` → `200 OK`

```json
{
  "attemptId": "00000000-0000-0000-0000-000000000000",
  "corrections": { "fullName": "Nguyễn Thu Hà" },
  "correctionReason": "Ảnh CCCD mờ, tên bị đọc sai dấu."
}
```

Ràng buộc:

| Trường | Bắt buộc | Ràng buộc |
| --- | --- | --- |
| `attemptId` | Có | id phiên OCR hiện hành (lấy từ `identity.attemptId`) |
| `corrections` | Không | map field → giá trị; hỗ trợ `documentNumber`, `fullName` |
| `correctionReason` | Có điều kiện | tối đa 500 ký tự; **bắt buộc** khi có `corrections` |

Hành vi:

- Chỉ xác nhận được khi `ocrState` ∈ `Extracted`, `NeedsReview`.
- Xác nhận đặt `confirmedAtUtc`; từ đó `missingFields` bỏ `identityConfirmation`.
- Đính chính **không** ghi đè raw OCR; lưu riêng trong `corrections`.
- Trả về hồ sơ đầy đủ (như `GET /`).

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Có `corrections` nhưng thiếu `correctionReason` |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `ATTEMPT_NOT_FOUND` | Không tìm thấy phiên OCR hiện hành |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `OCR_INCOMPLETE` | OCR chưa sẵn sàng để xác nhận |

## Bắt đầu so khớp khuôn mặt

`POST /identity/face-match` → `202 Accepted`

Headers bắt buộc: `Idempotency-Key`.

```json
{
  "selfieDocumentId": "00000000-0000-0000-0000-000000000000",
  "attemptId": "00000000-0000-0000-0000-000000000000"
}
```

- `selfieDocumentId` phải là tài liệu `FaceVerification` hiện hành; `attemptId` phải có OCR `Extracted`/`NeedsReview`.
- Phải có đồng thuận `IdentityProcessing` đang hiệu lực.
- Backend tự chọn ảnh CCCD tương ứng để đối chiếu; FE **không** gửi điểm/threshold.
- Kết quả nằm ở `identity.faceState`/`faceScore`/`faceThreshold` sau khi operation `Succeeded` (`Matched`, `NotMatched`, `NeedsReview`, `TechnicalError`).

Response `202`: giống `/identity/ocr` với `"type": "FaceMatch"`.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu `Idempotency-Key` |
| 400 | `DOCUMENT_SIDE_INVALID` | Selfie không đúng loại/hết hiệu lực |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `ATTEMPT_NOT_FOUND` | Không tìm thấy phiên OCR hiện hành |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `CONSENT_REQUIRED` | Thiếu đồng thuận `IdentityProcessing` |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Key đã dùng với body khác |
| 409 | `OCR_INCOMPLETE` | Phải đọc CCCD trước khi so khớp |
| 429 | — | Vượt rate limit `kyc-operation` (kèm `Retry-After`) |

## Lấy trạng thái thao tác KYC

`GET /operations/{id}` → `200 OK`

Dùng để polling OCR/face match.

```json
{
  "operationId": "00000000-0000-0000-0000-000000000000",
  "type": "FaceMatch",
  "state": "Succeeded",
  "errorCode": null,
  "nextPollAfterSeconds": 0,
  "createdAtUtc": "2026-09-29T10:08:00+00:00",
  "completedAtUtc": "2026-09-29T10:08:07+00:00"
}
```

- `nextPollAfterSeconds`: `2` khi đang `Queued`/`Processing`, `0` khi đã kết thúc. FE polling theo giá trị này.
- Khi `state = Failed`, `errorCode` mang mã an toàn (ví dụ `KYC_PROVIDER_UNAVAILABLE`, `NO_FACE_DETECTED`, `MULTIPLE_FACES_DETECTED`, `FACE_NOT_MATCHED`, `ID_EXPIRED`). **Không** hiển thị `Failed`/`TechnicalError` thành "CCCD không hợp lệ".
- Trả `404` (không kèm ProblemDetails) nếu operation không tồn tại hoặc không thuộc người gọi.

## Gửi hồ sơ để thẩm định

`POST /submit` → `202 Accepted`

Headers: `If-Match` (khuyến nghị).

```json
{ "finalConsentVersion": "final-v1" }
```

Điều kiện server kiểm tra:

- Hồ sơ đang `Draft`/`ChangesRequested` và `If-Match` khớp version.
- Không còn `missingFields` (nếu còn → `422 APPLICATION_INCOMPLETE`, `detail` liệt kê các field thiếu).
- Có đồng thuận `IdentityProcessing` đang hiệu lực.
- Có đồng thuận `FinalSubmission` với `contentVersion` **khớp chính xác** `finalConsentVersion`.
- Fingerprint CCCD không trùng hồ sơ khác đang reserve → nếu trùng `409 IDENTITY_CONFLICT` (dùng thông báo chung, chuyển hỗ trợ).

Hành vi:

- Đóng băng snapshot, tăng `submissionVersion`, tạo `ProviderApprovalReview(Pending)`, chuyển `Provider.ApprovalStatus = Submitted` trong một transaction.
- **Idempotent**: gửi lặp khi hồ sơ đã `Submitted` trả lại submission hiện có, không tạo review thứ hai.
- `Status` hồ sơ chuyển `Submitted`; `currentStep` = `SkillsAndBank`. Không tự bật nhận booking.

Response `202`:

```json
{
  "submissionId": "00000000-0000-0000-0000-000000000000",
  "version": 1,
  "status": "Submitted",
  "decision": "Pending",
  "submittedAtUtc": "2026-09-29T10:12:00+00:00",
  "decidedAtUtc": null,
  "decisionReason": null,
  "applicationCode": "FR-20260929-1A2B3C4D"
}
```

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu `finalConsentVersion` |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Hồ sơ đã gửi/chờ duyệt |
| 409 | `STALE_APPLICATION_VERSION` | `If-Match` không khớp version hiện tại |
| 409 | `CONSENT_REQUIRED` | Thiếu `IdentityProcessing` hoặc `FinalSubmission` (sai `contentVersion`) |
| 409 | `IDENTITY_CONFLICT` | CCCD đã dùng cho hồ sơ khác |
| 422 | `APPLICATION_INCOMPLETE` | Còn trường/tài liệu thiếu; `detail` liệt kê |

## Mở lại hồ sơ

`POST /reopen` → `200 OK`

```json
{ "reason": "Bổ sung lại ảnh CCCD mặt sau bị lóa." }
```

- Chỉ áp dụng khi `status` ∈ `Rejected`, `ChangesRequested`.
- Chuyển hồ sơ về `Draft`, tăng `version`, giải phóng identity claim đang reserve. Trả về hồ sơ đầy đủ (như `GET /`), FE tiếp tục sửa và gửi lại.
- `reason` không bắt buộc, tối đa 500 ký tự.

Lỗi:

| Status | `code` | Khi nào |
| --- | --- | --- |
| 401 | `UNAUTHENTICATED` | Thiếu thông tin xác thực |
| 404 | `APPLICATION_NOT_FOUND` | Chưa có hồ sơ |
| 409 | `APPLICATION_LOCKED` | Trạng thái không phải `Rejected`/`ChangesRequested` |

## Gợi ý cho frontend

- Nạp `GET /` ngay sau đăng nhập để dựng wizard: dùng `status` + `currentStep` để điều hướng, `missingFields` để đánh dấu và chặn nút gửi, `requirements` để quyết định giấy tờ bắt buộc (không hard-code).
- Bắt buộc gọi `POST /consents` (`IdentityProcessing`) **trước** khi cho phép upload/OCR/face-match, và `FinalSubmission` (đúng `contentVersion`) trước khi submit.
- **Đồng bộ version**: đọc `version` từ response mới nhất, gửi kèm `If-Match` ở `PATCH`/`DELETE`/`ocr`/`submit`; khi gặp `409 STALE_APPLICATION_VERSION` thì nạp lại `GET /` rồi thao tác lại.
- Sinh `Idempotency-Key` (UUID) mới cho mỗi lần gọi `/identity/ocr` và `/identity/face-match`; giữ nguyên key khi retry cùng request.
- Upload riêng từng loại: CCCD mặt trước (`CitizenIdFront`), mặt sau (`CitizenIdBack`), selfie (`FaceVerification`), giấy tờ bổ sung. Sau khi thay ảnh CCCD/selfie, phải làm lại OCR/face-match ở phần phụ thuộc.
- Polling `/operations/{id}` theo `nextPollAfterSeconds`; dừng khi `state` ∈ `Succeeded`, `Failed`, `Superseded`. Hiển thị trạng thái "Đang đọc CCCD"/"Đang đối chiếu" thay vì kết luận sớm.
- Chỉ bật nút "Gửi thẩm định" khi `missingFields` rỗng; xử lý `422 APPLICATION_INCOMPLETE` bằng cách map `detail`/`missingFields` về đúng bước.
- `AccountStatus` chưa xác thực tách khỏi provider chưa duyệt: submit **không** bật nhận booking. Sau khi duyệt mới chuyển sang bước thiết lập lịch/giá.
- Không hiển thị điểm face match thô nếu không cần; `livenessState = NotPerformed` nghĩa là **chưa** kiểm tra người thật — không ghi "đã xác thực sinh trắc".
- Token sống 15 phút; gặp `401` thì làm luồng refresh/đăng nhập lại rồi tải lại nháp từ server.
