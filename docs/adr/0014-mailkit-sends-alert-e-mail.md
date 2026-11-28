# 0014. MailKit sends alert e-mail

## Status

Accepted, 28 November 2026 (journey 19, staff receive alerts).

## Context

The worker e-mails alerts and the daily digest (L2-077, L2-078) through a mail service over
TLS, with a timeout. .NET's own `System.Net.Mail.SmtpClient` is not recommended for new
development: Microsoft's documentation says it does not support many modern protocols and
points to MailKit instead. ADR 0001 lists the backend's packages, and MailKit is not among them.

## Decision

- `SmtpMailSender` (Infrastructure) implements the `IMailSender` contract with MailKit and
  MimeKit, version 5. It is the only code that references them.
- Every connection is encrypted: port 465 uses TLS from the first byte, and any other port
  requires STARTTLS, so a server that offers neither is a failed send. Each send gives up after
  30 seconds.
- It logs nothing, so neither the credential nor the address reaches a log or a trace (L2-084).
- With no `Keryx__Mail__Host` the worker binds a sender that is not configured. No e-mail is
  attempted, and the worker logs that once at start.

## Consequences

- Tests never reach a mail service (AGENTS.md). They prove the choice of sender, the settings
  it requires and the choice of TLS mode. Behind the contract, a fake mail service proves every
  alert, refusal and retry. The SMTP conversation itself is MailKit's, and no test here runs
  it.
- A new MailKit major version is a dependency update like any other, checked by the build and
  the tests above.
