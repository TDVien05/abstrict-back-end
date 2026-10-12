# Yêu cầu backend để hoàn thiện flow booking

Tài liệu này liệt kê những việc frontend cần backend làm sau khi đã gắn xong các endpoint trong [BOOKING_API.md](BOOKING_API.md). Trạng thái kiểm tra: đối chiếu với Swagger và gọi thật backend local (`localhost:8080`) ngày 12/10/2026.

Hiện frontend đã chạy được đoạn: chọn lịch rảnh → báo giá → tạo đơn → đặt cọc giả lập → freelancer xác nhận/từ chối → khách xem/hủy đơn → khách hiện mã QR. Flow dừng ở đó.

## Tóm tắt

| # | Việc | Mức độ |
| --- | --- | --- |
| 1 | Khôi phục phiên đăng nhập sau khi tải lại trang | Chặn |
| 2 | Thống nhất endpoint đăng nhập/đăng ký freelancer | Chặn |
| 3 | Domain API production đang không có ứng dụng | Chặn (bản deploy) |
| 4 | Trả thông tin giao dịch cọc đang chờ trong chi tiết đơn | Cần |
| 5 | Thêm `id` danh mục vào `services[]` của freelancer | Cần |
| 6 | `allowedActions` cho đơn phía freelancer | Cần |
| 7 | Xác nhận cách tính giới hạn request cho `/bookings/quote` | Cần trả lời |
| 8 | Danh sách giá trị `role` và `timeline[].reason` | Cần trả lời |
| 9 | Tài khoản khách hàng thử trong script seed | Cần |
| 10 | Các bước sau `Confirmed`: quét QR, làm việc, nghiệm thu, thanh toán phần còn lại | Giai đoạn sau |
| 11 | Cổng thanh toán thật | Giai đoạn sau |
| 12 | Cải thiện nhỏ | Tùy chọn |

---

## Việc chặn flow

### 1. Khôi phục phiên đăng nhập

**Hiện trạng.** `POST /auth/login` trả `accessToken` trong body và frontend chỉ giữ token trong bộ nhớ. Swagger không có endpoint nào để lấy lại phiên. Tải lại trang là mất đăng nhập.

**Ảnh hưởng tới booking.** Khách đang giữ chỗ 15 phút mà F5 thì phải đăng nhập lại. Khi có cổng thanh toán thật, trình duyệt bị chuyển sang trang cổng rồi quay về, tức luôn tải lại trang, nên khách sẽ quay về trong trạng thái chưa đăng nhập.

**Cần.** Một trong hai:

- `POST /auth/refresh` dùng cookie `HttpOnly` (frontend đã gửi `credentials: 'include'`), trả cùng cấu trúc với `/auth/login`; hoặc
- `GET /auth/me` trả `userId`, `fullName`, `phoneNumber`, `role`, kèm cơ chế giữ token qua lần tải lại.

Frontend ưu tiên phương án refresh bằng cookie.

### 2. Endpoint xác thực của freelancer

**Hiện trạng.** Frontend đang gọi bốn đường dẫn dưới `/auth/freelancers/`: `register`, `verify-phone-otp`, `resend-phone-otp`, `login`. Swagger chỉ có:

- `/auth/login`
- `/auth/register`
- `/auth/register-freelancer`
- `/auth/verify-phone-otp`
- `/auth/resend-phone-otp`

`POST /auth/freelancers/login` trả 404. Đăng nhập freelancer qua `/auth/login` thì thành công và trả `role: "Freelancer"`.

**Đã xử lý tạm ở frontend.** Trang `/login` đọc `role` và đưa freelancer vào khu đối tác. Trang `/partners/login` vẫn hỏng.

**Cần backend chốt.**

- Đường dẫn chính thức cho đăng ký, xác thực OTP và gửi lại OTP của freelancer. Frontend sẽ đổi theo.
- Trang đăng nhập đối tác cần biết freelancer đang ở bước KYC nào để điều hướng. Thêm `currentStep` (và trạng thái duyệt hồ sơ) vào response `/auth/login` khi `role = Freelancer`, hoặc cung cấp một endpoint riêng trả tiến độ onboarding.

### 3. Domain API production

`https://abstrict.api.vientruong.site` trả 404 `Application not found` từ Railway cho mọi request. `vercel.json` của frontend chuyển tiếp `/api/*` tới domain này, nên bản deploy trên Vercel không gọi được API nào. Cần chạy lại service hoặc gắn lại custom domain.

---

## Việc cần cho flow hiện tại

### 4. Giao dịch cọc đang chờ trong chi tiết đơn

**Hiện trạng.** `POST /bookings/{id}/deposit` trả `paymentId`, nhưng `GET /bookings/{id}` chỉ có `depositPaymentStatus`. Nếu khách tải lại trang sau khi khởi tạo thanh toán, frontend không còn `paymentId` và `Idempotency-Key` cũ, nên không thể tiếp tục giao dịch đó.

**Cần.** Thêm vào chi tiết đơn một object cho giao dịch cọc gần nhất:

```json
"depositPayment": {
  "paymentId": "e3bc5193-8ddd-4eda-8152-5e5a24e305d5",
  "status": "Pending",
  "method": "Momo",
  "amountVnd": 30000,
  "expiresAtUtc": "2026-10-12T07:23:20+00:00",
  "checkoutUrl": null
}
```

**Cần trả lời.** Khi đơn đã có một giao dịch `Pending` mà frontend gọi `/deposit` với `Idempotency-Key` mới thì server làm gì: trả lại giao dịch đang chờ, hủy cái cũ tạo cái mới, hay trả lỗi (mã nào)? Tài liệu chỉ ghi "mỗi đơn chỉ có một giao dịch cọc đang chờ".

### 5. `id` danh mục dịch vụ trong hồ sơ freelancer

`POST /bookings` cần `serviceCategoryId`, nhưng `GET /freelancers/{providerId}` chỉ trả `services[].code`. Frontend đang gọi thêm `GET /catalog/service-categories` để tra `code → id`.

**Cần.** Thêm `serviceCategoryId` vào mỗi phần tử `services[]`. Phương án khác: cho `POST /bookings` và `/bookings/quote` nhận `serviceCategoryCode`.

### 6. `allowedActions` phía freelancer

Chi tiết đơn của khách có `allowedActions`, còn `GET /freelancer/bookings` và `/freelancer/bookings/{id}` thì không. Frontend đang tự suy luận: hiện nút xác nhận/từ chối khi `status = PendingProviderConfirmation`, trái với khuyến nghị trong tài liệu API.

**Cần.** Thêm `allowedActions` (`Confirm`, `Reject`, sau này `ScanCheckIn`…) vào cả tóm tắt và chi tiết đơn phía freelancer. Nút nằm ngay trên thẻ đơn trong danh sách nên tóm tắt cũng cần.

### 7. Giới hạn request cho báo giá

Tài liệu ghi các endpoint ghi dùng chung giới hạn 30 request / 10 phút. `POST /bookings/quote` có tính vào giới hạn này không?

Frontend gọi lại báo giá mỗi khi khách đổi địa chỉ, và mỗi lần khách quay lại chọn giờ khác. Nếu có tính, khách thử nhiều khung giờ có thể bị 429 đúng lúc bấm đặt cọc. Đề nghị tách báo giá ra giới hạn riêng, vì nó không tạo dữ liệu.

### 8. Danh sách giá trị cần công bố

- **`role`** trong response đăng nhập. Mới thấy `Freelancer`; cần toàn bộ giá trị (khách hàng, công ty, admin) để frontend phân quyền đúng. Hiện mọi giá trị khác `Freelancer` đều bị coi là khách hàng.
- **`timeline[].reason`**. Mới thấy `Created`. Cần danh sách mã để hiển thị tiếng Việt; hiện frontend ẩn trường này.
- **`status` trong query** của `GET /bookings` và `GET /freelancer/bookings`: frontend gửi tên enum dạng chuỗi (`status=PendingProviderConfirmation`). Phía freelancer đã thử và chạy đúng; phía khách chưa thử được.

### 9. Tài khoản khách hàng thử

Script seed chỉ có tài khoản freelancer (`0910000001`). Cần thêm một khách hàng có sẵn mật khẩu và ít nhất một địa chỉ ở Quận 1, Hồ Chí Minh, để thử trọn flow mà không phải qua OTP. Các endpoint cần token khách hàng hiện chưa được thử với backend thật.

---

## Giai đoạn sau: từ `Confirmed` tới hoàn tất

Tài liệu API ghi rõ các phần này chưa hỗ trợ. Đây là những gì frontend cần để vẽ tiếp màn hình; tên endpoint chỉ là gợi ý.

### 10. Các bước trong và sau ca làm

| Bước | Frontend cần |
| --- | --- |
| Quét QR check-in | Endpoint cho freelancer gửi `token` đã quét (ví dụ `POST /freelancer/bookings/{id}/check-in`), chuyển đơn sang `CheckedIn`. Mã lỗi riêng cho mã hết hạn, mã sai đơn, ngoài khung giờ. |
| Bắt đầu và làm checklist | Endpoint cập nhật trạng thái từng `task` (`InProgress`, `Completed`, `ClarificationRequested`), và chuyển đơn sang `InProgress`. |
| Ảnh trước/sau | Endpoint tải ảnh gắn với đơn hoặc task, giới hạn dung lượng và định dạng, URL để hiển thị cho khách. |
| Hoàn thành ca | Endpoint cho freelancer báo xong, chuyển sang `PendingAcceptance`. |
| Nghiệm thu | Endpoint cho khách chấp nhận hoặc yêu cầu làm lại; hạn tự động nghiệm thu nếu khách không phản hồi. |
| Thanh toán phần còn lại | Cách thu `remainingAmountVnd`: thời điểm, phương thức, có dùng lại cơ chế `Idempotency-Key` như cọc không. |
| Gia hạn ca | `extensionAmountVnd` đã có trong chi tiết đơn nhưng chưa có endpoint yêu cầu và chấp nhận gia hạn. |
| Đánh giá | Endpoint khách gửi điểm và nhận xét sau `Completed`; trường cho biết đơn đã được đánh giá chưa. |
| Khiếu nại | Endpoint mở khiếu nại (`Disputed`) và xem kết quả xử lý. |

Với mỗi bước, đề nghị bổ sung giá trị tương ứng vào `allowedActions` của cả hai phía để frontend không phải suy luận từ `status`.

### 11. Cổng thanh toán thật

Frontend đã xử lý sẵn trường hợp `checkoutUrl` khác `null` (mở trang cổng). Cần backend:

- Nhận URL quay về từ frontend hoặc cố định là `/orders/{bookingId}`, kèm tham số cho biết kết quả.
- Cập nhật trạng thái qua webhook của cổng, để trang chi tiết đơn chỉ cần gọi lại `GET /bookings/{id}`.
- Tắt `/payments/sandbox/...` ở production (tài liệu đã ghi trả 404; frontend hiện báo lỗi chung khi gặp 404 này).

Phần này phụ thuộc mục 1: không khôi phục được phiên thì khách quay về từ cổng sẽ bị đẩy ra trang đăng nhập.

---

## Tùy chọn

### 12. Cải thiện nhỏ

- **Đồng hồ giữ chỗ.** Đếm ngược `holdExpiresAtUtc` và thời hạn mã QR dựa vào đồng hồ máy khách. Máy lệch giờ sẽ hiện sai. Thêm `serverTimeUtc` vào response chi tiết đơn và response mã QR là đủ để hiệu chỉnh.
- **Số đơn theo trạng thái phía freelancer.** Các tab trạng thái chỉ hiện được số lượng của tab đang mở. Một endpoint hoặc trường trả số đơn theo từng trạng thái sẽ cho phép hiện số "chờ xác nhận" ngay khi mở trang.
- **Cập nhật trạng thái.** Trang chi tiết đơn của khách đang gọi lại API mỗi 15 giây khi đơn ở `PendingDeposit` hoặc `PendingProviderConfirmation`; danh sách đơn của freelancer không tự làm mới. Nếu backend có kênh đẩy (SSE hoặc WebSocket) thì báo để frontend chuyển sang.
- **Thông tin hiển thị người dùng.** Thanh trên cùng muốn hiện ảnh đại diện và hạng thành viên; response đăng nhập chưa có `avatarUrl` và hạng thành viên.
