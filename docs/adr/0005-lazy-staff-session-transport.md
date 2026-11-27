# 0005. Load the staff session client with the admin routes

## Decision

The sign-in flow calls the same-origin admin API using the browser Fetch API. The session
contract stays in the API library. The lazy route tree binds its implementation, and
unsubscribing cancels the request with an AbortController. A deployment using one origin
routes /admin/v1 to the API and browser page routes to the Angular app.

## Reason

Providing Angular HttpClient inside the lazy route tree increased the production initial
total to 248.87 kB from the 223.86 kB scaffold. A native request behind the session contract
and the existing attribute-based BEM class binding bring the initial total to 223.79 kB.
The admin shell and sign-in page both remain lazy.
