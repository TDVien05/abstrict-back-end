# Plan triển khai: luồng booking cho khách hàng (Customer)

Tài liệu này là bản giao việc cho một AI coding agent. Bạn làm việc trong repo `backend` (ASP.NET Core 8, EF Core 8, PostgreSQL 16). Hãy làm **tuần tự từng phase**, mỗi phase build xanh + test xanh rồi mới sang phase sau. Không tự mở rộng phạm vi ngoài mục 2.

---

## 1. Bối cảnh

ABSTRICT là nền tảng đặt lịch giúp việc theo giờ. Nghiệp vụ gốc nằm ở `PROJECT_IDEA.md` (mục 2.2) và `BACKEND_ARCHITECTURE.md`; hãy đọc hai file này trước.

Hiện trạng code:

- **Đã có:** đăng ký/đăng nhập khách (JWT), KYC freelancer, admin duyệt KYC, danh bạ freelancer công khai (`GET /api/v1/freelancers`), danh mục (`/api/v1/catalog/*`).
- **Đã có entity + bảng nhưng chưa có code nghiệp vụ:** `Booking`, `BookingTask`, `BookingStatusHistory`, `CheckInToken`, `Payment`, `MoneyMovement`, `AvailabilityWindow`, `CustomerAddress`, `ChecklistTemplate`, `ChecklistTemplateItem`, `Notification`. Xem `src/Abstrict.Api/Models/Entities/` và `Models/Enums/DomainEnums.cs`.
- **Chưa có dữ liệu:** bảng `availability_windows`, `checklist_templates`, `checklist_template_items`, `customer_addresses`, `bookings` đều trống.
- **Chưa có:** tích hợp cổng thanh toán (PSP chưa chốt), background job, thư mục `Repositories/`.

Mục tiêu: khách đã đăng nhập có thể chọn freelancer → chọn giờ trong lịch rảnh → xem báo giá → tạo booking (giữ slot) → trả cọc 15% (sandbox) → freelancer xác nhận → khách xem đơn, hủy đơn, lấy QR check-in.

## 2. Phạm vi

**Trong phạm vi**

1. CRUD địa chỉ của khách.
2. Freelancer khai báo lịch rảnh (tối thiểu: tạo/xem/xóa khoảng rảnh) và API công khai xem lịch rảnh/bận.
3. API lấy checklist theo danh mục dịch vụ.
4. Báo giá, tạo booking kèm giữ slot, chống đặt trùng ở cấp database.
5. Thanh toán cọc qua cổng **sandbox** nội bộ, sau một interface để thay PSP thật.
6. Freelancer xác nhận/từ chối (cần để booking đi tới `Confirmed`); tự xác nhận khi `AutoAcceptBookings = true`.
7. Job nền: hết hạn giữ slot, hết hạn chờ freelancer xác nhận.
8. Khách xem danh sách/chi tiết đơn, xem trước và thực hiện hủy đơn theo chính sách 2 giờ.
9. Khách tạo QR check-in (chỉ phát hành token).

**Ngoài phạm vi (không làm, không tạo stub)**

- Đối tác công ty (`ProviderType.Company`): chỉ hỗ trợ freelancer. Request tới provider là công ty trả `PROVIDER_NOT_BOOKABLE`.
- Quét/redeem QR, checklist trong ca, ảnh trước/sau, nghiệm thu, gia hạn, đánh giá, khiếu nại.
- PSP thật, thu 85% còn lại, chi trả cho freelancer.
- Gửi thông báo ra ngoài (SMS/push). Chỉ ghi bản ghi `Notification` vào DB.

## 3. Quy ước bắt buộc của repo

Làm giống code hiện có, không áp kiến trúc mới.

| Chủ đề | Quy ước | File mẫu |
| --- | --- | --- |
| Tầng | Controller → Service (interface + implementation). Service dùng thẳng `AppDbContext`. **Không** tạo Repository, MediatR, AutoMapper, FluentValidation. | `Services/Implementations/FreelancerKycService.cs` |
| Controller | `[ApiController]`, route `ApiRoutes.Version1 + "/..."`, primary constructor, `sealed`. Không chạm `DbContext`, không tính giá. | `Controllers/FreelancerKycController.cs` |
| Phân quyền | `[Authorize(Roles = nameof(UserRole.Customer))]`. User id lấy từ claim `ClaimTypes.NameIdentifier`. Service luôn kiểm tra quyền sở hữu tài nguyên. | `Controllers/KycControllerBase.cs` |
| Lỗi nghiệp vụ | Ném exception có `StatusCode`, `Code`, `Message`; controller base đổi thành `ProblemDetails` có `extensions.code` và `traceId`. `Message` tiếng Việt, `Code` là `UPPER_SNAKE_CASE`. | `KycFlowException` trong `CitizenIdProtector.cs`, `KycControllerBase.Run` |
| DTO | Request: `sealed class` với `init` + DataAnnotations, đặt ở `DTOs/Requests`. Response: `sealed record`, đặt ở `DTOs/Responses`. Không trả entity. | `DTOs/Requests/KycRequests.cs`, `DTOs/Responses/FreelancerDirectoryResponses.cs` |
| Phân trang | `PagedResponse<T>` có sẵn. `page >= 1`, `pageSize` kẹp 1..50. | `FreelancerDirectoryService.SearchAsync` |
| Thời gian | Lưu và trả UTC (`DateTimeOffset`, ISO 8601). Lấy "bây giờ" qua `TimeProvider` được inject, **không** dùng `DateTimeOffset.UtcNow` trong service. Múi giờ hiển thị/ngày lịch là `Asia/Ho_Chi_Minh` (UTC+7, không DST). | `FreelancerKycService` |
| Tiền | `long` VND, số nguyên đồng. Không dùng `double`/`decimal` cho tiền. Server tự tính, không tin số tiền client gửi. | `Booking.cs` |
| Enum | JSON là chuỗi (đã cấu hình `JsonStringEnumConverter`). Cột DB là chuỗi. | `Program.cs` |
| DB | Tên bảng/cột snake_case do convention tự sinh. Cấu hình entity trong `Data/Configurations/AbstrictModelConfiguration.cs`. | — |
| DI | Đăng ký trong `Program.cs` bằng `AddScoped<IX, X>()`. | `Program.cs` |
| Swagger | XML doc `/// <summary>` tiếng Việt cho controller/action, `[ProducesResponseType]` cho các mã chính. | `Controllers/CatalogController.cs` |
| Giá trị chính sách chưa chốt | Đưa vào options (mục 5), kèm comment `// TODO: POLICY`. Không hard-code trong service. | `Options/KycOptions.cs` |

**Quy tắc migration (đã từng gây sự cố, phải tuân thủ)**

- Không sửa migration đã có. Chỉ thêm migration mới bằng `dotnet ef migrations add <Name> --project src/Abstrict.Api`.
- Migration phải chạy được trên DB **đang có dữ liệu**. Thêm cột `NOT NULL` phải có default hợp lệ; thêm unique/exclusion constraint phải chắc chắn dữ liệu hiện tại không vi phạm.
- Sau khi thêm: `dotnet ef migrations has-pending-model-changes --project src/Abstrict.Api` phải báo không có thay đổi.
- Working tree đang có thay đổi chưa commit ở `Data/Configurations`, `Data/Migrations` và `scripts/`. **Không revert, không sửa** các thay đổi đó.

## 4. Mô hình nghiệp vụ

### 4.1 Trạng thái trong phạm vi

```text
PendingDeposit ──cọc thành công──▶ PendingProviderConfirmation ──freelancer xác nhận──▶ Confirmed
      │                                   │  (AutoAcceptBookings: nhảy thẳng Confirmed)        │
      │ hết hạn giữ slot                  │ từ chối → Rejected (hoàn đủ cọc)                    │ khách hủy
      ▼                                   │ hết hạn xác nhận → Expired (hoàn đủ cọc)            ▼
   Expired                                └ khách hủy → Cancelled (hoàn đủ cọc)             Cancelled
      ▲
      └ khách hủy khi chưa cọc → Cancelled (không có tiền)
```

Mọi lần đổi trạng thái phải: (a) chạy trong transaction có khóa dòng booking (`SELECT ... FOR UPDATE`, xem 4.5), (b) thêm một dòng `BookingStatusHistory`, (c) cập nhật `UpdatedAtUtc`. Viết một helper duy nhất làm ba việc này; chuyển trạng thái không hợp lệ ném `BOOKING_INVALID_STATE` (409).

### 4.2 Quy tắc chọn giờ

Một yêu cầu đặt `[startsAtUtc, endsAtUtc)` hợp lệ khi tất cả đúng:

1. Provider tồn tại, `Type = Freelancer`, `ApprovalStatus = Approved`, `IsAcceptingBookings = true`; nếu không → `PROVIDER_NOT_BOOKABLE` (409).
2. Provider có `ProviderService` đang `IsActive` cho `serviceCategoryId`; nếu không → `SERVICE_NOT_OFFERED` (409).
3. `startsAtUtc` và `endsAtUtc` rơi đúng bước `SlotStepMinutes` (giây = 0) → sai thì `INVALID_TIME_RANGE` (400).
4. Thời lượng ≥ `MinimumDurationMinutes` (120) và ≤ `MaximumDurationMinutes` → `DURATION_TOO_SHORT` / `DURATION_TOO_LONG` (400).
5. `startsAtUtc ≥ now + MinimumLeadMinutes` và `startsAtUtc ≤ now + MaximumAdvanceDays` → `START_TOO_SOON` / `START_TOO_FAR` (400).
6. Khoảng đặt nằm trọn trong **một khoảng rảnh liên tục**: gộp các `AvailabilityWindow` đang `IsActive` của provider mà chồng hoặc chạm nhau thành khoảng liên tục, rồi kiểm tra chứa trọn → sai thì `OUTSIDE_AVAILABILITY` (409).
7. Không chồng với booking "đang chiếm slot" của cùng provider → `SLOT_UNAVAILABLE` (409). Trạng thái chiếm slot: `PendingDeposit`, `PendingProviderConfirmation`, `Confirmed`, `CheckedIn`, `InProgress`, `PendingAcceptance`.
8. Địa chỉ thuộc về khách, và `(City, District)` của địa chỉ khớp một `ServiceArea` mà provider phục vụ (`ProviderServiceArea`) → `ADDRESS_NOT_FOUND` (404) / `ADDRESS_OUT_OF_SERVICE_AREA` (409).
9. Khách có ít hơn `MaximumActiveHoldsPerCustomer` booking `PendingDeposit` → vượt thì `TOO_MANY_PENDING_BOOKINGS` (409).

Đặt phần kiểm tra thuần (3, 4, 5, 6 sau khi đã nạp dữ liệu) vào class tĩnh `BookingSlotPolicy` để unit test không cần DB.

### 4.3 Giá

Đặt trong class tĩnh `BookingPricing`:

- `UnitPriceVnd` = `ProviderService.HourlyRateVnd` tại thời điểm tạo (snapshot).
- `BaseAmountVnd` = `UnitPriceVnd * durationMinutes / 60` (nhân trước, chia nguyên sau).
- `SurchargeAmountVnd` = 0. `// TODO: POLICY` phụ phí chưa chốt.
- `DepositAmountVnd` = `(BaseAmountVnd * DepositPercent + 99) / 100` (làm tròn lên tới đồng), `DepositPercent = 15`.
- `ExtensionAmountVnd` = 0.

Checklist chỉ mô tả phạm vi việc, **không** cộng tiền.

### 4.4 Hủy do khách

Đặt trong class tĩnh `BookingCancellationPolicy`, trả về `(refundVnd, providerShareVnd, platformShareVnd)`:

| Trạng thái lúc hủy | Kết quả |
| --- | --- |
| `PendingDeposit` | Không có tiền. Hủy các `Payment` đang `Pending` (`Status = Cancelled`). |
| `PendingProviderConfirmation` | Hoàn đủ cọc. |
| `Confirmed`, còn `≥ FreeCancellationMinutes` (120) trước `StartsAtUtc` (đúng mốc 120 phút vẫn miễn phí) | Hoàn đủ cọc. |
| `Confirmed`, còn `< 120` phút | Không hoàn. `providerShare = BaseAmountVnd * 10 / 100`, `platformShare = DepositAmountVnd - providerShare`. |
| Trạng thái khác | `BOOKING_NOT_CANCELLABLE` (409). |

Giả định cần ghi `// TODO: POLICY`: hủy khi freelancer chưa xác nhận luôn được hoàn đủ, kể cả sát giờ.

### 4.5 Chống đặt trùng

Hai lớp, phải có cả hai:

1. **Trong transaction tạo booking:** trước khi insert, chuyển các booking `PendingDeposit` của provider đó có `HoldExpiresAtUtc < now` sang `Expired` (để constraint không bị chặn bởi hold đã chết), rồi kiểm tra chồng lấn bằng query.
2. **Exclusion constraint ở PostgreSQL** (migration SQL thuần, xem Phase 1). Khi insert/update vi phạm, Npgsql ném `PostgresException` với `SqlState == "23P01"`; bắt qua `DbUpdateException.InnerException` và đổi thành `SLOT_UNAVAILABLE` (409).

Khóa dòng khi đổi trạng thái: trong transaction, nạp booking bằng
`dbContext.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id = {id} FOR UPDATE")`.

### 4.6 Tiền và sổ cái

`Payment` = giao dịch với cổng. `MoneyMovement` = nghĩa vụ/biến động tiền. `MoneyMovement.Reference` là unique, dùng làm khóa idempotent.

| Sự kiện | Payment | MoneyMovement (Type, Status, Reference) |
| --- | --- | --- |
| Cọc thành công | cọc → `Succeeded`, `PaidAtUtc` | `DepositHeld`, `Held`, `booking:{id}:deposit-held` |
| Hoàn đủ cọc (từ chối/hết hạn/hủy miễn phí) | cọc → `Refunded`, `RefundedAtUtc` | `CustomerRefund`, `Posted`, `booking:{id}:refund`; movement `DepositHeld` → `Reversed` |
| Hủy muộn | cọc giữ `Succeeded` | `ProviderEarning` = providerShare, `Pending`, `booking:{id}:cancel-provider`; `PlatformFee` = platformShare, `Posted`, `booking:{id}:cancel-platform`; `DepositHeld` → `Posted` |

`ProviderEarning` để `Pending` vì luồng chi trả chưa chốt (`// TODO: POLICY`). Bỏ qua movement có số tiền bằng 0 (DB có check `amount_vnd > 0`).

## 5. Cấu hình

Tạo `Options/BookingOptions.cs` (`SectionName = "Booking"`), bind trong `Program.cs`, khai giá trị mặc định ngay trong class để không bắt buộc sửa `appsettings.json`:

| Thuộc tính | Mặc định | Ghi chú |
| --- | --- | --- |
| `SlotStepMinutes` | 30 | |
| `MinimumDurationMinutes` | 120 | Đã chốt. |
| `MaximumDurationMinutes` | 720 | TODO: POLICY |
| `MinimumLeadMinutes` | 60 | TODO: POLICY |
| `MaximumAdvanceDays` | 30 | TODO: POLICY |
| `DepositPercent` | 15 | Đã chốt. |
| `HoldMinutes` | 15 | TODO: POLICY |
| `ProviderConfirmationMinutes` | 30 | TODO: POLICY |
| `FreeCancellationMinutes` | 120 | Đã chốt. |
| `LateCancellationProviderPercent` | 10 | Đã chốt. |
| `MaximumActiveHoldsPerCustomer` | 3 | TODO: POLICY |
| `CheckInTokenSeconds` | 120 | TODO: POLICY |
| `CheckInOpensMinutesBeforeStart` | 30 | TODO: POLICY |
| `ExpiryJobIntervalSeconds` | 60 | |

Tạo `Options/PaymentOptions.cs` (`SectionName = "Payments"`) với `SandboxEnabled` (mặc định `false`). Trong `appsettings.Development.json` đặt `"Payments": { "SandboxEnabled": true }`, và thêm biến `Payments__SandboxEnabled: "true"` cho service `api` trong `docker-compose.yml`.

## 6. Hợp đồng API

Tất cả dưới `/api/v1`. Lỗi là `ProblemDetails` với `code`. Ngoài các mã ở 4.2 còn có: `BOOKING_NOT_FOUND` (404, dùng cả khi đơn không thuộc về người gọi), `BOOKING_INVALID_STATE` (409), `VALIDATION_FAILED` (400, có sẵn).

### 6.1 Địa chỉ khách — role `Customer`

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/customer/addresses` | Danh sách, mặc định trước, rồi mới nhất. |
| POST | `/customer/addresses` | Tạo. Địa chỉ đầu tiên tự là mặc định. Tối đa 10 địa chỉ → `ADDRESS_LIMIT_REACHED` (409). |
| PUT | `/customer/addresses/{id}` | Sửa. |
| DELETE | `/customer/addresses/{id}` | Xóa (204). Booking cũ giữ `AddressSnapshot`; FK đã là `SetNull`. Nếu xóa địa chỉ mặc định thì gán mặc định cho địa chỉ mới nhất còn lại. |

Request (`SaveCustomerAddressRequest`): `label` (≤80), `apartmentNumber` (≤80), `floor?` (≤20), `buildingName` (≤160), `streetAddress` (≤200), `ward` (≤120), `district` (≤120), `city` (≤120), `accessInstructions?` (≤500), `isDefault`. Khi `isDefault = true` thì bỏ cờ mặc định của các địa chỉ khác trong cùng transaction. Response `CustomerAddressResponse`: các trường trên + `id`.

### 6.2 Lịch rảnh

Freelancer — role `Freelancer`, provider phải `Approved` (nếu không → `PROVIDER_NOT_APPROVED` 409):

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/freelancer/availability?from=YYYY-MM-DD&days=14` | Các khoảng rảnh của chính mình. |
| POST | `/freelancer/availability` | Body `{ startsAtUtc, endsAtUtc }`. Đúng bước 30 phút, dài ≥ 120 phút, bắt đầu ở tương lai, không chồng khoảng rảnh khác của mình (`AVAILABILITY_OVERLAP` 409). |
| DELETE | `/freelancer/availability/{id}` | Chỉ xóa được khi không có booking chiếm slot chồng lên khoảng đó (`AVAILABILITY_IN_USE` 409). |

Công khai — `[AllowAnonymous]`:

`GET /freelancers/{providerId}/availability?from=YYYY-MM-DD&days=7` (`days` 1..14, `from` mặc định là hôm nay theo giờ VN). Chỉ cho provider đã `Approved`, ngược lại 404 `FREELANCER_NOT_FOUND`. Response:

```json
{
  "providerId": "…",
  "timeZone": "Asia/Ho_Chi_Minh",
  "slotStepMinutes": 30,
  "minimumDurationMinutes": 120,
  "days": [
    {
      "date": "2026-10-20",
      "free": [{ "startsAtUtc": "…", "endsAtUtc": "…" }],
      "busy": [{ "startsAtUtc": "…", "endsAtUtc": "…" }]
    }
  ]
}
```

`free` = khoảng rảnh liên tục đã trừ đi các khoảng bận và phần đã qua (`< now + MinimumLeadMinutes`), cắt theo ranh giới ngày giờ VN, bỏ mảnh ngắn hơn 120 phút. `busy` = booking chiếm slot, **không** kèm bất kỳ thông tin khách/đơn nào.

### 6.3 Checklist — công khai

`GET /catalog/service-categories/{code}/checklist` → template `IsActive` có `Version` cao nhất của danh mục: `{ templateId, name, version, items: [{ id, parentItemId, areaName, description, sortOrder, isRequired }] }`. Không có template → 404 `CHECKLIST_NOT_FOUND`.

### 6.4 Booking của khách — role `Customer`

| Method | Route | Mô tả |
| --- | --- | --- |
| POST | `/bookings/quote` | Báo giá, không ghi DB. Chạy đủ kiểm tra 4.2 (trừ mục 9). |
| POST | `/bookings` | Tạo booking `PendingDeposit` và giữ slot. Trả 201 + `BookingDetailResponse`. |
| GET | `/bookings?status=&page=&pageSize=` | Đơn của tôi, `StartsAtUtc` giảm dần. `status` lọc tùy chọn. |
| GET | `/bookings/{id}` | Chi tiết. |
| POST | `/bookings/{id}/deposit` | Khởi tạo thanh toán cọc. |
| GET | `/bookings/{id}/cancellation-preview` | Số tiền hoàn/giữ nếu hủy ngay bây giờ. |
| POST | `/bookings/{id}/cancel` | Hủy. Body `{ reason? }` (≤500). |
| POST | `/bookings/{id}/check-in-token` | Phát hành QR check-in. |

`CreateBookingRequest` (dùng chung cho quote): `providerId`, `serviceCategoryId`, `customerAddressId`, `startsAtUtc`, `endsAtUtc`, `checklistItemIds[]` (≤100), `customerNote?` (≤1000).

`BookingQuoteResponse`: `unitPriceVnd`, `durationMinutes`, `baseAmountVnd`, `surchargeAmountVnd`, `depositAmountVnd`, `depositPercent`, `remainingAmountVnd` (= base + surcharge − deposit), `holdMinutes`.

Tạo booking:

- `BookingNumber` = `"AB" + yyMMdd (giờ VN) + 6 ký tự ngẫu nhiên` từ bảng chữ `ABCDEFGHJKMNPQRSTUVWXYZ23456789`, sinh bằng `RandomNumberGenerator`. Trùng unique index thì sinh lại, tối đa 3 lần.
- `AddressSnapshot` = ghép các phần không rỗng theo thứ tự `ApartmentNumber, Floor, BuildingName, StreetAddress, Ward, District, City`, ngăn bởi `", "`; nếu có `AccessInstructions` thì nối thêm `" (Ghi chú: …)"`.
- `BookingTask`: lấy các item bắt buộc (`IsRequired`) của template hiện hành **cộng** các item trong `checklistItemIds`. Item không thuộc template hiện hành của danh mục → `INVALID_CHECKLIST_ITEM` (400). Snapshot `AreaName`, `Description`, `SortOrder`, `TemplateItemId`. Danh mục chưa có template thì cho phép danh sách task rỗng.
- `HoldExpiresAtUtc = now + HoldMinutes`.
- Ghi `BookingStatusHistory` đầu tiên với `FromStatus = ToStatus = PendingDeposit`, `Reason = "Created"`.

`BookingSummaryResponse`: `id`, `bookingNumber`, `status`, `providerId`, `providerDisplayName`, `serviceCategoryCode`, `serviceCategoryName`, `startsAtUtc`, `endsAtUtc`, `baseAmountVnd`, `depositAmountVnd`, `holdExpiresAtUtc`.

`BookingDetailResponse`: các trường summary + `addressSnapshot`, `customerNote`, `unitPriceVnd`, `surchargeAmountVnd`, `extensionAmountVnd`, `remainingAmountVnd`, `providerConfirmationDeadlineUtc`, `cancelledAtUtc`, `cancellationReason`, `depositPaymentStatus` (`PaymentStatus?` của payment cọc mới nhất), `tasks[] { id, areaName, description, sortOrder, status }`, `timeline[] { fromStatus, toStatus, reason, atUtc }` (tăng dần theo thời gian), `allowedActions[]` (tập con của `PayDeposit`, `Cancel`, `ShowCheckInQr`, do server tính từ trạng thái và thời gian).

### 6.5 Thanh toán cọc (sandbox)

`POST /bookings/{id}/deposit`, header bắt buộc `Idempotency-Key` (1..100 ký tự, thiếu → `IDEMPOTENCY_KEY_REQUIRED` 400), body `{ method }` (`PaymentMethod`).

- Điều kiện: booking của tôi, `PendingDeposit`, hold chưa hết hạn (`HOLD_EXPIRED` 409 nếu đã hết).
- `Payment.IdempotencyKey = "{userId:N}:{key}"`. Gọi lại cùng key → trả lại payment cũ. Nếu booking đã có payment cọc `Pending` với key khác → trả payment đang có, không tạo thêm.
- Tạo `Payment { Purpose = Deposit, Status = Pending, AmountVnd = DepositAmountVnd, ProviderName, ProviderTransactionId }` qua `IPaymentGateway`.
- Response `DepositPaymentResponse`: `paymentId`, `status`, `amountVnd`, `method`, `expiresAtUtc` (= hold), `checkoutUrl?` (sandbox trả `null`).

Thiết kế tích hợp, đặt ở `Integrations/Payments/`:

```csharp
public sealed record PaymentInitiation(string ProviderName, string ProviderTransactionId, string? CheckoutUrl);
public interface IPaymentGateway
{
    Task<PaymentInitiation> CreateDepositAsync(Guid paymentId, long amountVnd, PaymentMethod method, CancellationToken cancellationToken);
    Task RefundAsync(string providerTransactionId, long amountVnd, CancellationToken cancellationToken);
}
```

`SandboxPaymentGateway`: `ProviderName = "sandbox"`, `ProviderTransactionId = Guid.NewGuid().ToString("N")`, `RefundAsync` không làm gì. Đăng ký `AddSingleton<IPaymentGateway, SandboxPaymentGateway>()`.

`POST /payments/sandbox/{paymentId}/confirm`, role `Customer`, body `{ succeeded: bool }`. Khi `Payments:SandboxEnabled = false` trả 404. Chỉ người trả (`PayerUserId`) được gọi. Endpoint này chỉ gọi `IBookingPaymentService.HandleGatewayResultAsync(providerName, providerTransactionId, succeeded)` — đúng hàm mà webhook PSP thật sẽ gọi sau này.

`HandleGatewayResultAsync` phải idempotent:

1. Payment đã ở trạng thái kết thúc (`Succeeded`, `Failed`, `Cancelled`, `Refunded`) → trả về trạng thái hiện tại, không đổi gì.
2. `succeeded = false` → payment `Failed`. Booking giữ `PendingDeposit` để khách thử lại trong thời gian hold.
3. `succeeded = true`, trong một transaction có khóa booking:
   - Booking còn `PendingDeposit` và hold chưa hết: payment `Succeeded`; ghi `DepositHeld`; nếu `FreelancerProfile.AutoAcceptBookings` → `Confirmed`, ngược lại → `PendingProviderConfirmation` với `ProviderConfirmationDeadlineUtc = min(now + ProviderConfirmationMinutes, StartsAtUtc)`; xóa `HoldExpiresAtUtc`; tạo `Notification` cho user của freelancer (`Type = "booking.deposit_paid"`).
   - Booking đã `Expired`/`Cancelled` (tiền về muộn): payment `Succeeded` rồi hoàn ngay theo 4.6. Không hồi sinh booking.

Gọi `IPaymentGateway` **ngoài** transaction DB.

### 6.6 Booking phía freelancer — role `Freelancer`

| Method | Route | Mô tả |
| --- | --- | --- |
| GET | `/freelancer/bookings?status=&page=&pageSize=` | Đơn gán cho provider của tôi. Không trả đơn `PendingDeposit`/`Expired` chưa từng có cọc. |
| GET | `/freelancer/bookings/{id}` | Chi tiết. |
| POST | `/freelancer/bookings/{id}/confirm` | `PendingProviderConfirmation` → `Confirmed`. Quá deadline → `CONFIRMATION_EXPIRED` (409). |
| POST | `/freelancer/bookings/{id}/reject` | Body `{ reason? }`. → `Rejected`, hoàn đủ cọc. |

Quyền riêng tư: response cho freelancer chỉ có `addressSnapshot` đầy đủ khi trạng thái là `Confirmed` trở đi; trước đó trả `addressSnapshot = null` và `areaLabel = "{District}, {City}"`. Không bao giờ trả số điện thoại khách trong phase này. Tên khách lấy từ `CustomerProfile.FullName`.

Mỗi lần xác nhận/từ chối/hết hạn/hủy tạo `Notification` cho bên còn lại (`booking.confirmed`, `booking.rejected`, `booking.expired`, `booking.cancelled`).

### 6.7 QR check-in

`POST /bookings/{id}/check-in-token`:

- Điều kiện: booking của tôi, `Confirmed`, `now` trong `[StartsAtUtc − CheckInOpensMinutesBeforeStart, EndsAtUtc]`; ngoài khung → `CHECK_IN_NOT_OPEN` (409).
- Sinh 32 byte ngẫu nhiên (`RandomNumberGenerator`), mã hóa base64url làm token thô. Lưu `TokenHash` = SHA-256 dạng hex của token thô. **Không lưu, không log token thô.**
- Vô hiệu token cũ chưa dùng của booking (đặt `ExpiresAtUtc = now`), rồi tạo token mới hết hạn sau `CheckInTokenSeconds`.
- Response: `{ token, expiresAtUtc }`. FE tự vẽ QR từ `token`.

## 7. Các phase

Sau mỗi phase chạy: `dotnet build Abstrict.sln` và `dotnet test Abstrict.sln`.

### Phase 0 — Baseline

1. Đọc `PROJECT_IDEA.md`, `BACKEND_ARCHITECTURE.md`, `docs/DATABASE_ENTITY_MAPPING.md`, và các file mẫu ở mục 3.
2. Chạy build + test, ghi lại kết quả ban đầu. Nếu baseline đỏ thì dừng và báo lại, không tự sửa.

### Phase 1 — Nền tảng

1. `Options/BookingOptions.cs`, `Options/PaymentOptions.cs`, bind trong `Program.cs`.
2. `BookingFlowException(statusCode, code, message)` và `Controllers/BookingControllerBase.cs` (sao cấu trúc `KycControllerBase`: `CurrentUserId`, `Run<T>`, `ToProblem`). Không sửa code KYC.
3. Policy thuần trong `Services/Implementations/`: `BookingPricing`, `BookingSlotPolicy` (kiểm tra bước/thời lượng/lead + gộp khoảng rảnh + trừ khoảng bận), `BookingCancellationPolicy`, `BookingStateMachine` (bảng chuyển trạng thái hợp lệ trong 4.1).
4. Rate limit policy `booking-write` trong `Program.cs`: theo user id, 30 request/10 phút (sao từ `kyc-write`). Gắn vào mọi endpoint ghi của khách.
5. Migration `AddBookingOverlapGuard` (SQL thuần trong `Up`, có `Down` tương ứng):

   ```sql
   CREATE EXTENSION IF NOT EXISTS btree_gist;
   ALTER TABLE bookings ADD CONSTRAINT ex_bookings_provider_slot
     EXCLUDE USING gist (provider_id WITH =, tstzrange(starts_at_utc, ends_at_utc, '[)') WITH &&)
     WHERE (status IN ('PendingDeposit','PendingProviderConfirmation','Confirmed','CheckedIn','InProgress','PendingAcceptance'));
   ```

6. Migration `SeedChecklistTemplates`: theo đúng cách làm của `20260929090000_SeedCatalogData.cs` (`migrationBuilder.InsertData` với GUID cố định, `Down` dùng `DeleteData`). Tạo một template version 1 cho `CLEANING`, `DEEP_CLEANING`, `COOKING`, `LAUNDRY` (ID danh mục cố định `aaaaaaaa-0000-0000-0000-000000000001..4`, xem migration đó), mỗi template 5–8 item tiếng Việt, 2–3 item `is_required = true`.
7. Unit test (`tests/Abstrict.Api.Tests/BookingPolicyTests.cs`), tối thiểu:
   - Giá: 80.000đ/giờ × 150 phút = 200.000; cọc 30.000. Cọc làm tròn lên khi lẻ.
   - Thời lượng 119 phút bị từ chối, 120 phút được nhận; lệch bước 30 phút bị từ chối.
   - Hai khoảng rảnh chạm nhau được gộp; khoảng đặt vắt qua khe hở bị từ chối.
   - Trừ khoảng bận ra đúng các mảnh rảnh, bỏ mảnh < 120 phút.
   - Hủy đúng mốc 120 phút → hoàn đủ; 119 phút → provider 10% giá gốc, nền tảng phần còn lại, tổng bằng đúng tiền cọc.
   - Bảng trạng thái: `Confirmed → PendingDeposit` bị từ chối; `PendingDeposit → Expired` hợp lệ.

### Phase 2 — Địa chỉ khách

`ICustomerAddressService` + `CustomerAddressService`, `CustomerAddressesController`, DTO theo 6.1. Test: validation của request (DataAnnotations).

### Phase 3 — Lịch rảnh và checklist

`IAvailabilityService` + implementation; `FreelancerAvailabilityController`; thêm action `GetAvailability` vào `FreelancersController`; thêm action checklist vào `CatalogController` (mở rộng `ICatalogService`). Theo 6.2 và 6.3.

### Phase 4 — Báo giá và tạo booking

`IBookingService` + `BookingService` (quote, create), `BookingsController`. Theo 4.2, 4.3, 4.5, 6.4. Bắt `23P01` → `SLOT_UNAVAILABLE`.

### Phase 5 — Cọc sandbox

`IPaymentGateway`, `SandboxPaymentGateway`, `IBookingPaymentService` + implementation, action `deposit` trong `BookingsController`, `SandboxPaymentsController`. Theo 4.6 và 6.5. Viết helper hoàn cọc dùng chung cho Phase 6 và 7.

### Phase 6 — Freelancer xác nhận và job hết hạn

1. `IFreelancerBookingService` + implementation, `FreelancerBookingsController`. Theo 6.6.
2. `BackgroundJobs/BookingExpiryJob.cs` kế thừa `BackgroundService`, đăng ký `AddHostedService`. Mỗi `ExpiryJobIntervalSeconds`, tạo scope mới và xử lý từng lô ≤ 100:
   - `PendingDeposit` có `HoldExpiresAtUtc < now` → `Expired`; hủy payment `Pending`.
   - `PendingProviderConfirmation` có `ProviderConfirmationDeadlineUtc < now` → `Expired`; hoàn đủ cọc.
   Mỗi booking một transaction riêng có khóa dòng và kiểm tra lại trạng thái sau khi khóa. Lỗi một booking không được làm dừng vòng lặp (bắt, log bằng `ILogger`, đi tiếp). Chạy lặp lại phải an toàn.

### Phase 7 — Danh sách, chi tiết, hủy

Hoàn thiện `GET /bookings`, `GET /bookings/{id}`, `cancellation-preview`, `cancel` theo 4.4, 4.6, 6.4. `allowedActions` tính ở server.

### Phase 8 — QR check-in

Theo 6.7. Unit test: hash ổn định với cùng token, token thô không xuất hiện trong `TokenHash`.

### Phase 9 — Tài liệu và dữ liệu thử

1. `docs/BOOKING_API.md`: theo văn phong `docs/FREELANCER_PROFILE_API.md` (bảng endpoint, tham số, ví dụ JSON, bảng mã lỗi).
2. Thêm các request mẫu vào `src/Abstrict.Api/Abstrict.Api.http`.
3. `scripts/seed-booking-fixtures.ps1` (dev, idempotent, theo kiểu `scripts/seed-fake-freelancers.ps1`): cho 5 freelancer `Approved` (provider `dddddddd-0000-0000-0000-000000000001..5`) khoảng rảnh 08:00–12:00 và 13:00–18:00 giờ VN trong 14 ngày tới.
4. Cập nhật `Description` của Swagger trong `Program.cs` để nhắc tới booking.

## 8. Kiểm chứng cuối

Chạy stack: `docker compose --env-file .env.docker up -d --build`, rồi `scripts/seed-fake-freelancers.ps1` và `scripts/seed-booking-fixtures.ps1`. Tài khoản freelancer thử: số `0910000001`, mật khẩu `Freelancer-dev-2026`. Tự đăng ký một khách qua `/auth/register` (môi trường Development có `Otp:ExposeCodeToClient = true` nên response trả kèm mã OTP).

Kịch bản phải chạy đúng bằng HTTP thật, ghi lại status code từng bước:

1. **Đường chính:** tạo địa chỉ với `city = "Hồ Chí Minh"`, `district = "Quận 1"` (khớp chính xác khu vực `bbbbbbbb-…-000000000001` của freelancer 1; lấy chuỗi từ `GET /catalog/service-areas`) → xem availability → quote → tạo booking (201, `PendingDeposit`) → deposit → sandbox confirm → `PendingProviderConfirmation` → freelancer confirm → `Confirmed`.
2. **Đặt trùng:** tạo booking thứ hai chồng giờ với đơn trên → 409 `SLOT_UNAVAILABLE`. Bắn 2 request tạo trùng giờ song song → đúng một cái 201, cái còn lại 409.
3. **Hết hạn giữ slot:** tạo booking, không trả cọc, đặt `Booking__HoldMinutes=1`, chờ job → `Expired`, slot đặt lại được.
4. **Từ chối:** freelancer reject → `Rejected`, payment `Refunded`, có movement `CustomerRefund`.
5. **Hủy miễn phí / hủy muộn:** preview khớp với kết quả hủy thật; hủy muộn sinh đúng 2 movement và tổng bằng tiền cọc.
6. **Idempotent:** gọi `deposit` hai lần cùng `Idempotency-Key` → cùng `paymentId`; gọi sandbox confirm hai lần → chỉ một movement `DepositHeld`.
7. **Quyền:** khách B đọc/hủy đơn của khách A → 404 `BOOKING_NOT_FOUND`; token freelancer gọi `/bookings` → 403.
8. **QR:** ngoài khung giờ → 409 `CHECK_IN_NOT_OPEN`; trong khung trả token, gọi lần hai làm token đầu hết hạn.

## 9. Định nghĩa hoàn thành

- `dotnet build` không warning mới, `dotnet test` xanh, có các unit test nêu ở Phase 1 và 8.
- `dotnet ef migrations has-pending-model-changes` báo không có thay đổi; migration chạy được trên DB đang có dữ liệu.
- 8 kịch bản ở mục 8 chạy đúng.
- Không có entity nào lộ ra response; endpoint công khai không lộ địa chỉ, số điện thoại, thông tin đơn.
- Mọi giá trị chính sách nằm trong `BookingOptions`, có `// TODO: POLICY` ở chỗ chưa chốt.
- Không thêm package NuGet mới. Không sửa migration cũ, code KYC/Auth, hay các thay đổi chưa commit có sẵn.

## 10. Khi gặp điều chưa rõ

- Nghiệp vụ không có trong tài liệu này hoặc `PROJECT_IDEA.md`: chọn phương án đơn giản nhất, đưa giá trị vào options, ghi `// TODO: POLICY` và liệt kê trong báo cáo cuối.
- Cần đổi schema ngoài hai migration ở Phase 1: dừng lại và hỏi.
- Báo cáo cuối gồm: danh sách file thêm/sửa theo phase, kết quả build/test, kết quả 8 kịch bản, các giả định đã tự đặt, những gì chưa làm được.
