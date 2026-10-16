# ABSTRICT Backend — Tổng hợp chức năng đang có

Tài liệu này liệt kê toàn bộ chức năng đã triển khai trong repo backend tại thời điểm hiện tại, kèm endpoint, vai trò và ghi chú nghiệp vụ. Nghiệp vụ gốc xem `PROJECT_IDEA.md`; kiến trúc xem `BACKEND_ARCHITECTURE.md`; chi tiết API từng module xem các file trong `docs/`.

- **Stack:** ASP.NET Core 8 Web API · EF Core 8 · PostgreSQL 16.
- **Base URL:** `/api/v1`. Lỗi trả `ProblemDetails` kèm `code` ổn định (`UPPER_SNAKE_CASE`) và `traceId`.
- **Kiến trúc:** Controller → Service (interface + implementation) → `AppDbContext`. Không dùng Repository/MediatR/AutoMapper/FluentValidation.
- **Quy ước dữ liệu:** ID là UUID; thời điểm lưu/trao đổi là UTC (ISO 8601), ngày lịch theo `Asia/Ho_Chi_Minh`; tiền VND là `long` số nguyên đồng; enum JSON là chuỗi.
- **Vai trò:** `Customer`, `Freelancer`, `Company`, `Admin`.

---

## 1. Xác thực và tài khoản

Đăng ký bằng số điện thoại + OTP (không cần email). Đăng nhập bằng số điện thoại/mật khẩu, trả JWT access token (15 phút) và đặt cookie refresh `HttpOnly` (30 ngày).

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/auth/register` | public | Đăng ký tài khoản khách hàng, phát OTP đăng ký. |
| POST | `/auth/register-freelancer` | public | Như trên nhưng tạo tài khoản `Freelancer`. |
| POST | `/auth/register-company` | public | Như trên nhưng tạo tài khoản `Company`. |
| POST | `/auth/verify-phone-otp` | public | Xác thực OTP, kích hoạt tài khoản. |
| POST | `/auth/resend-phone-otp` | public | Gửi lại OTP (cooldown 45s, tối đa 5 OTP/số/24h). |
| POST | `/auth/login` | public | Đăng nhập mọi vai trò; trả kèm hồ sơ onboarding theo vai trò. |
| POST | `/auth/refresh` | cookie | Khôi phục phiên, trả access token mới cùng cấu trúc login. |
| POST | `/auth/logout` | cookie | Thu hồi phiên, xóa cookie (204). |

- OTP: 6 chữ số, hết hạn sau 5 phút, khóa sau 5 lần nhập sai; lưu dạng HMAC-SHA256, không lưu mã rõ. Môi trường Development trả `developmentOtpCode` để thử không cần SMS.
- Login trả thêm: `customerProfile` (khách), `freelancerOnboarding` (`providerId`, `kycStatus`, `currentStep`), `companyOnboarding` (`providerId`, `status`, `currentStep`).
- Số điện thoại Việt Nam 10–11 số được chuẩn hóa trước khi lưu; mật khẩu ≥ 8 ký tự gồm chữ và số; lưu bằng `PasswordHasher<User>`.
- Refresh token không đổi sau mỗi lần refresh (nhiều tab dùng chung một phiên). CORS bật `AllowCredentials` cho origin trong `Cors:AllowedOrigins`.

Chi tiết: `CUSTOMER_REGISTRATION_API.md`.

---

## 2. Hồ sơ khách hàng

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/customer/profile` | Customer | Hồ sơ của tôi kèm sổ địa chỉ (mặc định đứng đầu). |
| PUT | `/customer/profile/avatar` | Customer | Tải lên/thay ảnh đại diện (JPG/PNG, ≤ 5 MB). |
| GET | `/customer/profile/avatar` | Customer | Nội dung ảnh đại diện của tôi. |
| DELETE | `/customer/profile/avatar` | Customer | Xóa ảnh đại diện (204). |

Ảnh đại diện xác định định dạng bằng magic bytes, lưu qua `IFileStorage`, phát qua endpoint có phân quyền. Chi tiết: `CUSTOMER_PROFILE_API.md`.

### Sổ địa chỉ khách hàng

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/customer/addresses` | Customer | Danh sách địa chỉ, mặc định trước. |
| POST | `/customer/addresses` | Customer | Thêm địa chỉ; địa chỉ đầu tiên tự là mặc định; tối đa 10. |
| PUT | `/customer/addresses/{id}` | Customer | Sửa địa chỉ. |
| DELETE | `/customer/addresses/{id}` | Customer | Xóa địa chỉ (204); đơn cũ giữ `AddressSnapshot`. |

---

## 3. Onboarding freelancer (KYC) và duyệt hồ sơ

Luồng: đăng ký → OTP → đăng nhập → khai thông tin + CCCD → tải 3 ảnh → gửi duyệt → admin duyệt/yêu cầu chỉnh sửa/từ chối. Trạng thái `Provider.ApprovalStatus`: `Draft → Submitted → UnderReview → Approved | Rejected` (ChangesRequested quay về Draft).

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/freelancer/kyc` | Freelancer | Trạng thái, thông tin (chỉ 4 số cuối CCCD), ảnh, phản hồi admin. |
| PUT | `/freelancer/kyc/profile` | Freelancer | Lưu thông tin; tuổi ≥ 18, CCCD 12 số, chặn trùng CCCD. |
| PUT | `/freelancer/kyc/documents/{type}` | Freelancer | Tải ảnh (`CitizenIdFront`, `CitizenIdBack`, `FaceVerification`); JPG/PNG thật, ≤ 5 MB. |
| POST | `/freelancer/kyc/submit` | Freelancer | Gửi duyệt; cần đủ 3 ảnh, không còn ảnh bị từ chối. |
| GET | `/freelancer/kyc/documents/{id}/content` | Freelancer | Ảnh của chính mình. |
| GET | `/admin/kyc?status=&page=&pageSize=` | Admin | Hàng chờ duyệt (mặc định Submitted + UnderReview). |
| GET | `/admin/kyc/{providerId}` | Admin | Chi tiết, kèm CCCD đã giải mã (ghi audit). |
| POST | `/admin/kyc/{providerId}/start-review` | Admin | Submitted → UnderReview. |
| POST | `/admin/kyc/{providerId}/decision` | Admin | `Approved` / `Rejected` / `ChangesRequested` (lý do ≥ 10 ký tự trừ khi duyệt). |
| GET | `/admin/kyc/{providerId}/documents/{id}/content` | Admin | Ảnh giấy tờ (ghi audit). |

- CCCD mã hóa bằng ASP.NET Data Protection; cột `citizen_id_hash` (HMAC-SHA256) chặn trùng.
- Ảnh lưu ngoài `wwwroot` qua `IFileStorage`, chỉ phát qua endpoint có phân quyền (`Cache-Control: private, no-store`).
- Không dùng OCR/so khớp khuôn mặt tự động; admin đối chiếu bằng mắt.

Chi tiết: `FREELANCER_KYC_API.md`.

---

## 4. Danh bạ freelancer công khai

Không cần đăng nhập; chỉ trả freelancer đã `Approved`. Không bao giờ lộ số điện thoại, CCCD, ngày sinh, địa chỉ, ảnh KYC.

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/freelancers` | public | Tìm kiếm/liệt kê freelancer, có phân trang. |
| GET | `/freelancers/{providerId}` | public | Chi tiết hồ sơ công khai (dịch vụ, khu vực). |
| GET | `/freelancers/{providerId}/availability` | public | Lịch rảnh/bận theo ngày; khoảng bận không kèm thông tin khách/đơn. |

Bộ lọc `/freelancers`: `serviceCode`, `city`, `district`, `minRating`, `acceptingBookings`, `page`, `pageSize`. Sắp xếp theo `averageRating` rồi `completedBookingCount` giảm dần. Chi tiết: `FREELANCER_PROFILE_API.md`.

---

## 5. Onboarding công ty đối tác và duyệt hồ sơ

Luồng: đăng ký (OTP) → đăng nhập → khai pháp nhân → người đại diện → khu vực nhận ca → tải giấy tờ → gửi duyệt → admin duyệt/yêu cầu chỉnh sửa/từ chối. Một tài khoản quản lý đúng một công ty.

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/company/onboarding` | Company | Toàn bộ trạng thái hồ sơ và bước tiếp theo. |
| PUT | `/company/onboarding/company` | Company | Lưu thông tin pháp nhân; lần đầu tạo hồ sơ `Draft`. |
| PUT | `/company/onboarding/representative` | Company | Lưu người đại diện (CCCD mã hóa, chỉ trả 4 số cuối). |
| PUT | `/company/onboarding/service-areas` | Company | Thay toàn bộ khu vực nhận ca (1–20). |
| PUT | `/company/onboarding/documents/{type}` | Company | Tải/thay giấy tờ (`multipart/form-data`). |
| GET | `/company/onboarding/documents/{documentId}/content` | Company | Nội dung giấy tờ của chính mình. |
| POST | `/company/onboarding/submit` | Company | Gửi duyệt. |
| GET | `/admin/companies?status=&page=&pageSize=` | Admin | Hàng chờ duyệt (cũ nhất trước). |
| GET | `/admin/companies/{providerId}` | Admin | Chi tiết, kèm CCCD người đại diện giải mã (ghi audit). |
| POST | `/admin/companies/{providerId}/start-review` | Admin | Submitted → UnderReview. |
| POST | `/admin/companies/{providerId}/decision` | Admin | Duyệt / từ chối / yêu cầu chỉnh sửa. |
| GET | `/admin/companies/{providerId}/documents/{documentId}/content` | Admin | Nội dung giấy tờ (ghi audit). |

- Giấy tờ: `BusinessRegistration` (JPG/PNG/PDF), `CitizenIdFront`/`CitizenIdBack` (JPG/PNG), `LiabilityInsurance` (khi `liabilityCoverageVnd > 0`); tối đa 10 MB/file, nhận diện bằng magic bytes.
- Mã số thuế 10 số hoặc 10 số + `-` + 3 số; trùng công ty khác trả 409 `TAX_CODE_ALREADY_USED`.
- **Lưu ý:** hồ sơ duyệt xong vẫn chưa nhận đơn (`isAcceptingBookings = false`); đặt lịch với công ty chưa hỗ trợ.

Chi tiết: `COMPANY_ONBOARDING_API.md`.

---

## 6. Danh mục (catalog)

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/catalog/service-areas` | public | Khu vực dịch vụ đang hoạt động. |
| GET | `/catalog/service-categories` | public | Danh mục dịch vụ (mã, tên). |
| GET | `/catalog/banks` | public | Danh sách ngân hàng. |
| GET | `/catalog/service-categories/{code}/checklist` | public | Checklist template đang dùng của danh mục. |
| GET | `/catalog/administrative-divisions` | public | Tỉnh/thành + quận/huyện (lọc `?city=`), kèm cờ `isServed`. |
| GET | `/catalog/administrative-divisions/provinces/{provinceCode}/districts/{districtCode}/wards` | public | Phường/xã của quận/huyện. |

- Dữ liệu hành chính lấy từ **Cas AddressKit**, cache trong bộ nhớ 24 giờ, có fallback danh mục nhúng; dùng bản ngày `2025-06-30` (3 cấp) vì hệ thống đặt lịch vẫn theo quận/huyện.
- Khu vực dịch vụ hiện giới hạn ở 22 quận/huyện TP. Hồ Chí Minh (migration `RestrictServiceAreasToHoChiMinh`).
- Danh mục dịch vụ dùng cho freelancer: `CLEANING`, `DEEP_CLEANING`, `COOKING`, `LAUNDRY`, `CHILDCARE`, `ELDERLY_CARE`, `AC_SERVICE`, `ELECTRICAL`, `PLUMBING`, `MOVING`, `GARDENING`, `PEST_CONTROL`.

---

## 7. Lịch rảnh của freelancer

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/freelancer/availability?from=&days=` | Freelancer | Các khoảng rảnh của tôi. |
| POST | `/freelancer/availability` | Freelancer | Thêm khoảng rảnh: bước 30 phút, ≥ 2 giờ, ở tương lai, không chồng khoảng khác. |
| DELETE | `/freelancer/availability/{id}` | Freelancer | Xóa khoảng rảnh chưa bị đơn chiếm chỗ. |

Chỉ hồ sơ đã `Approved` mới khai báo được (`PROVIDER_NOT_APPROVED`). Công ty không công khai lịch rảnh.

---

## 8. Đặt lịch (booking) — luồng khách hàng

Trạng thái: `PendingDeposit → PendingProviderConfirmation → Confirmed → CheckedIn → InProgress → PendingAcceptance → Completed`; các nhánh `Expired`, `Rejected`, `Cancelled`, `Disputed`.

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/bookings/quote` | Customer | Báo giá, không ghi DB (không tính vào rate limit). |
| POST | `/bookings` | Customer | Tạo đơn `PendingDeposit` và giữ chỗ (201). |
| GET | `/bookings?status=&page=&pageSize=` | Customer | Đơn của tôi, mới nhất trước, phân trang. |
| GET | `/bookings/{id}` | Customer | Chi tiết đơn kèm checklist, timeline, `allowedActions`. |
| POST | `/bookings/{id}/deposit` | Customer | Khởi tạo thanh toán cọc (cần header `Idempotency-Key`). |
| GET | `/bookings/{id}/cancellation-preview` | Customer | Số tiền hoàn/giữ nếu hủy ngay. |
| POST | `/bookings/{id}/cancel` | Customer | Hủy đơn. |
| POST | `/bookings/{id}/check-in-token` | Customer | Phát hành QR check-in ngắn hạn, dùng một lần. |
| GET | `/bookings/{id}/photos/{photoId}/content` | Customer | Nội dung ảnh tiến độ trong đơn của tôi. |

Quy tắc chính:

- **Giá (server tính):** giá gốc = đơn giá/giờ × thời lượng; **cọc 15%** giá gốc (làm tròn lên); phụ phí 0 (`TODO: POLICY`). Không tin số tiền client gửi.
- **Chọn giờ:** bước 30 phút, ca 2–12 giờ, đặt trước 60 phút đến 30 ngày; giờ phải nằm trọn một khoảng rảnh liên tục, không trùng booking đang chiếm slot.
- **Chống trùng:** transaction khóa dòng + PostgreSQL exclusion constraint `btree_gist` (`ex_bookings_provider_slot`); vi phạm → `SLOT_UNAVAILABLE`.
- **Hủy do khách:** `PendingDeposit` không phát sinh tiền; `PendingProviderConfirmation` hoàn đủ cọc; `Confirmed` còn ≥ 2 giờ thì hoàn đủ cọc, dưới 2 giờ giữ 15% giá gốc (10% cho provider, 5% nền tảng).
- **QR:** token 32 byte ngẫu nhiên, chỉ lưu SHA-256 hash, sống 120 giây, dùng một lần, chỉ phát khi `Confirmed` và trong khung 30 phút trước giờ bắt đầu đến giờ kết thúc.

Chi tiết: `BOOKING_API.md`.

### Phía freelancer

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| GET | `/freelancer/bookings?status=&page=&pageSize=` | Freelancer | Đơn gán cho tôi (không gồm đơn chưa từng có cọc). |
| GET | `/freelancer/bookings/counts` | Freelancer | Số đơn theo từng trạng thái (cho tab). |
| GET | `/freelancer/bookings/{id}` | Freelancer | Chi tiết; địa chỉ đầy đủ chỉ sau khi `Confirmed`. |
| POST | `/freelancer/bookings/{id}/confirm` | Freelancer | Xác nhận nhận đơn. |
| POST | `/freelancer/bookings/{id}/reject` | Freelancer | Từ chối; khách được hoàn đủ cọc. |
| POST | `/freelancer/bookings/{id}/check-in` | Freelancer | Quét QR của khách; đơn chuyển `CheckedIn`. |
| PATCH | `/freelancer/bookings/{id}/tasks/{taskId}` | Freelancer | Đổi trạng thái mục checklist (`Pending`/`InProgress`/`Completed`) trong ca. |
| POST | `/freelancer/bookings/{id}/photos` | Freelancer | Tải ảnh tiến độ Before/After; ảnh Before đầu tiên chuyển `InProgress`. |
| GET | `/freelancer/bookings/{id}/photos/{photoId}/content` | Freelancer | Nội dung ảnh tiến độ. |
| POST | `/freelancer/bookings/{id}/complete` | Freelancer | Hoàn tất ca (cần ≥ 1 ảnh After) → `PendingAcceptance`. |

Quyền riêng tư: trước `Confirmed` trả `addressSnapshot = null`, chỉ `areaLabel`; không bao giờ trả số điện thoại khách.

---

## 9. Thanh toán (sandbox)

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/payments/sandbox/{paymentId}/confirm` | Customer | Chốt kết quả giao dịch giả lập (`{ succeeded }`). |

- Chỉ tồn tại khi `Payments:SandboxEnabled = true` (mặc định bật ở Development), ngược lại 404.
- Cổng sandbox đặt sau interface `IPaymentGateway` để thay PSP thật sau này. Idempotent: gọi lại không đổi kết quả đã chốt.
- `Payment` ghi giao dịch với cổng; `MoneyMovement` ghi nghĩa vụ/biến động tiền (cọc, hoàn, chia phí) với `Reference` unique làm khóa idempotent.

---

## 10. Tác vụ nền

- **`BookingExpiryJob`** (`BackgroundService`, chu kỳ `Booking:ExpiryJobIntervalSeconds`): giải phóng hold `PendingDeposit` hết hạn và đơn `PendingProviderConfirmation` quá hạn phản hồi; mỗi đơn một scope/transaction riêng, lỗi một đơn không dừng vòng lặp.
- **`AdminAccountSeeder`**: tạo tài khoản admin đầu tiên theo `Admin:SeedPhoneNumber`/`Admin:SeedPassword`.
- **`DemoDataSeeder`**: khi `Demo:SeedFreelancers=true`, tự tạo freelancer demo + lịch rảnh + khách thử khi khởi động (idempotent).

---

## 11. Bảo mật

- **JWT Bearer** (access 15 phút) + cookie refresh `HttpOnly` (30 ngày). Production bắt buộc `Jwt:SigningKey` ≥ 32 byte.
- **Data Protection** mã hóa số CCCD; khóa lưu ở `DataProtection:KeysPath` (mất thư mục là mất khả năng giải mã).
- **Phân quyền theo vai trò** ở controller; service luôn kiểm tra quyền sở hữu tài nguyên.
- **Rate limit**: `kyc-write`/`booking-write` (30/10 phút/user), `avatar-write` (10/10 phút), `auth-login` (10/phút/IP), `auth-register` (5/10 phút/IP), `auth-refresh`, `auth-otp-verify`, `auth-otp-resend`, `kyc-operation`.
- **Audit**: ghi `AuditLog` cho duyệt hồ sơ, xem giấy tờ, đổi trạng thái, tiền, QR, quyết định tranh chấp; không ghi token/mật khẩu/CCCD thô.
- Endpoint công khai không lộ địa chỉ, số điện thoại, KYC hay thông tin đơn.

---

## 12. Kiểm thử và dữ liệu thử

- **Unit test** (`tests/Abstrict.Api.Tests`): `BookingPolicyTests` (giá, thời lượng, gộp khoảng rảnh, hủy, bảng trạng thái), `CompanyOnboardingTests`, `KycTests`, `AdministrativeDirectoryTests`, `ArchitectureTests`.
- **Seed script** (`scripts/`): `seed-fake-freelancers.ps1` (8 freelancer, 5 Approved), `seed-booking-fixtures.ps1` (lịch rảnh 08–12h & 13–18h giờ VN trong 14 ngày + khách thử), `seed-freelancer-avatars.ps1`.
- **Tài khoản Development:** admin `0900000001` / `Abstrict-dev-admin-2026`; freelancer `0910000001` / `Freelancer-dev-2026`; khách `0920000001` / `Customer-dev-2026`.
- **Docker Compose**: `api` (8080), `postgres` (5432), `pgadmin` (5050). Xem `DOCKER.md`.

---

## 13. Chưa triển khai (đã có entity/bảng nhưng chưa có nghiệp vụ)

Các phần sau thuộc giai đoạn sau, **không** được tạo stub:

- Nghiệm thu phía khách (`PendingAcceptance → Completed`) và gia hạn ca.
- Thu 85% còn lại, chi trả cho freelancer, cổng thanh toán (PSP) thật, webhook.
- Đánh giá hai chiều (`Rating`) và khiếu nại (`Dispute`, bằng chứng, quyết định, xem xét lại).
- Gói tăng hiển thị (`PromotionPlan`, `PromotionPurchase`, `ProviderMetricDaily`).
- Quản lý người làm của công ty (`CompanyWorker`, `CompanyWorkerAssignment`), bảng giá/dịch vụ công ty, tài khoản ngân hàng, ví đối tác.
- Hiển thị công ty trong danh bạ công khai và đặt lịch với công ty (`BookingService` hiện trả `PROVIDER_NOT_BOOKABLE` cho công ty).
- Thông báo ra ngoài (SMS/push) — hiện chỉ ghi bản ghi `Notification` vào DB.
- Kênh đẩy SSE/WebSocket (FE hiện gọi lại định kỳ).

---

## 14. Cấu trúc thư mục chính

```text
src/Abstrict.Api/
  Controllers/          # Auth, Catalog, Freelancers, Bookings, Payments, KYC, Company, Customer, Admin...
  Services/
    Interfaces/
    Implementations/    # *Service, *Policy, BookingWorkflow, seeders, helpers
  Data/
    AppDbContext, Configurations/, Migrations/
  Models/               # Entities/, Enums/
  DTOs/                 # Requests/, Responses/
  Integrations/         # Payments/ (IPaymentGateway, SandboxPaymentGateway)
                        # Storage/  (IFileStorage, LocalFileStorage)
                        # Notifications/ (IPhoneOtpSender và các sender)
  Options/              # BookingOptions, PaymentOptions, KycOptions, AddressKitOptions
  BackgroundJobs/       # BookingExpiryJob
  Common/               # ApiRoutes
  Program.cs
tests/Abstrict.Api.Tests/
scripts/                # seed scripts (PowerShell)
docs/                   # tài liệu API và kế hoạch
```
