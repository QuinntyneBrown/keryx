# Security Policy

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues, discussions
or pull requests.**

Report them privately through
[GitHub private vulnerability reporting](https://github.com/QuinntyneBrown/keryx/security/advisories/new).

Please include as much of the following as you can:

- The type of issue (for example authorization bypass, injection through ingested content,
  data exposure, a leaked API credential)
- The affected files, endpoints, workers or screens
- Steps to reproduce, and proof-of-concept code if available
- The impact, including how an attacker might exploit it

You should receive an acknowledgement within 5 business days. We will keep you informed of
progress toward a fix and disclosure.

## Supported versions

Only the latest commit on `main` receives security fixes.

## Data

Keryx stores publicly published information about events, organizations and churches, and
serves it to other products. Reports involving a way to poison that data, to read or change
it without authorization, or to expose an API client's credentials or a staff account are
treated as the highest priority.
