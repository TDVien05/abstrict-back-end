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

# Complaint fixtures: one existing Open case for the admin queue, and one clean Completed booking for the customer to submit a new case.
$serviceCategoryId = 'aaaaaaaa-0000-0000-0000-000000000001'
$complaintBooking = 'b00b0000-0000-0000-0000-000000000001'
$cleanBooking = 'b00b0000-0000-0000-0000-000000000002'
$readyBooking = 'b00b0000-0000-0000-0000-000000000003'
$sampleDispute = 'd15f0000-0000-0000-0000-000000000001'
$sampleDisputeMessage = 'd15f0000-0000-0001-0000-000000000001'
[void]$sql.AppendLine("INSERT INTO bookings (id, booking_number, customer_user_id, provider_id, service_category_id, customer_address_id, status, starts_at_utc, ends_at_utc, completed_at_utc, unit_price_vnd, base_amount_vnd, surcharge_amount_vnd, deposit_amount_vnd, extension_amount_vnd, address_snapshot, created_at_utc, updated_at_utc) SELECT '$complaintBooking', 'ABDEMO-C001', '$custUser', 'dddddddd-0000-0000-0000-000000000001', '$serviceCategoryId', 'c0c0c0c0-0000-0000-0002-000000000001', 'Completed', now() - interval '3 days', now() - interval '1 day', now() - interval '1 day', 80000, 320000, 0, 48000, 0, 'A-1205, Vinhomes Golden River, 2 Tôn Đức Thắng, Bến Nghé, Quận 1, Hồ Chí Minh', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$custUser') ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO bookings (id, booking_number, customer_user_id, provider_id, service_category_id, customer_address_id, status, starts_at_utc, ends_at_utc, completed_at_utc, unit_price_vnd, base_amount_vnd, surcharge_amount_vnd, deposit_amount_vnd, extension_amount_vnd, address_snapshot, created_at_utc, updated_at_utc) SELECT '$cleanBooking', 'ABDEMO-C002', '$custUser', 'dddddddd-0000-0000-0000-000000000002', '$serviceCategoryId', 'c0c0c0c0-0000-0000-0002-000000000001', 'Completed', now() - interval '5 days', now() - interval '3 days', now() - interval '3 days', 80000, 320000, 0, 48000, 0, 'A-1205, Vinhomes Golden River, 2 Tôn Đức Thắng, Bến Nghé, Quận 1, Hồ Chí Minh', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$custUser') ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO bookings (id, booking_number, customer_user_id, provider_id, service_category_id, customer_address_id, status, starts_at_utc, ends_at_utc, completed_at_utc, unit_price_vnd, base_amount_vnd, surcharge_amount_vnd, deposit_amount_vnd, extension_amount_vnd, address_snapshot, created_at_utc, updated_at_utc) SELECT '$readyBooking', 'ABDEMO-C003', '$custUser', 'dddddddd-0000-0000-0000-000000000003', '$serviceCategoryId', 'c0c0c0c0-0000-0000-0002-000000000001', 'Completed', now() - interval '7 days', now() - interval '5 days', now() - interval '5 days', 80000, 320000, 0, 48000, 0, 'A-1205, Vinhomes Golden River, 2 Tôn Đức Thắng, Bến Nghé, Quận 1, Hồ Chí Minh', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$custUser') ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT 'b00b0000-0000-0001-0000-000000000001', '$complaintBooking', 'PendingDeposit', 'Completed', '$custUser', 'DemoSeed', now(), now() ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT 'b00b0000-0000-0001-0000-000000000002', '$cleanBooking', 'PendingDeposit', 'Completed', '$custUser', 'DemoSeed', now(), now() ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT 'b00b0000-0000-0001-0000-000000000004', '$readyBooking', 'PendingDeposit', 'Completed', '$custUser', 'DemoSeed', now(), now() ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO disputes (id, booking_id, opened_by_user_id, category, status, description, response_deadline_utc, appeal_used, created_at_utc, updated_at_utc) SELECT '$sampleDispute', '$complaintBooking', '$custUser', 'ServiceQuality', 'Open', 'Khu vực bếp chưa được vệ sinh kỹ như nội dung checklist. Tôi gửi ảnh và mong được xem xét.', now() + interval '48 hours', false, now(), now() WHERE NOT EXISTS (SELECT 1 FROM disputes WHERE booking_id = '$complaintBooking') ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("UPDATE bookings SET status = 'Disputed', updated_at_utc = now() WHERE id = '$complaintBooking' AND EXISTS (SELECT 1 FROM disputes WHERE booking_id = '$complaintBooking' AND status IN ('Open','AwaitingResponse','UnderReview','Appealed'));")
[void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT 'b00b0000-0000-0001-0000-000000000003', '$complaintBooking', 'Completed', 'Disputed', '$custUser', 'CustomerDisputeOpened', now(), now() WHERE EXISTS (SELECT 1 FROM disputes WHERE booking_id = '$complaintBooking' AND status IN ('Open','AwaitingResponse','UnderReview','Appealed')) ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine("INSERT INTO dispute_messages (id, dispute_id, author_user_id, body, is_internal_admin_note, created_at_utc, updated_at_utc) SELECT '$sampleDisputeMessage', '$sampleDispute', '$custUser', 'Tôi đã kiểm tra lại sau ca và muốn nhờ admin hỗ trợ trao đổi với người làm.', false, now(), now() WHERE EXISTS (SELECT 1 FROM disputes WHERE id = '$sampleDispute') ON CONFLICT (id) DO NOTHING;")
[void]$sql.AppendLine('COMMIT;')

if ($ConnectionString) {
    $sql.ToString() | docker run --rm -i postgres:16-alpine psql $ConnectionString -v ON_ERROR_STOP=1 -q
} else {
    $sql.ToString() | docker exec -i $Container psql -U $DbUser -d $Database -v ON_ERROR_STOP=1 -q
}
if ($LASTEXITCODE -ne 0) { throw 'Seeding failed.' }
Write-Host "Seeded availability for 5 freelancers, $Days days from $($today.ToString('yyyy-MM-dd')) (08:00-12:00 and 13:00-18:00 Vietnam time)."
Write-Host "Test customer: phone $CustomerPhone, password '$CustomerPassword', one address in Quan 1."
Write-Host "Complaint fixtures: ABDEMO-C001 is an Open sample case for admin; ABDEMO-C003 is a Completed booking to submit a new complaint."
