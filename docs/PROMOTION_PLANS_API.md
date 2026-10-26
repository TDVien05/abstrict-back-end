# API gói tăng hiển thị (admin cấu hình, đối tác xem)

Admin tạo và quản lý các gói tăng hiển thị; freelancer và doanh nghiệp xem các gói đang bán cho đúng đối tượng của mình. Giá, thời hạn và quyền lợi hoàn toàn do admin cấu hình, không hard-code. Đối tác mua gói qua cổng thanh toán sandbox (xem mục "Mua gói"); việc áp dụng ưu tiên hiển thị vào danh bạ/tìm kiếm **chưa** được xây dựng.

Dữ liệu dùng bảng `promotion_plans` có sẵn, không cần migration. Base path `/api/v1`.

## Endpoint

| Method | Endpoint | Quyền | Ghi chú |
| --- | --- | --- | --- |
| GET | `/admin/promotion-plans` | Admin | Query `audience` (`Freelancer`, `Company`), `isActive`. Sắp xếp theo đối tượng, thời hạn, giá. |
| GET | `/admin/promotion-plans/{planId}` | Admin | 404 `PLAN_NOT_FOUND`. |
| POST | `/admin/promotion-plans` | Admin | 201. Lỗi: 400 validation, 409 `PLAN_CODE_TAKEN`. |
| PUT | `/admin/promotion-plans/{planId}` | Admin | Sửa tên, thời hạn, giá, bán kính, giới hạn người làm, quyền lợi, `isActive`. |
| DELETE | `/admin/promotion-plans/{planId}` | Admin | 204. 409 `PLAN_IN_USE` nếu đã có lượt mua: hãy tắt gói (`isActive=false`). |
| GET | `/partner/promotion-plans` | Freelancer, Company | Chỉ gói `isActive` của đúng đối tượng theo vai trò người gọi; rẻ và ngắn hạn lên trước. Không trả `purchaseCount`. |

## Request tạo gói

```json
{
  "code": "FREELANCER_FEATURED_7D",
  "audience": "Freelancer",
  "name": "Gói Nổi bật 7 ngày",
  "durationDays": 7,
  "priceVnd": 49000,
  "priorityRadiusKm": 10,
  "workerLimit": null,
  "benefits": ["Gắn nhãn “Được tài trợ”", "Ưu tiên hiển thị trong khu vực nhận ca"],
  "isActive": true
}
```

Sửa gói dùng cùng body nhưng không có `code` và `audience` (không đổi được sau khi tạo).

## Quy tắc dữ liệu

| Trường | Quy tắc | Mã lỗi (400) |
| --- | --- | --- |
| `code` | Cắt khoảng trắng, đổi sang chữ hoa; 3–40 ký tự, chỉ `A-Z`, `0-9`, `_`, không bắt đầu bằng `_`; duy nhất | `INVALID_PLAN_CODE` (trùng: 409 `PLAN_CODE_TAKEN`) |
| `name` | 2–120 ký tự, gộp khoảng trắng | `INVALID_PLAN_NAME` |
| `durationDays` | 1–365 | `INVALID_PLAN_DURATION` |
| `priceVnd` | 0–100.000.000 (`TODO: POLICY`, giới hạn trên để chặn nhập nhầm) | `INVALID_PLAN_PRICE` |
| `priorityRadiusKm` | Tùy chọn, 1–100 | `INVALID_PRIORITY_RADIUS` |
| `workerLimit` | Tùy chọn, 1–1000, chỉ cho gói `Company` | `INVALID_WORKER_LIMIT`, `WORKER_LIMIT_NOT_ALLOWED` |
| `benefits` | Tối đa 10 dòng, mỗi dòng ≤ 200 ký tự; dòng trống bị bỏ; lưu dạng mảng JSON ở `benefits_json` | `INVALID_PLAN_BENEFITS` |

Thiếu `audience`, `durationDays` hoặc `priceVnd` trả 400. Dữ liệu `benefits_json` hỏng (sửa tay trong DB) được đọc thành danh sách rỗng, không làm lỗi API.


## Mua gói

Đối tác đã được duyệt (`ApprovalStatus = Approved`) mua gói qua `/partner/promotion-purchases`. Freelancer được nhận diện qua `FreelancerProfile.UserId`, doanh nghiệp qua `CompanyProfile.OwnerUserId`. Thanh toán hiện chỉ có cổng sandbox (`Payments:SandboxEnabled`).

| Method | Endpoint | Quyền | Ghi chú |
| --- | --- | --- | --- |
| GET | `/partner/promotion-purchases` | Freelancer, Company | Lịch sử mua, mới nhất trước. |
| POST | `/partner/promotion-purchases` | Freelancer, Company | Body `{ "planId": "...", "method": "Momo" }`, bắt buộc header `Idempotency-Key`. Rate limit `promotion-write` (20 lần/10 phút/người dùng). |
| POST | `/partner/promotion-purchases/{purchaseId}/sandbox-confirm` | Freelancer, Company | Body `{ "succeeded": true }`; 404 `PURCHASE_NOT_FOUND` khi sandbox tắt hoặc lượt mua không thuộc về người gọi. |

Quy tắc:

- Gói giá 0: kích hoạt ngay, không tạo `Payment` (bảng `payments` yêu cầu số tiền > 0). Vì không có giao dịch nên `Idempotency-Key` không chặn được mua lặp; FE vô hiệu hóa nút khi đang gửi.
- Gói có phí: tạo `Payment` (`Purpose = Promotion`, trạng thái `Pending`) và lượt mua `PendingPayment`. Gọi lại cùng `Idempotency-Key`, hoặc mua lại cùng gói khi đang có lượt chờ thanh toán, trả về lượt cũ. Dùng lại khóa cho gói khác trả 409 `IDEMPOTENCY_KEY_REUSED`.
- Chốt thành công: `Payment.Succeeded`, lượt mua `Active`, ghi `MoneyMovement` loại `PromotionCharge` (`reference = promotion:{purchaseId}:charge`). Chốt thất bại: `PaymentFailed` (mua lại được bằng lượt mới). Gọi chốt lần hai sau khi đã chốt không đổi gì.
- Thời hạn: bắt đầu ngay khi kích hoạt, hoặc **nối tiếp** ngay sau gói đang/sắp hiệu lực của cùng đối tác (không mất ngày đã mua). Thời điểm bắt đầu tính lúc chốt thanh toán, không phải lúc bấm mua.
- Giá lấy từ gói tại lúc mua và lưu vào `PriceSnapshotVnd`; sửa giá gói sau đó không ảnh hưởng lượt đã mua.
- Trạng thái trả về là trạng thái hiệu lực: lượt `Active` đã quá `EndsAtUtc` được báo `Expired` dù DB chưa đổi (chưa có job cập nhật). `isCurrent` chỉ đúng với lượt đang chạy ngay lúc này.
- Lỗi: 400 `IDEMPOTENCY_KEY_REQUIRED`; 404 `PLAN_NOT_FOUND`; 409 `PLAN_NOT_AVAILABLE` (gói tắt hoặc sai đối tượng), `PROVIDER_NOT_APPROVED`, `IDEMPOTENCY_KEY_REUSED`.

Chưa có: tự áp dụng ưu tiên hiển thị vào danh bạ/tìm kiếm theo lượt `Active`, enforcement `workerLimit`, job chuyển `Active` → `Expired`, hủy lượt chờ thanh toán quá hạn, cổng thanh toán thật.

## Báo cáo cho admin

`GET /admin/promotion-plans/report` (chỉ Admin) trả số liệu cho khối "Báo cáo gói hiển thị" ở trang tổng quan admin:

- `activePlanCount`, `inactivePlanCount`: số gói đang bán và đã tắt.
- `paidPurchases`: số lượt mua đã được kích hoạt (lượt `Active` trong DB, kể cả đã hết hạn, gồm gói miễn phí). `activeNow`: số lượt đang chạy ngay lúc này. `pendingPayment`, `paymentFailed`: số lượt chờ thanh toán và thanh toán thất bại.
- `totalRevenueVnd`, `last7DaysRevenueVnd`: tổng giá đã chốt (`PriceSnapshotVnd`) của các lượt đã kích hoạt.
- `daily`: đúng 7 ngày gần nhất theo giờ Việt Nam (UTC+7), ngày không có giao dịch là 0; mỗi dòng gồm `date` (`yyyy-MM-dd`), `purchases`, `revenueVnd`. Ngày tính theo `Payment.PaidAtUtc`, hoặc `CreatedAtUtc` với gói miễn phí.
- `audiences`: lượt mua và doanh thu theo đối tượng (Freelancer, Company).
- `plans`: từng gói (cả gói đã tắt) kèm `purchases`, `activeNow`, `revenueVnd`; sắp theo doanh thu giảm dần.

Chưa có hoàn tiền cho lượt mua gói, nên doanh thu chưa trừ khoản hoàn nào.

## Lưu ý nghiệp vụ

- Đối tượng của người xem `/partner/promotion-plans` suy ra từ vai trò tài khoản (Freelancer → gói Freelancer, Company → gói Company), không nhận tham số từ client. Customer nhận 403.
- Sửa giá không ảnh hưởng lượt mua cũ: `PromotionPurchase` lưu `PriceSnapshotVnd` lúc mua.
- Gói đã có lượt mua không xóa được; việc kiểm tra có cả nhánh bắt lỗi khóa ngoại khi có lượt mua phát sinh giữa lúc kiểm tra và lúc xóa.
- Audit log: `promotion_plan.created`, `promotion_plan.updated`, `promotion_plan.deleted` (entity `PromotionPlan`, có `before/after`).

## Kiểm thử

- Unit test chính sách: `tests/Abstrict.Api.Tests/PromotionPlanTests.cs`.
- Kiểm chứng trên backend thật: `frontend/e2e/promotion-plans-api.real.spec.ts` (tạo, mã trùng, đối tượng, ẩn gói tắt/Company, phân quyền, xóa).
- Mua gói: unit test `PromotionPurchaseTests.cs`; API thật `frontend/e2e/promotion-purchases-api.real.spec.ts`; giao diện thật `frontend/e2e/promotion-plans-ui.real.spec.ts`.
- Gói Company chưa được xem bằng tài khoản doanh nghiệp thật vì chưa có mật khẩu dev đã biết; logic chọn đối tượng được phủ bằng unit test `AudienceOf`.
