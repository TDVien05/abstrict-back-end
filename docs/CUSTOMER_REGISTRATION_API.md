# Customer registration API

All routes use `/api/v1/auth`.

## Customer login

`POST /login`

```json
{
  "phoneNumber": "090 123 4567",
  "password": "abc12345"
}
```

The phone number is normalized using the same Vietnamese number rules as registration. Login succeeds only for an active customer whose phone number has been verified. A successful response contains a 15-minute JWT access token, returned as `tokenType: "Bearer"`, plus basic customer identity fields. Send it on protected endpoints as `Authorization: Bearer <accessToken>`. Login is limited to 10 requests per IP per minute. Incorrect credentials and inactive/unverified accounts return the same `401 INVALID_CREDENTIALS` response.

Set `Jwt:SigningKey` to a random secret of at least 32 UTF-8 bytes in non-development environments. `Jwt:Issuer` and `Jwt:Audience` can also be overridden. Production startup fails when the signing key is absent. The development signing key is local-only.

## Register

`POST /register`

```json
{
  "fullName": "Nguyễn Thu Hà",
  "phoneNumber": "090 123 4567",
  "password": "abc12345",
  "confirmPassword": "abc12345",
  "acceptTermsAndPrivacy": true
}
```

The name must contain 2–160 characters. Vietnamese phone numbers with 10 or 11 digits are normalized before storage. Passwords must contain at least 8 characters, a letter and a digit. Terms and privacy acceptance are required and their UTC timestamps are stored on `User`. Passwords are stored using ASP.NET Core's password hasher.

The endpoint creates a pending customer and issues a 6-digit registration OTP. A challenge expires after 5 minutes, can be resent after 45 seconds, and at most 5 OTPs may be issued to a phone number in 24 hours. Requests are additionally limited to 5 per IP in 10 minutes. The response contains the challenge id, phone number and UTC deadlines. In Development only, when `Otp:ExposeCodeToClient` is enabled, it also contains `developmentOtpCode` so the FE can complete the flow without an SMS provider.

## Verify phone

`POST /verify-phone-otp`

```json
{ "challengeId": "00000000-0000-0000-0000-000000000000", "code": "123456" }
```

The code must be exactly 6 ASCII digits. Five incorrect attempts lock and consume that challenge. On success the challenge is consumed, `PhoneVerifiedAtUtc` is set and the user becomes active. Verification requests are limited to 10 per IP per minute.

## Resend OTP

`POST /resend-phone-otp`

```json
{ "challengeId": "00000000-0000-0000-0000-000000000000" }
```

The endpoint returns a new challenge id and deadlines, observes the same cooldown and daily phone limit, and is limited to 3 requests per IP in 10 minutes. It includes `developmentOtpCode` under the same Development-only setting.

Errors use `ProblemDetails` with a stable `code`, such as `VALIDATION_FAILED`, `INVALID_OTP`, `OTP_EXPIRED`, `OTP_ATTEMPTS_EXCEEDED`, `OTP_RESEND_COOLDOWN`, and `OTP_SEND_LIMIT_EXCEEDED`. Cooldown responses include `Retry-After` seconds.

## OTP delivery configuration

Development uses a mock sender that returns the code to the FE; the code is not written to logs. Code exposure additionally requires both the Development environment and `Otp:ExposeCodeToClient`. Production never returns the OTP and currently fails closed because no SMS/Zalo provider or credentials have been selected; implement/register an `IPhoneOtpSender` for the chosen provider before enabling registration there. `Otp:HmacKey` is a local-only key in `appsettings.Development.json`; production must override it with a randomly generated secret of at least 32 UTF-8 bytes.
