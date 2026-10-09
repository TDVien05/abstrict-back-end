<#
 Seeds fake freelancer accounts (user + provider + freelancer profile + services + areas)
 into the dockerised Postgres. Idempotent: deterministic IDs + ON CONFLICT DO NOTHING.
 Usage:  powershell -File scripts/seed-fake-freelancers.ps1 [-Container abstrict-postgres] [-Password "Freelancer-dev-2026"]
 Phones: 0910000001..0910000008, all sharing the same dev password. DEV ONLY.
#>
param(
    [string]$Container = 'abstrict-postgres',
    [string]$Database = 'abstrict',
    [string]$DbUser = 'abstrict',
    [string]$Password = 'Freelancer-dev-2026'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)

# ASP.NET Core Identity v3 hash (PBKDF2-HMAC-SHA512, 100k iterations) so the real login endpoint accepts it.
function New-IdentityV3Hash([string]$plain) {
    $iterations = 100000
    $salt = New-Object byte[] 16
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($plain, $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA512)
    $subkey = $kdf.GetBytes(32)
    function BE([int]$v) { $b = [BitConverter]::GetBytes([uint32]$v); [Array]::Reverse($b); $b }
    $buf = [byte[]]@(1) + (BE 2) + (BE $iterations) + (BE 16) + $salt + $subkey
    [Convert]::ToBase64String($buf)
}

function Q($s) { if ($null -eq $s) { 'NULL' } else { "'" + ([string]$s).Replace("'", "''") + "'" } }

$codes = 'CLEANING','DEEP_CLEANING','COOKING','LAUNDRY','CHILDCARE','ELDERLY_CARE','AC_SERVICE','ELECTRICAL','PLUMBING','MOVING','GARDENING','PEST_CONTROL'

$people = @(
    @{ Name='Nguyễn Thị Lan'; Gender='Female'; Dob='1992-03-14'; Status='Approved';    Rating=4.8; Count=42; Done=38; Exp=6;  Bio='Dọn dẹp nhà cửa gọn gàng, tỉ mỉ.';                 Services=@('CLEANING:80000','DEEP_CLEANING:120000'); Areas=@(1,2,3); Address='12 Nguyễn Huệ, Quận 1, TP.HCM' },
    @{ Name='Trần Văn Hùng';  Gender='Male';   Dob='1988-11-02'; Status='Approved';    Rating=4.6; Count=25; Done=22; Exp=9;  Bio='Thợ điện nước nhiều năm kinh nghiệm.';              Services=@('ELECTRICAL:150000','PLUMBING:140000');   Areas=@(3,4,5); Address='45 Phạm Văn Đồng, Bình Thạnh, TP.HCM' },
    @{ Name='Lê Thị Mai';     Gender='Female'; Dob='1995-07-21'; Status='Approved';    Rating=4.9; Count=61; Done=57; Exp=4;  Bio='Nấu ăn gia đình, món Bắc - Nam.';                   Services=@('COOKING:100000');                       Areas=@(6,7);   Address='8 Láng Hạ, Đống Đa, Hà Nội' },
    @{ Name='Phạm Quốc Bảo';  Gender='Male';   Dob='1990-01-30'; Status='Approved';    Rating=4.3; Count=11; Done=9;  Exp=5;  Bio='Vệ sinh và bảo trì máy lạnh.';                      Services=@('AC_SERVICE:130000');                    Areas=@(1,5);   Address='99 Cộng Hòa, Tân Bình, TP.HCM' },
    @{ Name='Võ Thị Hồng';    Gender='Female'; Dob='1985-09-09'; Status='Approved';    Rating=5.0; Count=8;  Done=8;  Exp=12; Bio='Chăm sóc người cao tuổi, có chứng chỉ điều dưỡng.'; Services=@('ELDERLY_CARE:110000','CHILDCARE:100000'); Areas=@(9,10);  Address='3 Bạch Đằng, Hải Châu, Đà Nẵng' },
    @{ Name='Đặng Minh Tuấn'; Gender='Male';   Dob='1999-05-17'; Status='Submitted';   Rating=0;   Count=0;  Done=0;  Exp=1;  Bio='Mới tham gia, nhận giặt ủi và dọn dẹp.';           Services=@('LAUNDRY:70000','CLEANING:75000');       Areas=@(2);     Address='21 Cầu Giấy, Cầu Giấy, Hà Nội' },
    @{ Name='Hoàng Thị Thu';  Gender='Female'; Dob='1993-12-25'; Status='UnderReview'; Rating=0;   Count=0;  Done=0;  Exp=3;  Bio='Chăm sóc trẻ em tại nhà.';                           Services=@('CHILDCARE:95000');                      Areas=@(7,8);   Address='56 Xuân Thủy, Cầu Giấy, Hà Nội' },
    @{ Name='Bùi Anh Khoa';   Gender='Male';   Dob='2000-08-08'; Status='Draft';       Rating=0;   Count=0;  Done=0;  Exp=0;  Bio=$null;                                           Services=@();                                       Areas=@();      Address='17 Lý Thường Kiệt, Quận 3, TP.HCM' }
)

$hash = New-IdentityV3Hash $Password
$sql = New-Object Text.StringBuilder
[void]$sql.AppendLine('BEGIN;')
for ($i = 0; $i -lt $people.Count; $i++) {
    $n = $i + 1; $p = $people[$i]
    $uid = 'cccccccc-0000-0000-0000-{0:D12}' -f $n
    $prov = 'dddddddd-0000-0000-0000-{0:D12}' -f $n
    $fid = 'eeeeeeee-0000-0000-0000-{0:D12}' -f $n
    $phone = '09100000{0:D2}' -f $n
    $cccd = '0790{0:D8}' -f (90000000 + $n)
    $accepting = if ($p.Status -eq 'Approved') { 'true' } else { 'false' }
    $userStatus = if ($p.Status -eq 'Draft') { 'Pending' } else { 'Active' }
    $verified = if ($p.Status -eq 'Draft') { 'NULL' } else { 'now()' }
    $last4 = $cccd.Substring($cccd.Length - 4)
    [void]$sql.AppendLine("INSERT INTO users (id, phone_number, password_hash, role, status, phone_verified_at_utc, terms_accepted_at_utc, privacy_policy_accepted_at_utc, created_at_utc, updated_at_utc) VALUES ('$uid', '$phone', '$hash', 'Freelancer', '$userStatus', $verified, now(), now(), now(), now()) ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO providers (id, type, display_name, approval_status, is_accepting_bookings, bio, average_rating, rating_count, completed_booking_count, created_at_utc, updated_at_utc) VALUES ('$prov', 'Freelancer', $(Q $p.Name), '$($p.Status)', $accepting, $(Q $p.Bio), $($p.Rating), $($p.Count), $($p.Done), now(), now()) ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO freelancer_profiles (id, provider_id, user_id, legal_full_name, date_of_birth, gender, permanent_address, current_address, experience_years, auto_accept_bookings, citizen_id_hash, citizen_id_last4, citizen_id_number_protected, created_at_utc, updated_at_utc) VALUES ('$fid', '$prov', '$uid', $(Q $p.Name), '$($p.Dob)', '$($p.Gender)', $(Q $p.Address), $(Q $p.Address), $($p.Exp), false, encode(sha256(convert_to('fake-$cccd', 'UTF8')), 'hex'), '$last4', 'FAKE-SEED-NOT-DECRYPTABLE', now(), now()) ON CONFLICT DO NOTHING;")
    $k = 0
    foreach ($svc in $p.Services) {
        $code, $rate = $svc.Split(':'); $k++
        $catId = 'aaaaaaaa-0000-0000-0000-{0:D12}' -f ([array]::IndexOf($codes, $code) + 1)
        $rowId = 'ffffffff-0000-0000-{0:D4}-{1:D12}' -f $n, $k
        [void]$sql.AppendLine("INSERT INTO provider_services (id, provider_id, service_category_id, hourly_rate_vnd, is_active, created_at_utc, updated_at_utc) VALUES ('$rowId', '$prov', '$catId', $rate, true, now(), now()) ON CONFLICT DO NOTHING;")
    }
    $k = 0
    foreach ($a in $p.Areas) {
        $k++
        $areaId = 'bbbbbbbb-0000-0000-0000-{0:D12}' -f $a
        $rowId = '99999999-0000-0000-{0:D4}-{1:D12}' -f $n, $k
        [void]$sql.AppendLine("INSERT INTO provider_service_areas (id, provider_id, service_area_id, travel_radius_km, created_at_utc, updated_at_utc) VALUES ('$rowId', '$prov', '$areaId', 10, now(), now()) ON CONFLICT DO NOTHING;")
    }
}
[void]$sql.AppendLine('COMMIT;')

$sql.ToString() | docker exec -i $Container psql -U $DbUser -d $Database -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Seeding failed.' }
Write-Host "Seeded $($people.Count) fake freelancers (phones 0910000001..0910000008, password '$Password')."
