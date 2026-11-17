# ABSTRICT — Backend

REST API cho nền tảng tìm kiếm và đặt lịch giúp việc theo giờ tại TP.HCM: khách hàng đặt freelancer/công ty giúp việc, đặt cọc, check-in QR, nghiệm thu, đánh giá hai chiều, khiếu nại và gói tăng hiển thị.

- **Bản chạy thử:** https://abstrict.vientruong.site (frontend) · https://abstrict.api.vientruong.site (API)
- **Repo frontend:** https://github.com/TDVien05/abstrict-front-end

## Công nghệ

| Lớp | Công nghệ |
| --- | --- |
| Runtime | .NET 8 (SDK `8.0.425`, xem `global.json`), ASP.NET Core Web API |
| Dữ liệu | PostgreSQL 16, EF Core 8 + Npgsql, migration trong `src/Abstrict.Api/Data/Migrations` |
| Xác thực | Đăng ký/đăng nhập bằng số điện thoại + OTP, JWT access token (15 phút), refresh token cookie `HttpOnly` (30 ngày) |
| Thanh toán | Cổng `Sandbox` (mặc định cho dev) và tích hợp SePay |
| Khác | Swagger, background job hết hạn booking, lưu file cục bộ (ảnh KYC, check-in, bằng chứng) |
| Kiểm thử | xUnit (`tests/Abstrict.Api.Tests`) |
| Triển khai | Docker / Docker Compose, GitHub Actions CI (`.github/workflows/ci.yml`) |

## Cấu trúc thư mục

```text
src/Abstrict.Api/
  Controllers/      # Auth, Customer, Freelancer(s), Partner, Bookings, Payments,
                    # Disputes, Admin, CompanyOnboarding, Catalog, Health ...
  Services/         # Interfaces/ + Implementations/ (nghiệp vụ, seeder)
  DTOs/             # Requests/ và Responses/
  Models/           # Entities/ và Enums/
  Data/             # AppDbContext, Configurations/, Migrations/, Reference/
  Integrations/     # Payments, Storage, Notifications
  BackgroundJobs/   # BookingExpiryJob
  Options/          # Cấu hình có kiểu (Booking, Kyc, Payment, AddressKit)
  Program.cs        # DI, middleware, seed khi khởi động
tests/Abstrict.Api.Tests/   # Unit test chính sách nghiệp vụ, kiến trúc
scripts/                    # Script PowerShell tạo dữ liệu mẫu
docs/                       # Tài liệu API từng module
```

Luồng: `Controller → Service → AppDbContext`. Tài liệu chi tiết: `PROJECT_IDEA.md` (nghiệp vụ), `BACKEND_ARCHITECTURE.md`, `docs/FEATURES.md` (danh sách endpoint), `docs/DOCKER.md`.

Quy ước: base URL `/api/v1`; ID là UUID; thời gian UTC (ISO 8601); tiền VND là số nguyên; lỗi trả `ProblemDetails` kèm `code` ổn định.

## Hướng dẫn test trên bản chạy thử

1. Mở https://abstrict.vientruong.site.
2. Đăng nhập bằng số điện thoại + mật khẩu của một tài khoản bên dưới, hoặc tự đăng ký tài khoản mới (khách hàng, freelancer, công ty).
3. Kiểm tra API còn sống: `GET https://abstrict.api.vientruong.site/health`.

### Tài khoản test

Dữ liệu demo do `DemoDataSeeder` tạo; các tài khoản demo dùng chung mật khẩu đặt qua biến `Demo__SeedPassword` trên server (không commit vào repo).

| Vai trò | Số điện thoại | Ghi chú |
| --- | --- | --- |
| Admin | `0900000001` | Duyệt KYC/công ty, quản lý gói, xử lý khiếu nại |
| Khách hàng | `0920000001` | Khách thử, địa chỉ Quận 1 |
| Khách hàng | `0920000002` – `0920000006` | Có đơn mẫu để test đánh giá (nếu đã chạy `seed-rating-fixtures.ps1`) |
| Freelancer (đã duyệt) | `0910000001` – `0910000005` | Có lịch rảnh 14 ngày, nhận đặt lịch. `0910000001` = Nguyễn Thị Lan (dọn dẹp) |
| Freelancer (Submitted) | `0910000006` | Chờ admin duyệt KYC |
| Freelancer (UnderReview) | `0910000007` | Đang xét duyệt |
| Freelancer (Draft) | `0910000008` | Chưa hoàn tất hồ sơ |

> **Mật khẩu:** `<điền mật khẩu demo hiện tại>` — liên hệ chủ repo. Không commit mật khẩu thật vào README.

### Luồng gợi ý để thử

1. Khách `0920000001` → khám phá freelancer → chọn khung giờ rảnh → đặt lịch → thanh toán cọc 15% (cổng sandbox).
2. Freelancer `0910000001` → xác nhận đơn → quét QR check-in của khách → tải ảnh trước/sau → gửi nghiệm thu.
3. Khách nghiệm thu → đánh giá (trong 48 giờ) hoặc mở khiếu nại.
4. Admin `0900000001` → xử lý khiếu nại, duyệt KYC freelancer `0910000006`, quản lý gói tăng hiển thị.

Ở môi trường Development, API trả `developmentOtpCode` trong response để thử đăng ký không cần SMS.

## Chạy local

### Yêu cầu

- .NET SDK 8.0.x, PostgreSQL 16 (hoặc Docker), Git

### Cách 1 — Docker Compose (khuyến nghị)

```powershell
Copy-Item .env.docker.example .env.docker   # đổi các mật khẩu trong file này
docker compose --env-file .env.docker up -d --build
```

- API: http://localhost:8080 · Health: `/health` · Swagger: `/swagger`
- pgAdmin: http://localhost:5050
- Migration tự áp dụng (`Database__ApplyMigrations=true`). Dừng: `docker compose --env-file .env.docker down`. Xem thêm `docs/DOCKER.md`.

### Cách 2 — Chạy trực tiếp với .NET

```powershell
# 1. Tạo database PostgreSQL "abstrict". Mặc định trong appsettings.json là postgres/postgres,
#    hoặc ghi đè bằng biến môi trường:
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=abstrict;Username=postgres;Password=postgres"
$env:Database__ApplyMigrations = "true"

# 2. Chạy API (Development: OTP lộ ra response, thanh toán Sandbox, tạo admin dev)
dotnet run --project src/Abstrict.Api
```

Cổng nằm trong `src/Abstrict.Api/Properties/launchSettings.json` (3541 hoặc 5191 tùy profile). Có thể áp migration thủ công:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Abstrict.Api
```

### Cấu hình quan trọng

| Khóa (biến môi trường dùng `__`) | Ý nghĩa |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | Chuỗi kết nối PostgreSQL |
| `Database__ApplyMigrations` | Tự áp migration khi khởi động |
| `Cors__AllowedOrigins__0` | Origin frontend được phép (mặc định `https://abstrict.vientruong.site`; thêm `http://localhost:5173` khi dev) |
| `Admin__SeedPhoneNumber` / `Admin__SeedPassword` | Tạo admin đầu tiên (mật khẩu ≥ 12 ký tự); giá trị dev nằm trong `appsettings.Development.json` |
| `Demo__SeedFreelancers` / `Demo__SeedPassword` | Bật dữ liệu demo (8 freelancer + khách thử), mật khẩu ≥ 12 ký tự |
| `Payments__Provider` | `Sandbox` hoặc `Sepay` (kèm khóa SePay) |
| `Otp__HmacKey`, `Kyc__CitizenIdHashKey` | Khóa bí mật, bắt buộc đặt riêng ở production |

### Dữ liệu mẫu (PowerShell, tùy chọn)

`scripts/seed-fake-freelancers.ps1`, `seed-booking-fixtures.ps1`, `seed-rating-fixtures.ps1`, `seed-freelancer-avatars.ps1`.

### Test

```powershell
dotnet test
```

Chạy cùng frontend (`http://localhost:5173`, proxy `/api` tới API local): xem README repo frontend.

## Tài liệu API

Thư mục `docs/`: `FEATURES.md`, `BOOKING_API.md`, `FREELANCER_KYC_API.md`, `FREELANCER_PROFILE_API.md`, `COMPANY_ONBOARDING_API.md`, `CUSTOMER_PROFILE_API.md`, `PROMOTION_PLANS_API.md`, `RATINGS_API.md`.
