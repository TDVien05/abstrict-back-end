<#
 Adds demo customers and, for each approved demo freelancer, two completed bookings:
 one with a public customer review and one ready for the customer to review.
 Idempotent: deterministic IDs and ON CONFLICT DO NOTHING.
 Usage: powershell -File scripts/seed-rating-fixtures.ps1 [-Container abstrict-postgres]
 DEV ONLY.
#>
param(
    [string]$Container = 'abstrict-postgres',
    [string]$Database = 'abstrict',
    [string]$DbUser = 'abstrict',
    [string]$ConnectionString = '',
    [string]$CustomerPassword = 'Customer-dev-2026'
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)

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

$sql = New-Object Text.StringBuilder
[void]$sql.AppendLine('BEGIN;')
$categoryId = 'aaaaaaaa-0000-0000-0000-000000000001'
$rows = @(
    @{ N = 2; Provider = 1; Score = 5; Name = 'Nguyễn Minh Anh'; Phone = '0920000002'; Comment = 'Đúng giờ, làm việc cẩn thận và sạch sẽ.' },
    @{ N = 3; Provider = 2; Score = 4; Name = 'Trần Quốc Bảo'; Phone = '0920000003'; Comment = 'Thái độ tốt, hoàn thành hầu hết checklist.' },
    @{ N = 4; Provider = 3; Score = 5; Name = 'Lê Thu Hà'; Phone = '0920000004'; Comment = 'Dịch vụ rất tốt, tôi sẽ đặt lại.' },
    @{ N = 5; Provider = 4; Score = 5; Name = 'Phạm Gia Linh'; Phone = '0920000005'; Comment = 'Làm việc kỹ lưỡng, đúng như mô tả.' },
    @{ N = 6; Provider = 5; Score = 4; Name = 'Nguyễn Hoàng Nam'; Phone = '0920000006'; Comment = 'Thân thiện và hoàn thành đúng giờ.' }
)

foreach ($row in $rows) {
    $n = [int]$row.N
    $customer = 'c0c0c0c0-0000-0000-0000-{0:D12}' -f $n
    $profile = 'c0c0c0c0-0000-0000-0001-{0:D12}' -f $n
    $address = 'c0c0c0c0-0000-0000-0002-{0:D12}' -f $n
    $booking = 'b00b0000-0000-0000-0000-{0:D12}' -f (10 + $n)
    $history = 'b00b0000-0000-0001-0000-{0:D12}' -f (10 + $n)
    $rating = 'fa710000-0000-0000-0000-{0:D12}' -f $n
    $unreviewedBooking = 'b00b0000-0000-0000-0000-{0:D12}' -f (100 + $n)
    $unreviewedHistory = 'b00b0000-0000-0001-0000-{0:D12}' -f (100 + $n)
    $provider = 'dddddddd-0000-0000-0000-{0:D12}' -f [int]$row.Provider
    $passwordHash = New-IdentityV3Hash $CustomerPassword
    $name = $row.Name.Replace("'", "''")
    $comment = $row.Comment.Replace("'", "''")
    $bookingNumber = 'ABRATE-C{0:D3}' -f $n
    $bookingStartDays = 14 + $n

    [void]$sql.AppendLine("INSERT INTO users (id, phone_number, password_hash, role, status, phone_verified_at_utc, terms_accepted_at_utc, privacy_policy_accepted_at_utc, created_at_utc, updated_at_utc) VALUES ('$customer', '$($row.Phone)', '$passwordHash', 'Customer', 'Active', now(), now(), now(), now(), now()) ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO customer_profiles (id, user_id, full_name, created_at_utc, updated_at_utc) SELECT '$profile', '$customer', '$name', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$customer') ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO customer_addresses (id, customer_user_id, label, apartment_number, floor, building_name, street_address, ward, district, city, is_default, created_at_utc, updated_at_utc) SELECT '$address', '$customer', 'Nhà', '120$n', '12', 'Chung cư demo', '2 Tôn Đức Thắng', 'Bến Nghé', 'Quận 1', 'Hồ Chí Minh', true, now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$customer') ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO bookings (id, booking_number, customer_user_id, provider_id, service_category_id, customer_address_id, status, starts_at_utc, ends_at_utc, completed_at_utc, unit_price_vnd, base_amount_vnd, surcharge_amount_vnd, deposit_amount_vnd, extension_amount_vnd, address_snapshot, created_at_utc, updated_at_utc) SELECT '$booking', '$bookingNumber', '$customer', '$provider', '$categoryId', '$address', 'Completed', now() - interval '$bookingStartDays days', now() - interval '$bookingStartDays days' + interval '4 hours', now() - interval '$bookingStartDays days' + interval '4 hours', 80000, 320000, 0, 48000, 0, '120$n, Chung cư demo, 2 Tôn Đức Thắng, Bến Nghé, Quận 1, Hồ Chí Minh', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$customer') AND EXISTS (SELECT 1 FROM providers WHERE id = '$provider' AND type = 'Freelancer' AND approval_status = 'Approved') ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT '$history', '$booking', 'PendingDeposit', 'Completed', '$customer', 'DemoSeed', now(), now() WHERE EXISTS (SELECT 1 FROM bookings WHERE id = '$booking') ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO ratings (id, booking_id, reviewer_user_id, subject_type, subject_provider_id, score, comment, publish_after_utc, published_at_utc, created_at_utc, updated_at_utc) SELECT '$rating', '$booking', '$customer', 'Provider', '$provider', $([int]$row.Score), '$comment', now(), now(), now(), now() WHERE EXISTS (SELECT 1 FROM bookings WHERE id = '$booking' AND status = 'Completed') ON CONFLICT DO NOTHING;")
    $unreviewedNumber = 'ABUNRV-C{0:D3}' -f $n
    $unreviewedDays = 30 + $n
    [void]$sql.AppendLine("INSERT INTO bookings (id, booking_number, customer_user_id, provider_id, service_category_id, customer_address_id, status, starts_at_utc, ends_at_utc, completed_at_utc, unit_price_vnd, base_amount_vnd, surcharge_amount_vnd, deposit_amount_vnd, extension_amount_vnd, address_snapshot, created_at_utc, updated_at_utc) SELECT '$unreviewedBooking', '$unreviewedNumber', '$customer', '$provider', '$categoryId', '$address', 'Completed', now() - interval '$unreviewedDays days', now() - interval '$unreviewedDays days' + interval '4 hours', now() - interval '$unreviewedDays days' + interval '4 hours', 80000, 320000, 0, 48000, 0, '120$n, Chung cư demo, 2 Tôn Đức Thắng, Bến Nghé, Quận 1, Hồ Chí Minh', now(), now() WHERE EXISTS (SELECT 1 FROM users WHERE id = '$customer') AND EXISTS (SELECT 1 FROM providers WHERE id = '$provider' AND type = 'Freelancer' AND approval_status = 'Approved') ON CONFLICT DO NOTHING;")
    [void]$sql.AppendLine("INSERT INTO booking_status_history (id, booking_id, from_status, to_status, changed_by_user_id, reason, created_at_utc, updated_at_utc) SELECT '$unreviewedHistory', '$unreviewedBooking', 'PendingDeposit', 'Completed', '$customer', 'DemoSeed', now(), now() WHERE EXISTS (SELECT 1 FROM bookings WHERE id = '$unreviewedBooking') ON CONFLICT DO NOTHING;")
}

[void]$sql.AppendLine("UPDATE providers p SET rating_count = r.cnt, average_rating = r.avg_score, updated_at_utc = now() FROM (SELECT subject_provider_id, count(*)::int AS cnt, round(avg(score)::numeric, 2)::double precision AS avg_score FROM ratings WHERE subject_type = 'Provider' AND subject_provider_id IS NOT NULL AND published_at_utc IS NOT NULL AND published_at_utc <= now() GROUP BY subject_provider_id) r WHERE p.id = r.subject_provider_id;")
[void]$sql.AppendLine('COMMIT;')

if ($ConnectionString) {
    $sql.ToString() | docker run --rm -i postgres:16-alpine psql $ConnectionString -v ON_ERROR_STOP=1 -q
} else {
    $sql.ToString() | docker exec -i $Container psql -U $DbUser -d $Database -v ON_ERROR_STOP=1 -q
}
if ($LASTEXITCODE -ne 0) { throw 'Seeding failed.' }
Write-Host 'Seeded up to five approved demo freelancers with one reviewed and one not-yet-reviewed Completed booking each.'
Write-Host "Demo customer phones: 0920000002-0920000006; password: '$CustomerPassword'."
Write-Host 'Reviewed bookings: ABRATE-C002 through ABRATE-C006.'
Write-Host 'Ready-to-review bookings: ABUNRV-C002 through ABUNRV-C006.'
