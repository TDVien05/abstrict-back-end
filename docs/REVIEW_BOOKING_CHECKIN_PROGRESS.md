# Review commit `214225b` — Check-in, ảnh tiến độ và hoàn tất ca (freelancer)

Tài liệu này tóm tắt commit vừa push để bạn review nhanh: làm gì, sinh ra file nào, luồng nghiệp vụ và điểm cần lưu ý.

## Thông tin commit

| | |
| --- | --- |
| Hash | `214225b` |
| Branch | `feat/booking-checkin-progress` (tạo từ `feat/addresses`) |
| Message | `feat: add freelancer check-in, progress photos and work completion` |
| Quy mô | 15 file, +789 / −21 |
| PR | https://github.com/TDVien05/abstrict-back-end/pull/new/feat/booking-checkin-progress |

## Mục tiêu

Khép kín phần "trong ca" của đơn đặt lịch: khách phát QR check-in → freelancer quét để bắt đầu ca → tải ảnh tiến độ Before/After, cập nhật checklist → chủ động hoàn tất ca để chờ khách nghiệm thu. Trước commit này, luồng sau `Confirmed` gần như chưa có nghiệp vụ.

## Luồng trạng thái

```text
Confirmed ──check-in QR──▶ CheckedIn ──ảnh Before đầu tiên──▶ InProgress
                                │                                  │
                                └────── hoàn tất ca (cần ảnh After) ┘
                                                ▼
                                        PendingAcceptance
```

Nghiệm thu phía khách (`PendingAcceptance → Completed`) **chưa** làm — để giai đoạn sau.

## API mới

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/bookings/{id}/check-in-token` | Customer | Phát QR check-in dùng một lần, TTL 120s. |
| POST | `/freelancer/bookings/{id}/check-in` | Freelancer | Quét token → `CheckedIn`. |
| PATCH | `/freelancer/bookings/{id}/tasks/{taskId}` | Freelancer | Đổi trạng thái mục checklist. |
| POST | `/freelancer/bookings/{id}/photos` | Freelancer | Tải ảnh Before/After (`multipart/form-data`). |
| GET | `/freelancer/bookings/{id}/photos/{photoId}/content` | Freelancer | Nội dung ảnh (private). |
| GET | `/bookings/{id}/photos/{photoId}/content` | Customer | Nội dung ảnh tiến độ của đơn mình. |
| POST | `/freelancer/bookings/{id}/complete` | Freelancer | Hoàn tất ca (cần ≥ 1 ảnh After) → `PendingAcceptance`. |

## File thay đổi (để review)

### Tài liệu / cấu hình
| File | Thay đổi |
| --- | --- |
| `.gitignore` | Thêm `.codegraph/`. |
| `docs/BOOKING_API.md` | Bổ sung bảng endpoint, mục "Check-in, ảnh tiến độ và hoàn tất ca", timeline reason và mã lỗi mới. |
| `docs/FEATURES.md` | **File mới** (276 dòng): tổng hợp toàn bộ chức năng backend hiện có. |

### Controllers
| File | Thay đổi |
| --- | --- |
| `BookingControllerBase.cs` | Thêm helper `ImageFile(BookingPhotoContent)` — phát ảnh với `Cache-Control: private, no-store` và `X-Content-Type-Options: nosniff`. |
| `BookingsController.cs` | Thêm `GET /bookings/{id}/photos/{photoId}/content`. |
| `FreelancerBookingsController.cs` | Thêm các endpoint check-in, tasks, photos (upload + content), complete. |

### DTOs
| File | Thay đổi |
| --- | --- |
| `DTOs/Requests/BookingRequests.cs` | Thêm `CheckInRequest`, `UpdateBookingTaskStatusRequest`. |
| `DTOs/Responses/BookingResponses.cs` | Thêm `BookingPhotoResponse`; thêm `Photos` vào `BookingDetailResponse` và `FreelancerBookingDetailResponse`; mở rộng `FreelancerBookingAction` = `Confirm, Reject, CheckIn, UploadPhotos, CompleteWork`. |

### Services
| File | Thay đổi |
| --- | --- |
| `BookingMapping.cs` | Overload `IsCheckInOpen(...)`; `FreelancerActions` nhận thêm khung giờ + options và trả `CheckIn`/`UploadPhotos`/`CompleteWork`; thêm `Photos(...)`; `ToDetail` kèm danh sách ảnh. |
| `BookingPolicies.cs` | State machine thêm `CheckedIn → PendingAcceptance`, `InProgress → PendingAcceptance`; timeline reason `CheckedIn`, `WorkStarted`, `WorkFinished`; thêm `BookingCompletionPolicy` và `BookingTaskPolicy`. |
| `BookingService.cs` | Inject `IFileStorage`; nạp kèm `Photos`; thêm `OpenPhotoAsync` (kiểm tra quyền sở hữu phía khách). |
| `FreelancerBookingService.cs` | Thêm `CheckInAsync`, `UpdateTaskStatusAsync`, `CompleteWorkAsync`, `UploadPhotoAsync`, `OpenPhotoAsync` (phần lớn +198 dòng). |
| `IBookingService.cs` | Thêm `OpenPhotoAsync` + record `BookingPhotoContent(Stream, string)`. |
| `IFreelancerBookingService.cs` | Thêm 5 method tương ứng. |

### Tests
| File | Thay đổi |
| --- | --- |
| `BookingPolicyTests.cs` | +72 dòng: cửa sổ check-in, hash token một chiều, `FreelancerActions` (CheckIn/UploadPhotos/CompleteWork), `BookingCompletionPolicy`, `BookingTaskPolicy`, cập nhật bảng chuyển trạng thái. |

## Luật nghiệp vụ mới

- **Check-in:** token 32 byte ngẫu nhiên, DB chỉ lưu SHA-256 hash, sống 120 giây, dùng một lần; chỉ mở khi đơn `Confirmed` và trong khung từ 30 phút trước giờ bắt đầu đến giờ kết thúc.
- **Ảnh:** chỉ nhận `Before`/`After`, JPG/PNG thật (magic bytes), ≤ 5 MB; chỉ tải khi đơn `CheckedIn`/`InProgress`; ảnh `Before` đầu tiên chuyển `InProgress`; lưu ngoài `wwwroot`, phát qua endpoint có phân quyền.
- **Checklist:** chỉ nhận `Pending`/`InProgress`/`Completed`; `Completed` ghi `completedAtUtc`.
- **Hoàn tất ca:** chỉ khi đơn `CheckedIn`/`InProgress` và có ≥ 1 ảnh `After`, không bắt buộc mọi task `Completed`; chuyển `PendingAcceptance` (lý do `WorkFinished`) + thông báo khách.

## Kiểm thử & xác minh

- `dotnet build Abstrict.sln`: thành công (chỉ còn warning XML doc có sẵn từ trước).
- `dotnet test tests/Abstrict.Api.Tests`: **101/101 pass**.
- Không cần migration — các cột/bảng (`BookingTask.Status`, `CompletedAtUtc`, `BookingPhoto`, `CheckInToken`) đã có sẵn từ trước.

## Điểm cần review / lưu ý

1. **Commit gộp nhiều phần:** ngoài phần tôi thêm trong phiên này (hoàn tất ca + trạng thái task), commit còn chứa phần đã có sẵn trong working tree từ trước (phát QR check-in, upload ảnh). Tất cả cùng một khu vực tính năng nên gộp chung; nếu muốn tách nhỏ thì báo tôi.
2. **Cho phép hoàn tất từ `CheckedIn`:** để không chặn trường hợp quên upload ảnh Before; đổi lại điều kiện bắt buộc là có ảnh `After`. Cân nhắc nếu bạn muốn bắt buộc phải có ảnh Before.
3. **`BookingPhotoType.Evidence`** hiện vẫn bị chặn (chỉ Before/After) — giữ nguyên, chưa dùng.
4. **Không có geofence** khi check-in: chỉ dựa vào QR + khung giờ, không kiểm tra vị trí.
5. **Chưa có test tích hợp** cho `UploadPhotoAsync`/`OpenPhotoAsync`/`CompleteWorkAsync` (chỉ test policy thuần). Nên bổ sung test service nếu muốn phủ DB/file storage.
6. **Chưa gồm `.claude/`** (settings công cụ cá nhân) trong commit — vẫn untracked.
