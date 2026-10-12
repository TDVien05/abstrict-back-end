# Customer Profile API

Hồ sơ và ảnh đại diện của khách hàng. Base URL: `/api/v1`. Lỗi trả `ProblemDetails` kèm `code` và `traceId`.

| Method | Route | Vai trò | Mô tả |
| --- | --- | --- | --- |
| POST | `/auth/login` | public | Đăng nhập; khách hàng nhận kèm `customerProfile`. |
| GET | `/customer/profile` | Customer | Hồ sơ của tôi kèm sổ địa chỉ (`addresses`). |
| PUT | `/customer/profile/avatar` | Customer | Tải lên hoặc thay ảnh đại diện. |
| GET | `/customer/profile/avatar` | Customer | Lấy nội dung ảnh đại diện của tôi. |
| DELETE | `/customer/profile/avatar` | Customer | Xóa ảnh đại diện (204). |

Các endpoint `Customer` cần `Authorization: Bearer <accessToken>`.

## Hồ sơ trả về khi đăng nhập

`POST /auth/login` (request và các lỗi xem `CUSTOMER_REGISTRATION_API.md`) trả thêm `customerProfile` khi tài khoản là khách hàng, để FE hiển thị ngay mà không cần gọi thêm:

```json
{
  "accessToken": "<jwt>",
  "tokenType": "Bearer",
  "expiresAtUtc": "2026-10-12T07:46:42.0404341+00:00",
  "userId": "66e7f842-8d27-41bc-9dc3-09ce6a7d32a3",
  "fullName": "Nguyễn Thu Hà",
  "phoneNumber": "0901234567",
  "role": "Customer",
  "customerProfile": {
    "userId": "66e7f842-8d27-41bc-9dc3-09ce6a7d32a3",
    "fullName": "Nguyễn Thu Hà",
    "phoneNumber": "0901234567",
    "avatarUrl": "/api/v1/customer/profile/avatar?v=1791790301777",
    "createdAtUtc": "2026-10-12T07:07:33.539076+00:00",
    "addresses": [
      {
        "id": "3f1c6a52-9b0e-4c1d-8a57-2d6f0e9b7a11",
        "label": "Nhà",
        "apartmentNumber": "12A",
        "floor": "5",
        "buildingName": "Sunrise Riverside",
        "streetAddress": "12 Nguyễn Hữu Thọ",
        "ward": "Phước Kiển",
        "district": "Nhà Bè",
        "city": "Hồ Chí Minh",
        "accessInstructions": "Gọi bảo vệ mở cổng",
        "isDefault": true
      }
    ]
  }
}
```

| Trường | Ghi chú |
| --- | --- |
| `customerProfile` | `null` với tài khoản Freelancer và Admin. |
| `customerProfile.avatarUrl` | `null` khi chưa có ảnh. Là đường dẫn tương đối, tải bằng cách ở mục GET bên dưới. |
| `customerProfile.createdAtUtc` | Thời điểm tạo hồ sơ. |
| `customerProfile.addresses` | Sổ địa chỉ, địa chỉ mặc định đứng đầu, `[]` khi chưa có. Thêm/sửa/xóa qua `/customer/addresses` (xem `BOOKING_API.md`). |

Các trường cấp ngoài (`userId`, `fullName`, `phoneNumber`, `role`) giữ nguyên như trước. Sau khi đổi avatar hoặc thêm/sửa/xóa địa chỉ, FE gọi `GET /customer/profile` để lấy hồ sơ mới nhất (hoặc cập nhật `avatarUrl` từ response của PUT, đặt về `null` sau DELETE).

## PUT `/customer/profile/avatar`

Gửi `multipart/form-data` với trường `file`. Chấp nhận JPG hoặc PNG, tối đa 5 MB. Định dạng được xác định từ nội dung file, không dựa vào đuôi file hay `Content-Type`. Tải ảnh mới sẽ thay và xóa ảnh cũ. Giới hạn 10 lần ghi / 10 phút / người dùng (tính chung với DELETE).

```json
{
  "avatarUrl": "/api/v1/customer/profile/avatar?v=1791789964426",
  "updatedAtUtc": "2026-10-12T07:26:04.42611+00:00"
}
```

`avatarUrl` là đường dẫn tương đối; tham số `v` đổi sau mỗi lần cập nhật để tránh cache ảnh cũ.

## GET `/customer/profile/avatar`

Trả nội dung ảnh (`image/jpeg` hoặc `image/png`). Vì endpoint cần access token nên FE không gắn thẳng vào `<img src>` được: hãy `fetch` kèm header rồi dùng `URL.createObjectURL(blob)`. Chưa có ảnh trả 404 `AVATAR_NOT_FOUND`.

## DELETE `/customer/profile/avatar`

Xóa ảnh và trả 204. Gọi khi chưa có ảnh vẫn trả 204.

## Mã lỗi

| HTTP | `code` | Khi nào |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | Thiếu trường `file`, hoặc request vượt quá giới hạn kích thước. |
| 400 | `EMPTY_FILE` | File rỗng. |
| 404 | `AVATAR_NOT_FOUND` | Chưa có ảnh đại diện. |
| 413 | `FILE_TOO_LARGE` | Ảnh lớn hơn 5 MB. |
| 415 | `UNSUPPORTED_IMAGE` | Nội dung không phải JPG hoặc PNG. |
| 429 | — | Vượt giới hạn tần suất. |
