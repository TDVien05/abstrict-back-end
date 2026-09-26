# ABSTRICT — Project idea dùng chung cho Backend và Frontend

**Phiên bản:** 1.0 · 26/09/2026  
**Sản phẩm:** Nền tảng tìm kiếm, đặt lịch giúp việc theo giờ tại Việt Nam.  
**Stack chốt:** ASP.NET Core 8 Web API · React + Vite + Tailwind CSS · PostgreSQL đề xuất.

> Đây là hợp đồng nghiệp vụ chung cho hai repo. Trạng thái **Đã chốt** là yêu cầu triển khai; mục **Cần chốt** không được tự biến thành chính sách sản phẩm.

## 1. Mục tiêu và actor

Khách chọn freelancer hoặc công ty giúp việc, chọn giờ/phạm vi việc, xem giá, đặt cọc, theo dõi ca làm, nghiệm thu và đánh giá. Freelancer và công ty có thể mua gói tăng hiển thị. Admin xét duyệt hồ sơ và xử lý khiếu nại.

| Actor | Trách nhiệm chính |
| --- | --- |
| Khách hàng | Tìm đối tác, đặt lịch, thanh toán cọc, hủy/gia hạn, đưa QR check-in, nghiệm thu, đánh giá, khiếu nại. |
| Freelancer | Đăng ký/KYC, khai báo lịch rảnh, xác nhận đơn, quét QR, thực hiện checklist, ảnh trước/sau, đánh giá khách, mua gói. |
| Công ty đối tác | Đăng ký chờ duyệt, nhận đơn, tự bố trí người làm, quản lý người được gắn vào đơn, đánh giá khách, mua gói. Không cần công khai lịch rảnh. |
| Admin | Duyệt KYC/công ty, quản lý trạng thái đối tác, gói, khiếu nại, quyết định và yêu cầu xem xét lại. |

Người do công ty cử đến là **người thực hiện gắn với booking**, có quyền quét QR khi được ủy quyền; không nhất thiết là actor kinh doanh thứ năm hay một HR portal đầy đủ.

## 2. Phạm vi và luồng nghiệp vụ đã chốt

### 2.1 Khám phá và hồ sơ

- Hồ sơ chỉ hiển thị để đặt khi đã được admin duyệt và đang hoạt động.
- Một danh sách đối tác thống nhất gồm freelancer và công ty; hồ sơ trả phí có nhãn **Được tài trợ/Đề xuất**, không tách thành danh sách đặt hàng riêng. Bộ lọc khu vực, loại đối tác, dịch vụ, thời gian và sắp xếp; phân trang với lựa chọn số kết quả/trang.
- Freelancer công khai các khoảng **rảnh/bận**, khách bấm khoảng rảnh và chọn giờ bắt đầu/kết thúc tùy ý trong một khoảng rảnh liên tục; **ca tối thiểu 2 giờ**, không trùng booking. Giao diện xem tuần theo dải chọn ngày, lịch chi tiết cho ngày đang chọn.
- Công ty không hiển thị slot cá nhân; khách gửi yêu cầu theo giờ, công ty tự chịu trách nhiệm cử người và thực hiện.

### 2.2 Booking và giá

1. Khách chọn đối tác, địa điểm, ngày/giờ, các mục checklist, note; hệ thống hiển thị **giá gốc ca**, phụ phí nếu có và **cọc 15% giá gốc** trước khi xác nhận.
2. Khách thanh toán cọc. Freelancer xác nhận hoặc tự xác nhận nếu đã bật; công ty được coi là xác nhận khi thanh toán cọc thành công.
3. Freelancer từ chối/hết hạn xác nhận hoặc công ty không thể thực hiện: hoàn đủ cọc. Chặn đặt trùng giờ bằng kiểm tra phía server và giữ slot tạm trong thời gian thanh toán.
4. Người thực hiện quét **QR đơn hàng do khách hiển thị**, QR ngắn hạn, dùng một lần; check-in không dựa vào GPS. Người làm lưu ảnh trước/sau gắn booking/đầu việc, cập nhật checklist, gửi nghiệm thu. Khách xác nhận, yêu cầu làm rõ hoặc mở tranh chấp.

**Giá đề xuất để triển khai thử:** đơn giá theo giờ do đối tác thiết lập × thời lượng + phụ phí công việc đặc biệt công khai trước khi đặt. Checklist thông thường mô tả phạm vi, không tự cộng tiền theo từng ô. Chính sách niêm yết/phê duyệt giá cuối cùng vẫn **cần chốt**.

**Hủy do khách:** còn **từ 2 giờ trở lên** trước giờ bắt đầu đã xác nhận thì hoàn toàn bộ cọc; còn **dưới 2 giờ** thì giữ 15% giá gốc, trong đó 10% giá gốc thuộc bên cung cấp, 5% giá gốc thuộc nền tảng. Hủy đúng mốc 2 giờ là miễn phí. Nếu bên cung cấp hủy, hoàn đủ cọc.

**Gia hạn:** chỉ khi ca đang thực hiện và trước giờ kết thúc; khách hoặc người làm đề nghị thời lượng/việc thêm, hệ thống báo giá phát sinh, **hai bên đồng ý** và server kiểm tra lịch không trùng. Đơn qua công ty do công ty hoặc người được ủy quyền chấp thuận. Giá gốc và tiền gia hạn lưu riêng. Từ chối thì giữ nguyên phạm vi/giá; người làm có quyền không thực hiện việc ngoài phạm vi.

**Trạng thái booking:** `PendingDeposit → PendingProviderConfirmation (freelancer) → Confirmed → CheckedIn → InProgress → PendingAcceptance → Completed`; các nhánh `Expired`, `Rejected`, `Cancelled`, `Disputed`. Trạng thái cọc, hoàn cọc, phần tiền còn lại và tiền gia hạn theo dõi riêng.

### 2.3 Gói tăng hiển thị

- Freelancer đã duyệt KYC và công ty đã duyệt/kích hoạt mới được mua. Thanh toán thành công mới kích hoạt; không tự gia hạn.
- Gói cho phép tham gia vị trí ưu tiên trên trang chủ và kết quả tìm kiếm **khi phù hợp bộ lọc**, có nhãn tài trợ; luân phiên giữa các hồ sơ đủ điều kiện; không bảo đảm thứ hạng, lượt xem, booking hay thay đổi điểm đánh giá.
- Freelancer kín lịch hoặc công ty ngừng nhận đơn thì ngừng được đề xuất để đặt; **thời hạn gói vẫn chạy**. Có số lượt hiển thị và mở hồ sơ để đối tác theo dõi.
- Gói 7/30 ngày cho mỗi loại đối tác; **giá hiện có trong đặc tả chỉ là đề xuất**, quản lý qua cấu hình/bảng giá, không hard-code.

### 2.4 Đánh giá hai chiều và khiếu nại

- Sau ca thực sự check-in và kết thúc, khách và phía cung cấp mỗi bên gửi tối đa **1 đánh giá/booking trong 48 giờ**, điểm 1–5 sao và nhận xét tùy chọn; đơn công ty được đánh giá cho **hồ sơ công ty**. Không xem đánh giá của bên kia trước khi công bố. Công bố khi cả hai gửi hoặc hết 48 giờ; nếu có khiếu nại, hoãn công bố tới khi tất cả khiếu nại liên quan đóng.
- Khiếu nại chất lượng/tiền ca: gửi khi nghiệm thu hoặc trong **48 giờ** từ lúc ca kết thúc. Khiếu nại an toàn/hư hại/thất lạc: bắt buộc bằng chứng ngay khi gửi và admin tiếp nhận. Bên kia có **24 giờ** phản hồi; admin xem xét, có thể yêu cầu bổ sung bằng chứng và quyết định có lý do.
- Mỗi khiếu nại được yêu cầu **xem xét lại một lần** trong **24 giờ từ lúc nhận quyết định**, bắt buộc có **bằng chứng mới**; admin ra kết luận cuối cùng. Ảnh, địa chỉ, dữ liệu nhạy cảm chỉ cho bên liên quan và admin xem theo quyền.

## 3. Màn hình / module ưu tiên

| Khách hàng | Freelancer / công ty | Admin |
| --- | --- | --- |
| Landing, khám phá + phân trang, hồ sơ, chọn giờ/việc, checkout cọc, chi tiết booking + QR, nghiệm thu, đánh giá, khiếu nại | Đăng ký/hồ sơ, KYC hoặc hồ sơ công ty, lịch rảnh (freelancer), danh sách/chi tiết đơn, QR scanner, checklist/ảnh, đề nghị gia hạn, gói và thống kê, đánh giá/khiếu nại | Hàng chờ duyệt, quản lý đối tác, cấu hình gói, danh sách/trang xử lý khiếu nại và bằng chứng |

Landing page nên có hero tìm đối tác, dịch vụ, danh sách đối tác chung, quy trình đặt lịch, QR check-in và CTA trở thành đối tác. Giao diện hiện tại dùng nhận diện **ABSTRICT**; nội dung điều hướng và giá phải khớp API, tránh số liệu/đảm bảo giả.

## 4. Ngôn ngữ dữ liệu/API chung

- Các đối tượng: `User`, `CustomerProfile`, `FreelancerProfile`, `CompanyProfile`, `CompanyWorkerAssignment`, `ServiceCategory`, `ChecklistTemplate/Item`, `AvailabilityWindow`, `Booking`, `BookingTask`, `BookingExtension`, `BookingPhoto`, `CheckInToken`, `Payment`, `MoneyMovement`, `PromotionPlan`, `PromotionPurchase`, `Rating`, `Dispute`, `DisputeEvidence`, `DisputeDecision`, `Notification`.
- BE là nguồn xác thực cuối cùng cho giá, lịch, quyền, hạn chót và trạng thái. FE chỉ hiển thị dự toán và trạng thái server trả về; mọi mutation nhạy cảm kiểm tra lại ở BE.
- Mọi thời điểm lưu UTC, API ISO 8601; hiển thị `Asia/Ho_Chi_Minh`. Tiền VND là **số nguyên đồng**, không dùng float. ID ổn định (UUID). API dùng mã trạng thái máy đọc được và thông báo tiếng Việt cho người dùng.
- FE/BE thống nhất hợp đồng qua **OpenAPI**, version `/api/v1`, pagination `{items,page,pageSize,totalItems,totalPages}`; cập nhật contract khi đổi nghiệp vụ.
- Ảnh và bằng chứng cần upload an toàn, metadata gắn booking, kiểm tra quyền truy cập và có thời hạn lưu theo chính sách sau khi chốt.

## 5. Cần chốt trước khi tích hợp tiền và automation

1. PSP/cách thu cọc, cách thu **85% còn lại** và tiền gia hạn, thời điểm/luồng chuyển 10% phí hủy cho đối tác; hoàn tiền và đối soát. `MoneyMovement` mô tả nghĩa vụ/luồng tiền, `Payment` ghi nhận giao dịch với cổng thanh toán; không đánh đồng hai khái niệm.
2. Bảng giá chính thức, phí đặc biệt, chính sách giảm giá, mua gói mới khi gói cũ còn hiệu lực và hoàn gói bị admin tạm dừng.
3. Hạn freelancer phản hồi đơn, hạn khách nghiệm thu, no-show và check-in dự phòng.
4. Hạn nộp khiếu nại an toàn/hư hại/thất lạc, hạn admin ra quyết định, thời gian lưu ảnh, bằng chứng và dữ liệu KYC.

**Nguyên tắc triển khai:** Các giá trị chưa chốt để trong cấu hình hoặc đánh dấu `TODO: POLICY`, không tự đưa lên UI như cam kết đã ban hành.
