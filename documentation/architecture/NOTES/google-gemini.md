

Here’s the simplest mental model: an API key alone is often not enough for the Live (streaming) endpoint; you need an OAuth 2.0 access token (“Bearer” token) with the proper scope so the server knows who you are and that you’re allowed to open a live session.

Steps (pick ONE path)

Fast developer machine path (gcloud CLI)
Install Google Cloud SDK if you don’t have it.
Run: gcloud auth login (sign in)
Run: gcloud auth application-default login (sets ADC)
Then whenever you need a bearer token:
gcloud auth application-default print-access-token
That output string is your Bearer token. Set it as an env var:
PowerShell:
setx GEMINI_OAUTH_TOKEN (gcloud auth application-default print-access-token)
(Restart shell so your app can read the variable.)
Service account JWT path (headless)
In Google Cloud Console:
Enable the Generative Language API (same project as your model access).
Create a Service Account (IAM & Admin > Service Accounts).
Grant it a role that allows generative language usage (for Gemini experimental endpoints that may be “Generative Language User” or a broader AI role—if uncertain start with roles/viewer + API-specific user role).
Create a JSON key for that service account and download it.
Use that JSON to mint an access token:
Scope: https://www.googleapis.com/auth/generative-language
Token endpoint: https://oauth2.googleapis.com/token
Flow: Create a signed JWT (RS256) with:
iss: service account email
sub: service account email
aud: https://oauth2.googleapis.com/token
iat: current time
exp: iat + 3600
scope: https://www.googleapis.com/auth/generative-language
POST form: grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer assertion=<signed_jwt>
Response contains access_token you use as Bearer.
User OAuth (auth code or device code)
Create OAuth Client ID (Web or Desktop).
Use an OAuth library to request an access token with the scope https://www.googleapis.com/auth/generative-language.
After user consents you get refresh + access token. Refresh periodically.
Recommended (least friction): gcloud path while developing. Move to service account for production.

C# snippet for service account (using Google.Apis.Auth):
Add package:
dotnet add package Google.Apis.Auth

Code:
using Google.Apis.Auth.OAuth2;
var credential = GoogleCredential.FromFile("service-account.json")
.CreateScoped("https://www.googleapis.com/auth/generative-language");
var token = await credential.GetAccessTokenForRequestAsync();
Console.WriteLine(token);

Then pass token into your GeminiLiveClient constructor (bearerToken).

Using in WebSocket:
_ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");

Important distinctions:

API key: identifies the project, limited to simple model calls.
OAuth access token: ties identity + permissions, required for privileged endpoints (like live streaming).
Refresh: The access token expires (~1 hour). Re-fetch before expiration.
Troubleshooting checklist:

401 Unauthorized: bad/expired token.
403 Forbidden: token valid but lacks permission (scope or role).
404 on handshake: still using wrong endpoint or missing required auth (re-check you removed ?key= when using pure bearer).
Mixed success: try both header Authorization and x-goog-api-key (some previews layer both).
After you get the token:
PowerShell quick test:
$env:GEMINI_OAUTH_TOKEN = (gcloud auth application-default print-access-token)
dotnet run

Do you want me to patch Program.cs to automatically read GEMINI_OAUTH_TOKEN and pass it to the client? Just say “patch it” and I’ll apply.



--


Used 1 reference
Here’s the simplest mental model: an API key alone is often not enough for the Live (streaming) endpoint; you need an OAuth 2.0 access token (“Bearer” token) with the proper scope so the server knows who you are and that you’re allowed to open a live session.

Steps (pick ONE path)

Fast developer machine path (gcloud CLI)
Install Google Cloud SDK if you don’t have it.
Run: gcloud auth login (sign in)
Run: gcloud auth application-default login (sets ADC)
Then whenever you need a bearer token:
gcloud auth application-default print-access-token
That output string is your Bearer token. Set it as an env var:
PowerShell:
setx GEMINI_OAUTH_TOKEN (gcloud auth application-default print-access-token)
(Restart shell so your app can read the variable.)
Service account JWT path (headless)
In Google Cloud Console:
Enable the Generative Language API (same project as your model access).
Create a Service Account (IAM & Admin > Service Accounts).
Grant it a role that allows generative language usage (for Gemini experimental endpoints that may be “Generative Language User” or a broader AI role—if uncertain start with roles/viewer + API-specific user role).
Create a JSON key for that service account and download it.
Use that JSON to mint an access token:
Scope: https://www.googleapis.com/auth/generative-language
Token endpoint: https://oauth2.googleapis.com/token
Flow: Create a signed JWT (RS256) with:
iss: service account email
sub: service account email
aud: https://oauth2.googleapis.com/token
iat: current time
exp: iat + 3600
scope: https://www.googleapis.com/auth/generative-language
POST form: grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer assertion=<signed_jwt>
Response contains access_token you use as Bearer.
User OAuth (auth code or device code)
Create OAuth Client ID (Web or Desktop).
Use an OAuth library to request an access token with the scope https://www.googleapis.com/auth/generative-language.
After user consents you get refresh + access token. Refresh periodically.
Recommended (least friction): gcloud path while developing. Move to service account for production.

C# snippet for service account (using Google.Apis.Auth):
Add package:
dotnet add package Google.Apis.Auth

Code:
using Google.Apis.Auth.OAuth2;
var credential = GoogleCredential.FromFile("service-account.json")
.CreateScoped("https://www.googleapis.com/auth/generative-language");
var token = await credential.GetAccessTokenForRequestAsync();
Console.WriteLine(token);

Then pass token into your GeminiLiveClient constructor (bearerToken).

Using in WebSocket:
_ws.Options.SetRequestHeader("Authorization", $"Bearer {token}");

Important distinctions:

API key: identifies the project, limited to simple model calls.
OAuth access token: ties identity + permissions, required for privileged endpoints (like live streaming).
Refresh: The access token expires (~1 hour). Re-fetch before expiration.
Troubleshooting checklist:

401 Unauthorized: bad/expired token.
403 Forbidden: token valid but lacks permission (scope or role).
404 on handshake: still using wrong endpoint or missing required auth (re-check you removed ?key= when using pure bearer).
Mixed success: try both header Authorization and x-goog-api-key (some previews layer both).
After you get the token:
PowerShell quick test:
$env:GEMINI_OAUTH_TOKEN = (gcloud auth application-default print-access-token)
dotnet run
