<#
 Seeds what is needed to try the booking flow:
 - availability windows for the approved fake freelancers (see seed-fake-freelancers.ps1):
   08:00-12:00 and 13:00-18:00 Vietnam time, every day, starting today;
 - a test customer (phone 0920000001, password Customer-dev-2026) with one address in Quan 1, Ho Chi Minh.
 Idempotent: deterministic IDs + ON CONFLICT DO NOTHING; re-run any day to extend the calendar.
 Usage:  powershell -File scripts/seed-booking-fixtures.ps1 [-Days 14] [-Container abstrict-postgres]
 DEV ONLY.
#>
param(
    [string]$Container = 'abstrict-postgres',
    [string]$Database = 'abstrict',
    [string]$DbUser = 'abstrict',
    # Remote Postgres (e.g. Railway public URL). When set, psql runs in a throwaway postgres:16-alpine container instead of `docker exec`.
    [string]$ConnectionString = '',
    [int]$Days = 14,
    [string]$CustomerPhone = '0920000001',
    [string]$CustomerPassword = 'Customer-dev-2026'
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

# Vietnam is UTC+7 with no DST: 08-12 and 13-18 local are 01-05 and 06-11 UTC on the same calendar day.
$shifts = @(@{ Slot = 1; From = 1; To = 5 }, @{ Slot = 2; From = 6; To = 11 })
$today = [DateTime]::UtcNow.AddHours(7).Date

$sql = New-Object Text.StringBuilder
[void]$sql.AppendLine('BEGIN;')
for ($n = 1; $n -le 5; $n++) {
    $prov = 'dddddddd-0000-0000-0000-{0:D12}' -f $n
    for ($d = 0; $d -lt $Days; $d++) {
        $day = $today.AddDays($d)
        foreach ($shift in $shifts) {
            $id = 'a11a0000-0000-0000-{0:D4}-{1:yyyyMMdd}{2:D4}' -f $n, $day, $shift.Slot
            $from = '{0:yyyy-MM-dd} {1:D2}:00:00+00' -f $day, $shift.From
            $to = '{0:yyyy-MM-dd} {1:D2}:00:00+00' -f $day, $shift.To
            [void]$sql.AppendLine("INSERT INTO availability_windows (id, provider_id, starts_at_utc, ends_at_utc, is_active, created_at_utc, updated_at_utc) VALUES ('$id', '$prov', '$from', '$to', true, now(), now()) ON CONFLICT DO NOTHING;")
        }
    }
}

# Test customer with a saved address in Quận 1 (served by freelancer 1), so the flow can be tried without OTP.
$custUser = 'c0c0c0c0-0000-0000-0000-000000000001'
$custHash = New-IdentityV3Hash $CustomerPassword
[void]$sql.AppendLine("INSERT INTO users (id, phone_number, password_hash, role, status, phone_verified_at_utc, terms_accepted_at_utc, privacy_policy_accepted_at_utc, created_at_utc, updated_at_utc) VALUES ('$custUser', '$CustomerPhone', '$custHash', 'Customer', 'Active', now(), now(), now(), now(), now()) ON CONFLICT DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO customer_profiles (id, user_id, full_name, created_at_utc, updated_at_utc) SELECT 'c0c0c0c0-0000-0000-0001-000000000001', '$custUser', 'Khách Hàng Thử', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$custUser') ON CONFLICT DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO customer_addresses (id, customer_user_id, label, apartment_number, floor, building_name, street_address, ward, district, city, access_instructions, is_default, created_at_utc, updated_at_utc) SELECT 'c0c0c0c0-0000-0000-0002-000000000001', '$custUser', 'Nhà', 'A-1205', '12', 'Vinhomes Golden River', '2 Tôn Đức Thắng', 'Bến Nghé', 'Quận 1', 'Hồ Chí Minh', 'Gọi lễ tân', true, now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$custUser') ON CONFLICT DO NOTHING;")
[void]$sql.AppendLine('COMMIT;')

if ($ConnectionString) {
    $sql.ToString() | docker run --rm -i postgres:16-alpine psql $ConnectionString -v ON_ERROR_STOP=1 -q
} else {
    $sql.ToString() | docker exec -i $Container psql -U $DbUser -d $Database -v ON_ERROR_STOP=1 -q
}
if ($LASTEXITCODE -ne 0) { throw 'Seeding failed.' }
Write-Host "Seeded availability for 5 freelancers, $Days days from $($today.ToString('yyyy-MM-dd')) (08:00-12:00 and 13:00-18:00 Vietnam time)."
Write-Host "Test customer: phone $CustomerPhone, password '$CustomerPassword', one address in Quan 1."
