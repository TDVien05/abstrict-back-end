<#
 Downloads a portrait for each fake freelancer (see seed-fake-freelancers.ps1), uploads it to the
 dockerised MinIO bucket and points providers.avatar_object_key at it.
 Idempotent: deterministic object keys (avatars/<providerId>.jpg), re-running overwrites.
 Usage:  powershell -File scripts/seed-freelancer-avatars.ps1 [-MinioContainer abstrict-minio] [-Bucket abstrict-private]
 Photos come from pexels.com (free to use under the Pexels License). DEV ONLY.
#>
param(
    [string]$Container = 'abstrict-postgres',
    [string]$Database = 'abstrict',
    [string]$DbUser = 'abstrict',
    [string]$MinioContainer = 'abstrict-minio',
    [string]$Bucket = 'abstrict-private',
    [int]$Size = 512
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Seed index (matches the dddddddd-... provider ids) -> Pexels photo id.
$photos = @{
    1 = 1645820    # Nguyễn Thị Lan
    2 = 25974876   # Trần Văn Hùng
    3 = 4350178    # Lê Thị Mai
    4 = 450214     # Phạm Quốc Bảo
    5 = 27966713   # Võ Thị Hồng
    6 = 7501576    # Đặng Minh Tuấn
    7 = 1066121    # Hoàng Thị Thu
    8 = 26524785   # Bùi Anh Khoa
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('abstrict-avatars-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$sql = New-Object Text.StringBuilder
[void]$sql.AppendLine('BEGIN;')
try {
    foreach ($n in ($photos.Keys | Sort-Object)) {
        $photoId = $photos[$n]
        $prov = 'dddddddd-0000-0000-0000-{0:D12}' -f $n
        $key = 'avatars/{0}.jpg' -f $prov.Replace('-', '')
        $file = Join-Path $work "$n.jpg"
        $url = "https://images.pexels.com/photos/$photoId/pexels-photo-$photoId.jpeg?auto=compress&cs=tinysrgb&w=$Size&h=$Size&fit=crop"

        Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing -UserAgent 'Mozilla/5.0'
        $head = [IO.File]::ReadAllBytes($file)[0..2]
        if ($head[0] -ne 0xFF -or $head[1] -ne 0xD8 -or $head[2] -ne 0xFF) { throw "Photo $photoId is not a JPEG." }

        # Upload from inside the MinIO container so the root credentials never leave it.
        docker cp $file "${MinioContainer}:/tmp/avatar.jpg" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Copying photo $photoId into $MinioContainer failed." }
        docker exec -e "OBJECT=$Bucket/$key" $MinioContainer sh -c 'set -f; IFS=; curl --fail --silent --show-error --aws-sigv4 aws:amz:us-east-1:s3 --user $MINIO_ROOT_USER:$MINIO_ROOT_PASSWORD -H Content-Type:image/jpeg -T /tmp/avatar.jpg http://localhost:9000/$OBJECT; rc=$?; rm -f /tmp/avatar.jpg; exit $rc'
        if ($LASTEXITCODE -ne 0) { throw "Uploading $key to MinIO failed." }

        [void]$sql.AppendLine("UPDATE providers SET avatar_object_key = '$key', updated_at_utc = now() WHERE id = '$prov';")
        Write-Host "Uploaded $Bucket/$key (pexels $photoId)"
    }
}
finally {
    Remove-Item -Recurse -Force $work
}
[void]$sql.AppendLine('COMMIT;')

$sql.ToString() | docker exec -i $Container psql -U $DbUser -d $Database -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Updating avatar keys failed.' }
Write-Host "Seeded $($photos.Count) freelancer avatars into MinIO bucket '$Bucket'."
