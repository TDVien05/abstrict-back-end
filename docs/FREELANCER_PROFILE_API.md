# Freelancer Profile API

Danh bạ freelancer **công khai** (không cần đăng nhập) cho màn hình tìm kiếm / xem hồ sơ của khách hàng. Chỉ trả freelancer có `ApprovalStatus = Approved`; hồ sơ Draft / Submitted / UnderReview / Rejected luôn trả 404 hoặc bị lọc khỏi danh sách.

Không bao giờ trả: số điện thoại, CCCD, ngày sinh, địa chỉ cư trú, ảnh KYC. Dữ liệu đó chỉ có ở các API KYC (xem `FREELANCER_KYC_API.md`).

Base URL: `/api/v1`. Lỗi trả `ProblemDetails` kèm `code` và `traceId`.

## Tổng hợp các API liên quan đến hồ sơ freelancer

| Method | Route | Vai trò | Mô tả | Tài liệu |
| --- | --- | --- | --- | --- |
| GET | `/freelancers` | public | Tìm kiếm / liệt kê freelancer đã duyệt. | file này |
| GET | `/freelancers/{providerId}` | public | Chi tiết hồ sơ công khai. | file này |
| POST | `/auth/register-freelancer` | public | Đăng ký tài khoản freelancer (OTP). | `FREELANCER_KYC_API.md` |
| GET | `/freelancer/kyc` | Freelancer | Hồ sơ KYC của chính mình. | `FREELANCER_KYC_API.md` |
| PUT | `/freelancer/kyc/profile` | Freelancer | Lưu thông tin cá nhân + CCCD. | `FREELANCER_KYC_API.md` |
| PUT | `/freelancer/kyc/documents/{type}` | Freelancer | Tải ảnh giấy tờ. | `FREELANCER_KYC_API.md` |
| POST | `/freelancer/kyc/submit` | Freelancer | Gửi duyệt. | `FREELANCER_KYC_API.md` |
| GET | `/admin/kyc`, `/admin/kyc/{providerId}` | Admin | Duyệt hồ sơ KYC. | `FREELANCER_KYC_API.md` |

---

## GET `/freelancers`

Query (tất cả tùy chọn):

| Tham số | Kiểu | Mô tả |
| --- | --- | --- |
| `serviceCode` | string | Mã dịch vụ, không phân biệt hoa thường: `CLEANING`, `DEEP_CLEANING`, `COOKING`, `LAUNDRY`, `CHILDCARE`, `ELDERLY_CARE`, `AC_SERVICE`, `ELECTRICAL`, `PLUMBING`, `MOVING`, `GARDENING`, `PEST_CONTROL`. Chỉ tính dịch vụ đang bật. |
| `city` | string | Thành phố khớp chính xác với khu vực phục vụ (vd `Hồ Chí Minh`). |
| `district` | string | Quận khớp chính xác (vd `Quận 1`). Kết hợp với `city` để tránh trùng tên quận. |
| `minRating` | number | Điểm trung bình tối thiểu (0-5). |
| `acceptingBookings` | bool | `true` chỉ lấy người đang nhận đơn. |
| `page` | int | Mặc định 1. |
| `pageSize` | int | Mặc định 20, tối đa 50 (giá trị lớn hơn bị cắt về 50). |

Sắp xếp: `averageRating` giảm dần, rồi `completedBookingCount` giảm dần.

Ví dụ: `GET /api/v1/freelancers?serviceCode=CLEANING&city=Hồ%20Chí%20Minh&minRating=4.5` (URL-encode ký tự tiếng Việt).

Response `200`:

```json
{
  "items": [
    {
      "providerId": "dddddddd-0000-0000-0000-000000000001",
      "displayName": "Nguyễn Thị Lan",
      "bio": "Dọn dẹp nhà cửa gọn gàng, tỉ mỉ.",
      "averageRating": 4.8,
      "ratingCount": 42,
      "completedBookingCount": 38,
      "experienceYears": 6,
      "isAcceptingBookings": true,
      "minHourlyRateVnd": 80000,
      "serviceCodes": ["CLEANING", "DEEP_CLEANING"]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

`minHourlyRateVnd` là `null` khi freelancer chưa có dịch vụ nào đang bật.

## GET `/freelancers/{providerId}`

`providerId` là GUID lấy từ danh sách.

Response `200`:

```json
{
  "providerId": "dddddddd-0000-0000-0000-000000000005",
  "displayName": "Võ Thị Hồng",
  "bio": "Chăm sóc người cao tuổi, có chứng chỉ điều dưỡng.",
  "gender": "Female",
  "averageRating": 5,
  "ratingCount": 8,
  "completedBookingCount": 8,
  "experienceYears": 12,
  "isAcceptingBookings": true,
  "services": [
    { "code": "ELDERLY_CARE", "name": "Chăm sóc người cao tuổi", "hourlyRateVnd": 110000, "description": null }
  ],
  "serviceAreas": [
    { "id": "bbbbbbbb-0000-0000-0000-000000000009", "city": "Đà Nẵng", "district": "Hải Châu", "wardOrComplex": null, "travelRadiusKm": 10 }
  ]
}
```

Lỗi:

| HTTP | `code` | Khi nào |
| --- | --- | --- |
| 404 | `FREELANCER_NOT_FOUND` | Không tồn tại, hoặc chưa được duyệt. |
| 404 | (không có body) | `providerId` không phải GUID hợp lệ (route không khớp). |

## Dữ liệu mẫu

`scripts/seed-fake-freelancers.ps1` tạo 8 freelancer giả (5 Approved, 1 Submitted, 1 UnderReview, 1 Draft), số `0910000001`-`0910000008`. Chỉ 5 người Approved xuất hiện trong các API trên.
