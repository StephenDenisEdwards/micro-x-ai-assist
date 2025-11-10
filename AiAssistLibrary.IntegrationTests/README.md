# Markdown File



# Setting the environment variable for tests


````````

This is the description of what the code block changes:
Add detailed instructions for setting Azure OpenAI environment variables for integration tests.

This is the code block that represents the suggested code change:

````````markdown
# Integration Tests

These tests exercise the `HybridQuestionDetector` against simulated speech-to-text output. The Azure fallback classification runs only when the required environment variables are present.

## Required Environment Variables

| Name | Purpose |
|------|---------|
| `AZURE_OPENAI_ENDPOINT` | Base endpoint of your Azure OpenAI resource (e.g. `https://my-resource.openai.azure.com`) |
| `AZURE_OPENAI_DEPLOYMENT` | Name of the chat/completions deployment (e.g. a GPT model deployment) |
| `AZURE_OPENAI_API_KEY` | API key for the Azure OpenAI resource |

If any are missing the integration test is skipped (`[SkippableFact]`).

## Set Variables Temporarily

### PowerShell
```powershell
$env:AZURE_OPENAI_ENDPOINT="https://my-resource.openai.azure.com"
$env:AZURE_OPENAI_DEPLOYMENT="myGptDeployment"
$env:AZURE_OPENAI_API_KEY="<key>"
# Run tests
dotnet test AiAssistLibrary.IntegrationTests/AiAssistLibrary.IntegrationTests.csproj
```

### cmd.exe
```cmd
set AZURE_OPENAI_ENDPOINT=https://my-resource.openai.azure.com
set AZURE_OPENAI_DEPLOYMENT=myGptDeployment
set AZURE_OPENAI_API_KEY=<key>
rem Run tests
dotnet test AiAssistLibrary.IntegrationTests\AiAssistLibrary.IntegrationTests.csproj
```

### Linux / macOS (bash / zsh)
```bash
export AZURE_OPENAI_ENDPOINT="https://my-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT="myGptDeployment"
export AZURE_OPENAI_API_KEY="<key>"
dotnet test AiAssistLibrary.IntegrationTests/AiAssistLibrary.IntegrationTests.csproj
```

## Persistent (User scope, Windows PowerShell)
```powershell
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_ENDPOINT","https://my-resource.openai.azure.com","User")
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT","myGptDeployment","User")
[Environment]::SetEnvironmentVariable("AZURE_OPENAI_API_KEY","<key>","User")
# Restart Visual Studio / shell before running tests
```

## Visual Studio RunSettings File
Create a `test.runsettings` file in the solution root:
```xml
<RunSettings>
 <TestRunParameters>
 <Parameter name="AZURE_OPENAI_ENDPOINT" value="https://my-resource.openai.azure.com" />
 <Parameter name="AZURE_OPENAI_DEPLOYMENT" value="myGptDeployment" />
 <Parameter name="AZURE_OPENAI_API_KEY" value="<key>" />
 </TestRunParameters>
</RunSettings>
```
Then: Test > Configure Run Settings > Select `test.runsettings`.

## Inline for a Single Command (POSIX shells)
```bash
AZURE_OPENAI_ENDPOINT=https://my-resource.openai.azure.com \
AZURE_OPENAI_DEPLOYMENT=myGptDeployment \
AZURE_OPENAI_API_KEY=<key> \
 dotnet test AiAssistLibrary.IntegrationTests/AiAssistLibrary.IntegrationTests.csproj
```

## GitHub Actions Example
```yaml
jobs:
 test:
 runs-on: ubuntu-latest
 steps:
 - uses: actions/checkout@v4
 - uses: actions/setup-dotnet@v4
 with:
 dotnet-version:8.0.x
 - name: Run tests
 env:
 AZURE_OPENAI_ENDPOINT: https://my-resource.openai.azure.com
 AZURE_OPENAI_DEPLOYMENT: myGptDeployment
 AZURE_OPENAI_API_KEY: ${{ secrets.AZURE_OPENAI_API_KEY }}
 run: dotnet test AiAssistLibrary.IntegrationTests/AiAssistLibrary.IntegrationTests.csproj --logger trx
```

## Verifying Variables
You can add a temporary check:
```csharp
Console.WriteLine(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
```
Or inside a test:
```csharp
Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")));
```

## Security Notes
- Never commit real keys to source control.
- Prefer a secrets manager or CI secret store for `AZURE_OPENAI_API_KEY`.
- Scope API keys with least privilege where possible.

## Running the Integration Test
```bash
dotnet test AiAssistLibrary.IntegrationTests/AiAssistLibrary.IntegrationTests.csproj -v minimal
```
If variables are set, the `HybridDetector_AzureIntegrationTests` test will attempt a live classification; otherwise it skips.

