# Chạy ABSTRICT bằng Docker Compose

Stack phát triển gồm:

- `api`: ASP.NET Core 8, cổng mặc định `8080`.
- `postgres`: PostgreSQL 16, cổng mặc định `5432`.
- `pgadmin`: pgAdmin 4, cổng mặc định `5050`.

## Khởi động

```powershell
Copy-Item .env.docker.example .env.docker
```

Đổi hai mật khẩu trong `.env.docker`, sau đó chạy:

```powershell
docker compose --env-file .env.docker up -d --build
docker compose --env-file .env.docker ps
```

Truy cập:

- API health: `http://localhost:8080/health`
- Swagger: `http://localhost:8080/swagger`
- pgAdmin: `http://localhost:5050`

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

Named volumes `postgres_data` và `pgadmin_data` vẫn được giữ lại. Chỉ thêm `--volumes` nếu chủ động muốn xóa toàn bộ dữ liệu local.

## Migration

Migration `InitialCreate` đã có trong source. Khi chạy bằng Compose, API nhận `Database__ApplyMigrations=true` và tự áp dụng các migration còn thiếu sau khi PostgreSQL healthy.

Khi phát triển entity mới, tạo migration bằng local tool đã khai báo trong `.config/dotnet-tools.json`:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add AddFeatureName --project src/Abstrict.Api/Abstrict.Api.csproj --startup-project src/Abstrict.Api/Abstrict.Api.csproj --output-dir Data/Migrations
```
