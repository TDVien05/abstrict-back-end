# Chạy ABSTRICT bằng Docker Compose

Stack phát triển gồm:

- `api`: ASP.NET Core 8, cổng mặc định `8080`.
- `postgres`: PostgreSQL 16, cổng mặc định `5432`.
- `pgadmin`: pgAdmin 4, cổng mặc định `5050`.
- `minio`: lưu ảnh/tài liệu trong volume `minio_data`, S3 API tại `9000`, console tại `9001`.
- `minio-init`: tạo bucket riêng trước khi API khởi động.

## Khởi động

```powershell
Copy-Item .env.docker.example .env.docker
```

Đổi các mật khẩu trong `.env.docker` (PostgreSQL, pgAdmin và MinIO; mật khẩu MinIO tối thiểu 8 ký tự), sau đó chạy:

```powershell
docker compose --env-file .env.docker up -d --build
docker compose --env-file .env.docker ps
```

Truy cập:

- API health: `http://localhost:8080/health`
- Swagger: `http://localhost:8080/swagger`
- pgAdmin: `http://localhost:5050`
- MinIO console: `http://localhost:9001` (đăng nhập bằng `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD`).
- MinIO S3 API: `http://localhost:9000`.

## Lưu ảnh qua MinIO

Compose cấu hình `Kyc__Storage__Provider=MinIO` và endpoint nội bộ `http://minio:9000`.
Bucket mặc định là `abstrict-private`, có thể đổi bằng `MINIO_BUCKET`. Bucket không cho truy cập ẩn danh;
ảnh/tài liệu được đọc qua các endpoint API có kiểm tra quyền hiện có. Database vẫn lưu `ObjectKey`,
còn nội dung file nằm trong bucket, theo cấu trúc `yyyy/MM/dd/<GUID>`.

MinIO và `mc` được build từ source chính thức với release cố định trong `docker/minio/Dockerfile`,
vì MinIO Community hiện phân phối dưới dạng source: https://github.com/minio/minio.
Lần build đầu cần Internet để tải source và Go dependencies. Console của release này có chức năng
hạn chế; dùng `mc` để quản lý bucket/object:

```powershell
docker compose --env-file .env.docker run --rm --entrypoint /bin/sh minio-init -ec 'mc alias set storage http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"; mc ls --recursive "storage/$MINIO_BUCKET"'
```

Nếu `.env.docker` đã tồn tại, bổ sung các biến `MINIO_*` từ `.env.docker.example`, giữ nguyên các cấu hình khác.
Hai cổng MinIO chỉ được publish trên `127.0.0.1`. Cấu hình Compose này dùng tài khoản root MinIO cho
stack phát triển; khi triển khai production, cấp credentials riêng có quyền giới hạn trong bucket và dùng HTTPS.

Khi chạy API trực tiếp ngoài Docker, mặc định cũng dùng MinIO tại `http://localhost:9000`.
Cấu hình `Kyc:Storage` trong `src/Abstrict.Api/appsettings.Local.json` với `Provider=MinIO`,
`Endpoint=http://localhost:9000`, `Bucket`, `AccessKey`, `SecretKey` tương ứng với `.env.docker`.
File cấu hình local này được bỏ qua bởi Git và Docker build để không đóng gói credentials vào image.
Backend báo lỗi cấu hình ngay khi khởi động nếu thiếu credentials; không tự chuyển sang lưu local.
Nếu cần lưu local, phải chọn rõ `Provider=Local` và cấu hình `LocalRootPath`.
Các file local cũ không được tự động chuyển sang MinIO.

## Kết nối PostgreSQL trong pgAdmin

Đăng nhập pgAdmin bằng `PGADMIN_DEFAULT_EMAIL` và `PGADMIN_DEFAULT_PASSWORD`, sau đó đăng ký server:

| Thuộc tính | Giá trị |
| --- | --- |
| Name | `ABSTRICT PostgreSQL` |
| Host name/address | `postgres` |
| Port | `5432` |
| Maintenance database | Giá trị `POSTGRES_DB` |
| Username | Giá trị `POSTGRES_USER` |
| Password | Giá trị `POSTGRES_PASSWORD` |

Trong mạng Docker phải dùng hostname `postgres`, không dùng `localhost`.

## Dừng stack

```powershell
docker compose --env-file .env.docker down
```

Named volumes `postgres_data`, `pgadmin_data` và `minio_data` vẫn được giữ lại. Chỉ thêm `--volumes` nếu chủ động muốn xóa toàn bộ dữ liệu local, bao gồm ảnh/tài liệu.

## Migration

Migration `InitialCreate` đã có trong source. Khi chạy bằng Compose, API nhận `Database__ApplyMigrations=true` và tự áp dụng các migration còn thiếu sau khi PostgreSQL healthy.

Khi phát triển entity mới, tạo migration bằng local tool đã khai báo trong `.config/dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add AddFeatureName --project src/Abstrict.Api/Abstrict.Api.csproj --startup-project src/Abstrict.Api/Abstrict.Api.csproj --output-dir Data/Migrations
```
