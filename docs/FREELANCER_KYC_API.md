# Freelancer KYC API

Luồng: đăng ký freelancer (OTP) -> đăng nhập -> khai báo thông tin + CCCD -> tải 3 ảnh -> gửi duyệt -> admin duyệt / yêu cầu chỉnh sửa / từ chối.

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/api/v1/auth/register-freelancer` | public | Như `/auth/register` nhưng tạo tài khoản Freelancer. Xác thực OTP qua `/auth/verify-phone-otp`. |
| GET | `/api/v1/freelancer/kyc` | Freelancer | Trạng thái, thông tin (chỉ 4 số cuối CCCD), ảnh, phản hồi của admin. |
| PUT | `/api/v1/freelancer/kyc/profile` | Freelancer | Lưu thông tin. Tuổi >= 18, CCCD 12 số, một CCCD chỉ dùng cho một tài khoản (409 `CITIZEN_ID_ALREADY_USED`). |
| PUT | `/api/v1/freelancer/kyc/documents/{CitizenIdFront\|CitizenIdBack\|FaceVerification}` | Freelancer | multipart `file`; JPG/PNG thật (kiểm tra magic bytes), <= 5 MB. Thay ảnh cũ cùng loại. |
| POST | `/api/v1/freelancer/kyc/submit` | Freelancer | Cần đủ 3 ảnh, không còn ảnh bị từ chối. Tạo `ProviderApprovalReview`. |
| GET | `/api/v1/freelancer/kyc/documents/{id}/content` | Freelancer (chủ sở hữu) | Ảnh của chính mình. |
| GET | `/api/v1/admin/kyc?status=&page=&pageSize=` | Admin | Bỏ trống `status` = hàng chờ (Submitted + UnderReview). |
| GET | `/api/v1/admin/kyc/{providerId}` | Admin | Chi tiết, kèm số CCCD giải mã. Ghi audit. |
| POST | `/api/v1/admin/kyc/{providerId}/start-review` | Admin | Submitted -> UnderReview. |
| POST | `/api/v1/admin/kyc/{providerId}/decision` | Admin | `{decision: Approved\|Rejected\|ChangesRequested, reason, rejectedDocumentTypes[]}`; lý do >= 10 ký tự trừ khi duyệt. |
| GET | `/api/v1/admin/kyc/{providerId}/documents/{id}/content` | Admin | Ảnh giấy tờ. Ghi audit. |

Trạng thái `Provider.ApprovalStatus`: Draft -> Submitted -> UnderReview -> Approved | Rejected | (ChangesRequested quay về Draft). Draft và Rejected được phép sửa và gửi lại.

## Bảo mật và cấu hình

- Số CCCD mã hóa bằng ASP.NET Data Protection; cột `citizen_id_hash` là HMAC-SHA256 (khóa `Kyc:CitizenIdHashKey`, bắt buộc ngoài Development) để chặn trùng. Không log nội dung KYC.
- Ảnh lưu ngoài wwwroot qua `IFileStorage` (`Storage:RootPath`), tên file là GUID; chỉ phát qua endpoint có phân quyền với `Cache-Control: private, no-store`.
- Data Protection keys lưu ở `DataProtection:KeysPath`. **Mất thư mục này là mất khả năng giải mã số CCCD**; production cần volume bền vững và sao lưu.
- Admin đầu tiên: đặt `Admin:SeedPhoneNumber`, `Admin:SeedPassword` (>= 12 ký tự), tùy chọn `Admin:SeedFullName`; được tạo khi khởi động nếu số chưa tồn tại. Giá trị mặc định chỉ có trong `appsettings.Development.json`.
- Không dùng OCR / so khớp khuôn mặt tự động: admin đối chiếu bằng mắt.
