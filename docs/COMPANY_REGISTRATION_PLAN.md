# Plan triển khai: luồng đăng ký tài khoản cho công ty đối tác (Company)

Tài liệu này là bản giao việc cho một AI coding agent. Bạn làm việc trong repo `backend` (ASP.NET Core 8, EF Core 8, PostgreSQL 16). Làm **tuần tự từng phase**, mỗi phase build xanh + test xanh rồi mới sang phase sau. Không tự mở rộng phạm vi ngoài mục 2.

---

## 1. Bối cảnh

ABSTRICT có hai loại đối tác: freelancer và công ty giúp việc. Nghiệp vụ gốc ở `PROJECT_IDEA.md` (mục 1, 2.1) và màn hình "C1 - Định danh Doanh nghiệp" trong `docs/DATABASE_ENTITY_MAPPING.md`. Đọc hai file này trước.

Hiện trạng code:

- **Luồng freelancer đã chạy đủ và là khuôn mẫu để làm theo:** đăng ký bằng số điện thoại + OTP (`POST /auth/register-freelancer`), đăng nhập, khai hồ sơ KYC, tải giấy tờ, gửi duyệt, admin duyệt. Xem `docs/FREELANCER_KYC_API.md`, `FreelancerKycService`, `AdminKycService`, `FreelancerKycController`, `AdminKycController`.
- **Đã có entity + bảng cho công ty nhưng chưa có dòng code nghiệp vụ nào:** `CompanyProfile`, `CompanyRepresentative`, `CompanyWorker`, cùng các bảng dùng chung `Provider`, `VerificationDocument`, `ProviderApprovalReview`, `ProviderServiceArea`, `AuditLog`. Xem `src/Abstrict.Api/Models/Entities/` và phần `ConfigureProviders` trong `Data/Configurations/AbstrictModelConfiguration.cs`.
- **Tài khoản `Company` đang bị chặn đăng nhập:** `CustomerLoginService.CanSignIn` loại `UserRole.Company`.
- **Đăng ký dùng chung:** `CustomerRegistrationService.RegisterAsync(request, ct, role)` nhận `role`, tạo `User` + `CustomerProfile` (giữ họ tên người đăng ký) và OTP với `OtpPurpose.CustomerRegistration` cho mọi vai trò. Xác thực/gửi lại OTP dùng chung `/auth/verify-phone-otp`, `/auth/resend-phone-otp`.
- Đăng nhập trả `freelancerOnboarding` cho freelancer và đặt cookie refresh; xem mục "Đăng nhập và phiên" trong `docs/BOOKING_API.md`.

Mục tiêu: một công ty tự đăng ký tài khoản, đăng nhập, khai thông tin pháp nhân + người đại diện + khu vực nhận ca, tải giấy tờ, gửi duyệt; admin xem xét và duyệt / yêu cầu chỉnh sửa / từ chối.

## 2. Phạm vi

**Trong phạm vi**

1. Đăng ký tài khoản công ty bằng số điện thoại + OTP, và cho phép tài khoản công ty đăng nhập.
2. Trả tiến độ onboarding của công ty trong response đăng nhập/refresh.
3. Công ty khai và sửa: thông tin pháp nhân, người đại diện, khu vực nhận ca.
4. Tải, thay và xem lại giấy tờ (ĐKKD, CCCD người đại diện, bảo hiểm trách nhiệm).
5. Gửi duyệt.
6. Admin: hàng chờ, chi tiết, bắt đầu xem xét, ra quyết định, xem giấy tờ.

**Ngoài phạm vi (không làm, không tạo stub)**

- Quản lý người làm của công ty (`CompanyWorker`, `CompanyWorkerAssignment`).
- Bảng giá/dịch vụ của công ty (`ProviderService`), tài khoản ngân hàng, ví ký quỹ.
- Hiển thị công ty trong danh bạ công khai và đặt lịch với công ty. `BookingService` vẫn trả `PROVIDER_NOT_BOOKABLE` cho công ty.
- Gói tăng hiển thị, bài test năng lực (`ProviderAssessment`).
- Đăng nhập nhiều người dùng cho một công ty: một tài khoản = một công ty.

## 3. Quy ước bắt buộc của repo

Làm giống code hiện có, không áp kiến trúc mới.

| Chủ đề | Quy ước | File mẫu |
| --- | --- | --- |
| Tầng | Controller → Service (interface + implementation). Service dùng thẳng `AppDbContext`. Không tạo Repository, MediatR, AutoMapper, FluentValidation. | `FreelancerKycService.cs` |
| Controller | `[ApiController]`, route `ApiRoutes.Version1 + "/..."`, primary constructor, `sealed`, kế thừa `KycControllerBase` (đã có `CurrentUserId`, `Run<T>`, `ToProblem`, `ImageFile`). | `FreelancerKycController.cs` |
| Lỗi nghiệp vụ | Ném `KycFlowException(statusCode, code, message)`; `code` là `UPPER_SNAKE_CASE`, `message` tiếng Việt. | `CitizenIdProtector.cs` |
| DTO | Request: `sealed class` + `init` + DataAnnotations ở `DTOs/Requests`. Response: `sealed record` ở `DTOs/Responses`. Không trả entity. | `KycRequests.cs`, `KycResponses.cs` |
| Thời gian | `TimeProvider` được inject; lưu và trả UTC. | `FreelancerKycService.cs` |
| Dữ liệu nhạy cảm | Số CCCD mã hóa bằng `CitizenIdProtector.Protect(...).Protected`; không log, không đưa vào `AuditLog`. Response cho công ty chỉ có 4 số cuối. | `FreelancerKycService.SaveProfileAsync` |
| File | Lưu qua `IFileStorage`, định dạng xác định bằng magic bytes, không tin `Content-Type`/đuôi file. Chỉ phát qua endpoint có phân quyền, `Cache-Control: private, no-store`. | `FreelancerKycService.UploadDocumentAsync`, `KycImage.cs` |
| Phân quyền | `[Authorize(Roles = nameof(UserRole.Company))]` / `Admin`. Service luôn kiểm tra quyền sở hữu qua `CompanyProfile.OwnerUserId`. | `FreelancerKycController.cs` |
| Rate limit | Endpoint ghi dùng policy `kyc-write` có sẵn; đăng ký dùng `auth-register`. | `Program.cs` |
| Audit | Ghi `AuditLog` cho gửi duyệt và mọi thao tác của admin, theo cách đặt tên `company.submitted`, `company.viewed`… | `AdminKycService.NewAudit` |
| DI, Swagger | `AddScoped<IX, X>()` trong `Program.cs`; XML doc tiếng Việt, `[ProducesResponseType]`. | `Program.cs`, `CatalogController.cs` |

**Không sửa hành vi luồng freelancer.** Không refactor `FreelancerKycService`/`AdminKycService` để dùng chung; viết service riêng cho công ty và chấp nhận trùng lặp có kiểm soát. Được phép thêm thành viên mới vào các class dùng chung (ví dụ thêm định dạng PDF) miễn là không đổi kết quả của code đang gọi.

**Migration.** Luồng này dự kiến **không cần migration**: mọi bảng và cột đã có. Sau khi xong, `dotnet ef migrations has-pending-model-changes --project src/Abstrict.Api` phải báo không có thay đổi. Nếu thấy cần đổi schema, dừng lại và hỏi. Không sửa migration cũ. Working tree đang có thay đổi chưa commit; không revert chúng.

## 4. Mô hình nghiệp vụ

### 4.1 Trạng thái

Dùng `Provider.ApprovalStatus`, giống freelancer:

```text
(chưa có hồ sơ) ──lưu thông tin pháp nhân──▶ Draft ──gửi duyệt──▶ Submitted ──admin mở──▶ UnderReview
                                              ▲                                              │
                                              └────── ChangesRequested ◀─────────────────────┤
                                                                          Approved ◀─────────┤
                                                                          Rejected ◀─────────┘
```

- Chỉ sửa được khi `Draft` hoặc `Rejected`; trạng thái khác trả 409 `COMPANY_NOT_EDITABLE`.
- `ChangesRequested` đưa hồ sơ về `Draft` và đánh dấu các giấy tờ bị nêu tên là `Rejected`.
- Duyệt (`Approved`) **không** tự bật `Provider.IsAcceptingBookings`; để `false` vì đặt lịch với công ty chưa có.

### 4.2 Các bước onboarding

`currentStep` được tính ở server, theo thứ tự ưu tiên:

| `currentStep` | Điều kiện |
| --- | --- |
| `Completed` | `Approved`. |
| `Suspended` | `Suspended`. |
| `AwaitingReview` | `Submitted` hoặc `UnderReview`. |
| `Company` | Chưa có `CompanyProfile`. |
| `Representative` | Chưa có `CompanyRepresentative`. |
| `ServiceAreas` | Chưa có `ProviderServiceArea` nào. |
| `Documents` | Thiếu giấy tờ bắt buộc, hoặc còn giấy tờ `Rejected`. |
| `Submit` | Đủ hết, chưa gửi. |

Đặt hàm tính bước trong class tĩnh `CompanyOnboardingPolicy` để unit test không cần DB.

### 4.3 Quy tắc dữ liệu

**Thông tin pháp nhân** (`CompanyProfile` + `Provider`):

| Trường | Quy tắc |
| --- | --- |
| `legalName` | 2–200 ký tự, gộp khoảng trắng thừa. |
| `displayName` | 2–120 ký tự; lưu vào `Provider.DisplayName`. |
| `taxCode` | Mã số thuế: đúng 10 chữ số, hoặc 10 chữ số + `-` + 3 chữ số (chi nhánh). Chuẩn hóa bỏ khoảng trắng. Trùng với công ty khác → 409 `TAX_CODE_ALREADY_USED` (kiểm tra bằng query và bắt cả lỗi unique index). |
| `hotline` | Chuẩn hóa bằng `VietnamesePhoneNumber.Normalize`; không hợp lệ → 400 `INVALID_HOTLINE`. Cho phép thêm đầu số `1800`/`1900` (8–10 chữ số). |
| `headquartersAddress` | 5–300 ký tự. |
| `workforceSize` | Enum `CompanyWorkforceSize`: `UnderTen`, `TenToThirty`, `OverThirty`. |
| `liabilityCoverageVnd` | Tùy chọn, `long` ≥ 0. |

Lần lưu đầu tiên tạo `Provider { Type = Company, ApprovalStatus = Draft, IsAcceptingBookings = false }` và `CompanyProfile { OwnerUserId }` trong cùng một `SaveChanges`.

**Người đại diện** (`CompanyRepresentative`, cần có `CompanyProfile` trước → nếu chưa có trả 409 `COMPANY_PROFILE_REQUIRED`):

| Trường | Quy tắc |
| --- | --- |
| `fullName` | 2–160 ký tự. |
| `jobTitle` | 2–120 ký tự. |
| `workEmail` | Email hợp lệ, ≤ 254 ký tự, lưu chữ thường. |
| `phoneNumber` | `VietnamesePhoneNumber.Normalize`; sai → 400 `INVALID_PHONE_NUMBER`. |
| `identityNumber` | CCCD đúng 12 chữ số. Lưu `IdentityNumberEncrypted = protector.Protect(x).Protected`. Không chặn trùng giữa các công ty. |

Response chỉ trả `identityNumberLast4`, lấy bằng cách `Unprotect` rồi cắt 4 số cuối trong service; không bao giờ trả số đầy đủ cho công ty.

**Khu vực nhận ca:** danh sách `serviceAreaIds`, 1–20 phần tử, không trùng, mỗi id phải là `ServiceArea` đang `IsActive` (sai → 400 `INVALID_SERVICE_AREA`). Lưu theo kiểu thay toàn bộ tập `ProviderServiceArea` của provider.

**Giấy tờ** (`VerificationDocument`, cần có `CompanyProfile` trước):

| `type` | Bắt buộc | Định dạng |
| --- | --- | --- |
| `BusinessRegistration` | Có | JPG, PNG hoặc PDF |
| `CitizenIdFront` | Có (của người đại diện) | JPG hoặc PNG |
| `CitizenIdBack` | Có (của người đại diện) | JPG hoặc PNG |
| `LiabilityInsurance` | Chỉ khi `liabilityCoverageVnd > 0` | JPG, PNG hoặc PDF |

- Tối đa 10 MB/file (`FILE_TOO_LARGE` 413), file rỗng `EMPTY_FILE` 400, sai định dạng `UNSUPPORTED_DOCUMENT` 415, loại không thuộc bảng trên `INVALID_DOCUMENT_TYPE` 400.
- Nhận diện PDF bằng magic bytes `%PDF-` (`0x25 0x50 0x44 0x46 0x2D`). Tạo helper `CompanyDocument` (định dạng cho phép theo từng loại, `Detect`, `ContentTypeFromKey` có nhánh `.pdf` → `application/pdf`); không sửa `KycImage`.
- Lưu ở thư mục `company/{providerId:N}`. Tải lại cùng loại thì thay bản cũ và xóa file cũ, theo đúng trình tự an toàn của `FreelancerKycService.UploadDocumentAsync` (lưu file mới → ghi DB → xóa file cũ; DB lỗi thì xóa file mới).
- Khi trả PDF: `Content-Disposition: inline`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`.

**Gửi duyệt:** yêu cầu `currentStep = Submit` theo 4.2; thiếu gì trả 409 với mã tương ứng (`COMPANY_PROFILE_REQUIRED`, `COMPANY_REPRESENTATIVE_REQUIRED`, `COMPANY_SERVICE_AREAS_REQUIRED`, `COMPANY_DOCUMENTS_MISSING`, `COMPANY_DOCUMENT_REJECTED`). Thành công: `ApprovalStatus = Submitted`, thêm `ProviderApprovalReview { Decision = Pending, SubmittedAtUtc }`, ghi audit `company.submitted`.

## 5. Hợp đồng API

Tất cả dưới `/api/v1`. Lỗi là `ProblemDetails` với `code`.

### 5.1 Tài khoản

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/auth/register-company` | public | Cùng body và response với `/auth/register`; tạo tài khoản `Company`. `fullName` là họ tên người quản lý tài khoản. Rate limit `auth-register`. |
| POST | `/auth/verify-phone-otp`, `/auth/resend-phone-otp` | public | Dùng lại nguyên trạng. |
| POST | `/auth/login`, `/auth/refresh` | public / cookie | Cho phép `Company`. Response thêm `companyOnboarding`. |

Thay đổi ở `CustomerLoginService`: bỏ điều kiện loại `UserRole.Company` trong `CanSignIn`; thêm trường `CompanyOnboarding` (kiểu `CompanyOnboardingResponse?`) vào cuối `CustomerLoginResponse`, chỉ khác `null` khi `role = Company`:

```json
"companyOnboarding": { "providerId": null, "status": "NotStarted", "currentStep": "Company" }
```

`status` là `NotStarted` hoặc tên `ApprovalStatus`. Cập nhật doc comment của `CustomerLoginResponse` (danh sách `role` thêm `Company`).

### 5.2 Onboarding của công ty — role `Company`

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/company/onboarding` | Toàn bộ trạng thái hồ sơ. |
| PUT | `/company/onboarding/company` | Lưu thông tin pháp nhân. |
| PUT | `/company/onboarding/representative` | Lưu người đại diện. |
| PUT | `/company/onboarding/service-areas` | Body `{ "serviceAreaIds": ["…"] }`. |
| PUT | `/company/onboarding/documents/{type}` | `multipart/form-data`, trường `file`. |
| GET | `/company/onboarding/documents/{documentId}/content` | Nội dung giấy tờ của chính mình. |
| POST | `/company/onboarding/submit` | Gửi duyệt. |

Mọi endpoint (trừ tải nội dung file) trả `CompanyOnboardingResponse`:

```json
{
  "status": "Draft",
  "currentStep": "Documents",
  "canEdit": true,
  "providerId": "…",
  "company": {
    "legalName": "Công ty TNHH Sạch Nhà",
    "displayName": "Sạch Nhà",
    "taxCode": "0312345678",
    "hotline": "02838123456",
    "headquartersAddress": "12 Lê Lợi, Quận 1, Hồ Chí Minh",
    "workforceSize": "TenToThirty",
    "liabilityCoverageVnd": 500000000
  },
  "representative": {
    "fullName": "Trần Văn An",
    "jobTitle": "Giám đốc",
    "workEmail": "an@sachnha.vn",
    "phoneNumber": "0901234567",
    "identityNumberLast4": "1234"
  },
  "serviceAreas": [{ "id": "…", "city": "Hồ Chí Minh", "district": "Quận 1", "wardOrComplex": null }],
  "requiredDocumentTypes": ["BusinessRegistration", "CitizenIdFront", "CitizenIdBack", "LiabilityInsurance"],
  "documents": [{ "id": "…", "type": "BusinessRegistration", "status": "Pending", "originalFileName": "dkkd.pdf", "contentType": "application/pdf", "uploadedAtUtc": "…", "rejectionReason": null }],
  "feedback": { "decision": "ChangesRequested", "reason": "…", "decidedAtUtc": "…" },
  "submittedAtUtc": null
}
```

Khi chưa có hồ sơ: `status = "NotStarted"`, `currentStep = "Company"`, `canEdit = true`, các object là `null`, các mảng rỗng (`requiredDocumentTypes` vẫn trả 3 loại bắt buộc mặc định). Dùng lại `KycFeedbackResponse` cho `feedback`.

### 5.3 Admin — role `Admin`

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/admin/companies?status=&page=&pageSize=` | Bỏ trống `status` = hàng chờ (`Submitted` + `UnderReview`), cũ nhất trước. Trả `PagedResponse` gồm `providerId`, `legalName`, `taxCode`, `ownerPhoneNumber`, `status`, `submittedAtUtc`. |
| GET | `/admin/companies/{providerId}` | Chi tiết đầy đủ, **kèm số CCCD người đại diện đã giải mã**. Ghi audit `company.viewed`. |
| POST | `/admin/companies/{providerId}/start-review` | `Submitted` → `UnderReview`; ghi `ReviewStartedAtUtc`, `AssignedAdminUserId`. |
| POST | `/admin/companies/{providerId}/decision` | Body là `KycDecisionRequest` có sẵn (`decision`, `reason`, `rejectedDocumentTypes`). |
| GET | `/admin/companies/{providerId}/documents/{documentId}/content` | Nội dung giấy tờ. Ghi audit `company.document_viewed`. |

Quy tắc quyết định, làm đúng như `AdminKycService.DecideAsync` (đọc kỹ hàm này trước): chỉ nhận `Approved` / `Rejected` / `ChangesRequested`; lý do tối thiểu 10 ký tự trừ khi duyệt; chỉ quyết định được khi `Submitted` hoặc `UnderReview`; `Approved` đánh dấu mọi giấy tờ `Verified`; `ChangesRequested`/`Rejected` đánh dấu các loại trong `rejectedDocumentTypes` là `Rejected` kèm lý do; cập nhật `ProviderApprovalReview` đang mở; ghi audit `company.approved` / `company.rejected` / `company.changesrequested`. Endpoint admin của freelancer (`/admin/kyc/*`) phải tiếp tục **không** trả về công ty, và endpoint công ty không trả freelancer.

### 5.4 Mã lỗi mới

| HTTP | `code` |
| --- | --- |
| 400 | `INVALID_TAX_CODE`, `INVALID_HOTLINE`, `INVALID_PHONE_NUMBER`, `INVALID_SERVICE_AREA`, `INVALID_DOCUMENT_TYPE`, `EMPTY_FILE` |
| 404 | `COMPANY_NOT_FOUND` (admin), `DOCUMENT_NOT_FOUND` |
| 409 | `TAX_CODE_ALREADY_USED`, `COMPANY_NOT_EDITABLE`, `COMPANY_PROFILE_REQUIRED`, `COMPANY_REPRESENTATIVE_REQUIRED`, `COMPANY_SERVICE_AREAS_REQUIRED`, `COMPANY_DOCUMENTS_MISSING`, `COMPANY_DOCUMENT_REJECTED`, `COMPANY_REVIEW_INVALID_STATE` |
| 413 | `FILE_TOO_LARGE` |
| 415 | `UNSUPPORTED_DOCUMENT` |

## 6. Các phase

Sau mỗi phase: `dotnet build Abstrict.sln` và `dotnet test Abstrict.sln`.

### Phase 0 — Baseline

Đọc các file ở mục 1 và mục 3. Chạy build + test, ghi kết quả. Baseline đỏ thì dừng và báo lại.

### Phase 1 — Tài khoản

1. `POST /auth/register-company` trong `AuthController` (sao `RegisterFreelancer`, truyền `UserRole.Company`), có XML doc.
2. Cho `Company` đăng nhập; thêm `CompanyOnboardingResponse` + enum `CompanyOnboardingStep` vào `DTOs/Responses`, thêm trường `CompanyOnboarding` vào `CustomerLoginResponse`.
3. `CompanyOnboardingPolicy.ResolveStep(...)` (thuần, theo 4.2) và `CompanyTaxCode.Normalize(string) : string?` (thuần, theo 4.3).
4. Unit test `tests/Abstrict.Api.Tests/CompanyOnboardingTests.cs`: từng nhánh của `ResolveStep`; mã số thuế hợp lệ (`0312345678`, `0312345678-001`, có khoảng trắng) và không hợp lệ (9 số, có chữ, sai hậu tố).

### Phase 2 — Hồ sơ công ty

`ICompanyOnboardingService` + `CompanyOnboardingService` với `GetAsync`, `SaveCompanyAsync`, `SaveRepresentativeAsync`, `SaveServiceAreasAsync`; `CompanyOnboardingController`; DTO theo 5.2. Dùng `BuildOnboarding` chung cho service này và cho đăng nhập để hai nơi không lệch nhau.

### Phase 3 — Giấy tờ và gửi duyệt

Helper `CompanyDocument`; `UploadDocumentAsync`, `OpenDocumentAsync`, `SubmitAsync`; các action tương ứng. `[RequestSizeLimit]` = 10 MB + 256 KB. Unit test: `CompanyDocument.Detect` nhận JPG/PNG/PDF, từ chối GIF/văn bản; PDF bị từ chối cho `CitizenIdFront`.

### Phase 4 — Admin

`IAdminCompanyReviewService` + implementation, `AdminCompanyController`, DTO `AdminCompanyListItemResponse`, `AdminCompanyDetailResponse` theo 5.3.

### Phase 5 — Tài liệu và dữ liệu thử

1. `docs/COMPANY_ONBOARDING_API.md` theo văn phong `docs/FREELANCER_KYC_API.md` + ví dụ JSON và bảng mã lỗi.
2. Thêm request mẫu vào `src/Abstrict.Api/Abstrict.Api.http`.
3. Cập nhật mục "Đăng nhập và phiên" trong `docs/BOOKING_API.md`: `role` có thêm `Company`, thêm `companyOnboarding`, thêm `/auth/register-company` vào danh sách đường dẫn chính thức.
4. Cập nhật `Description` Swagger trong `Program.cs`.

## 7. Kiểm chứng cuối

Chạy `docker compose --env-file .env.docker up -d --build`. Môi trường Development trả `developmentOtpCode` nên đăng ký được không cần SMS. Admin thử: số `0900000001`, mật khẩu `Abstrict-dev-admin-2026`. Dùng `--data-binary @file` khi gửi JSON có tiếng Việt bằng curl trên Windows.

Phải chạy đúng bằng HTTP thật, ghi lại status code từng bước:

1. **Đường chính:** đăng ký công ty → xác thực OTP → đăng nhập (`role = Company`, `currentStep = Company`) → lưu pháp nhân → người đại diện → khu vực → tải 3 giấy tờ bắt buộc (ĐKKD là PDF) → `currentStep = Submit` → gửi duyệt → admin thấy trong hàng chờ → start-review → `Approved` → đăng nhập lại thấy `currentStep = Completed`.
2. **Yêu cầu chỉnh sửa:** admin `ChangesRequested` kèm `rejectedDocumentTypes = ["BusinessRegistration"]` → công ty thấy `status = Draft`, `currentStep = Documents`, có `feedback` → gửi duyệt ngay bị 409 `COMPANY_DOCUMENT_REJECTED` → tải lại → gửi được.
3. **Khóa sửa:** sau khi gửi duyệt, mọi `PUT` trả 409 `COMPANY_NOT_EDITABLE`.
4. **Mã số thuế trùng:** công ty thứ hai dùng lại MST → 409 `TAX_CODE_ALREADY_USED`.
5. **Giấy tờ:** file văn bản đổi đuôi `.pdf` → 415; PDF cho `CitizenIdFront` → 415; `liabilityCoverageVnd > 0` mà thiếu `LiabilityInsurance` → gửi duyệt 409 `COMPANY_DOCUMENTS_MISSING`.
6. **Phân quyền:** công ty A đọc giấy tờ của công ty B → 404; token `Customer`/`Freelancer` gọi `/company/onboarding` → 403; token `Company` gọi `/freelancer/kyc` và `/bookings` → 403; token không phải admin gọi `/admin/companies` → 403.
7. **Không lẫn loại đối tác:** `/admin/kyc` không có công ty, `/admin/companies` không có freelancer, `/freelancers` không có công ty.
8. **Bảo mật dữ liệu:** response của công ty không có số CCCD đầy đủ; cột `identity_number_encrypted` trong DB không chứa số CCCD thô; `audit_logs` không chứa số CCCD.
9. **Hồi quy:** đăng nhập khách (`0920000001`) và freelancer (`0910000001`) vẫn trả đúng `customerProfile` / `freelancerOnboarding`, `companyOnboarding = null`.

## 8. Định nghĩa hoàn thành

- `dotnet build` không warning mới; `dotnet test` xanh, có các unit test ở Phase 1 và 3.
- Không có migration mới; `has-pending-model-changes` báo không có thay đổi.
- 9 kịch bản ở mục 7 chạy đúng.
- Không sửa hành vi luồng freelancer, booking, avatar; không thêm package NuGet; không đụng các thay đổi chưa commit có sẵn.

## 9. Khi gặp điều chưa rõ

- Nghiệp vụ không có trong tài liệu này hoặc `PROJECT_IDEA.md`: chọn phương án đơn giản nhất, ghi `// TODO: POLICY` và liệt kê trong báo cáo cuối.
- Cần đổi schema: dừng lại và hỏi.
- Báo cáo cuối gồm: file thêm/sửa theo phase, kết quả build/test, kết quả 9 kịch bản, các giả định đã tự đặt, những gì chưa làm được.
