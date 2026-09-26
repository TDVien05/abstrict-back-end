# ABSTRICT — Backend architecture (ASP.NET Core 8)

**Tài liệu dùng chung:** `PROJECT_IDEA.md`. Backend dùng kiến trúc cơ bản **Controller → Service → Repository** trong **một dự án Web API**.

## 1. Luồng xử lý và trách nhiệm

```text
React → Controller → Service → Repository → EF Core DbContext → PostgreSQL
```

| Thành phần | Trách nhiệm |
| --- | --- |
| Controller | Nhận HTTP request, xác thực/phân quyền ban đầu, validate định dạng, gọi Service, trả HTTP response và DTO. Không truy cập DbContext hoặc tính giá. |
| Service | Xử lý nghiệp vụ, quyền trên từng tài nguyên, thay đổi trạng thái, giá, lịch, giao dịch nhiều bảng; gọi Repository và các tích hợp ngoài. |
| Repository | Đọc/ghi dữ liệu bằng EF Core, query/filter/pagination. Không tự quyết định chính sách hủy hay chuyển trạng thái. |
| DbContext | EF Core mapping, migration và transaction với PostgreSQL. |

Controller phụ thuộc interface của Service; Service phụ thuộc interface của Repository. Implementation được đăng ký qua DI trong `Program.cs`. Không bắt buộc generic repository; truy vấn riêng cho từng module dễ bảo trì hơn.

## 2. Cấu trúc repo

```text
Abstrict.sln
src/Abstrict.Api/
  Controllers/              # Auth, Providers, Bookings, Payments, ...
  Services/
    Interfaces/
    Implementations/
  Repositories/
    Interfaces/
    Implementations/
  Data/
    AppDbContext.cs
    Configurations/
    Migrations/
  Models/
    Entities/
    Enums/
  DTOs/
    Requests/
    Responses/
  Integrations/
    Payments/
    Storage/
    Notifications/
  BackgroundJobs/
  Middleware/
  Common/
  Program.cs
tests/Abstrict.Api.Tests/
```

Bản đầu không cần tách project Domain/Application/Infrastructure, CQRS, MediatR hay event bus. Nếu quy mô tăng, có thể tách project sau mà vẫn giữ luồng Controller → Service → Repository.

## 3. Module

| Module | Service chính | Repository chính |
| --- | --- | --- |
| Tài khoản và duyệt | Auth, Profile, Approval | User, Freelancer, Company, KYC |
| Khám phá và lịch | Discovery, Availability | Provider, Availability, Booking |
| Đặt lịch | Booking, Pricing, CheckIn, Extension | Booking, BookingTask, QR, BookingPhoto |
| Tiền | Payment, Refund | Payment, MoneyMovement |
| Gói đẩy hồ sơ | Promotion | Plan, PromotionPurchase, Metric |
| Đánh giá/khiếu nại | Rating, Dispute | Rating, Dispute, Evidence, Decision |

PostgreSQL + EF Core 8; UUID cho ID; tiền VND là số nguyên đồng; timestamp lưu UTC. Ảnh/KYC/bằng chứng lưu object storage, DB lưu metadata và quyền sở hữu. `Payment` ghi giao dịch với cổng; `MoneyMovement` ghi nghĩa vụ/biến động tiền (cọc, hoàn, chia phí), không gộp hai khái niệm.

## 4. Quy tắc đặt trong Service

- `AvailabilityService`: freelancer công bố khoảng rảnh và hệ thống hiển thị cả rảnh/bận; ca đặt phải nằm trọn một khoảng liên tục và **tối thiểu 2 giờ**. Công ty không công khai lịch rảnh.
- `PricingService`: báo giá ở server theo giá/giờ, thời lượng, phụ phí được cấu hình; snapshot giá gốc và cọc **15%**. Không chấp nhận tổng tiền client gửi làm nguồn sự thật.
- `BookingService`: tạo hold, xác nhận/từ chối, hủy, check-in, nghiệm thu và chuyển trạng thái hợp lệ. Freelancer xác nhận (hoặc tự xác nhận nếu bật); đơn công ty xác nhận sau cọc thành công.
- `BookingService`/Cancellation: khách hủy khi còn **từ 2 giờ trở lên** trước giờ bắt đầu đã xác nhận thì hoàn đủ cọc; dưới 2 giờ giữ 15% giá gốc (10% cho bên cung cấp, 5% cho nền tảng). Bên cung cấp hủy thì hoàn đủ.
- `CheckInService`: QR đơn hàng do khách hiển thị, ngắn hạn, dùng một lần, chỉ người được gắn với booking quét; không check-in bằng GPS.
- `ExtensionService`: trong ca, trước giờ kết thúc, cả khách và phía cung cấp đồng ý giá/thời lượng mới, kiểm tra không trùng lịch; giá gốc và khoản gia hạn lưu riêng.
- `PromotionService`: chỉ kích hoạt gói sau thanh toán thành công; kín lịch thì ngừng đề xuất nhưng **thời hạn vẫn chạy**.
- `RatingService`: hai bên mỗi bên một đánh giá/booking trong 48 giờ, giữ kín trước công bố; khi có tranh chấp thì hoãn công bố tới lúc đóng.
- `DisputeService`: nhóm an toàn/hư hại/thất lạc cần bằng chứng khi gửi; bên kia phản hồi trong 24 giờ; xin xem xét lại tối đa một lần trong 24 giờ với bằng chứng mới.

Các chính sách được đánh dấu **Cần chốt** trong `PROJECT_IDEA.md` không được hard-code thành cam kết. Đặc biệt cách thu 85% còn lại và giá bán gói vẫn chờ quyết định.

## 5. API, bảo mật và phối hợp với Frontend

- Version `/api/v1`; controller theo tài nguyên: Auth, Profiles, Providers, Availability, Bookings, Payments, Promotions, Ratings, Disputes, Admin.
- Xuất Swagger/OpenAPI cho FE sinh TypeScript client. DTO tách entity; response phân trang `{items,page,pageSize,totalItems,totalPages}`. Lỗi `ProblemDetails` có `code` ổn định: `SLOT_UNAVAILABLE`, `DEPOSIT_REQUIRED`, `QR_EXPIRED`.
- ASP.NET Core auth với vai trò Customer/Freelancer/Company/Admin. Controller kiểm tra quyền truy cập; Service kiểm tra quyền sở hữu booking/ảnh/bằng chứng và quyền người công ty được ủy quyền. Không lộ địa chỉ, KYC hay ảnh trong endpoint công khai.
- Webhook thanh toán xác thực chữ ký và xử lý idempotent; không đánh dấu tiền đã chuyển chỉ vì booking hoàn thành.

## 6. Transaction và tác vụ định kỳ

- Tạo hold/booking và gia hạn kiểm tra chồng lấn **trong transaction**, dùng DB constraint hoặc khóa phù hợp để chống hai khách book trùng freelancer. Hold có hạn dùng; giải phóng khi hết hạn/thanh toán thất bại.
- Service quản lý transaction cho use case cập nhật nhiều bảng; các Repository chia sẻ DbContext theo request và không `SaveChanges` giữa chừng tùy ý. Không mở transaction DB trong lúc chờ dịch vụ thanh toán ngoài.
- Background jobs xử lý hold hết hạn, gói hết hạn, mốc đánh giá, công bố rating và thông báo; mỗi job chạy lặp an toàn.
- Audit việc duyệt, sửa giá, đổi trạng thái, QR, tiền và quyết định tranh chấp; không ghi token, mật khẩu hoặc nội dung KYC vào log.

## 7. Thứ tự triển khai và kiểm chứng

1. Web API, EF Core migrations, auth, Swagger, DI, Controller–Service–Repository.
2. Hồ sơ/duyệt, checklist/dịch vụ, khám phá đối tác, lịch freelancer.
3. Báo giá/hold/booking, cọc sandbox, hủy/hoàn, QR, ảnh và nghiệm thu.
4. Gia hạn, gói, rating, khiếu nại và jobs.

Test trọng điểm: ca dưới 2 giờ; đặt/gia hạn trùng giờ; hủy đúng mốc 2 giờ; webhook lặp; QR một lần; quyền xem ảnh; rating không lộ khi khiếu nại còn mở.
