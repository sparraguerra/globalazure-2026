using CommunityToolkit.Aspire.Hosting.Dapr;

var builder = DistributedApplication.CreateBuilder(args);

// Load Lab/.env for local development.
// Priority (lowest → highest): .env file → AppHost config/user secrets → Aspire-managed vars.
// This lets `dotnet user-secrets set` override .env without editing the file,
// while neither source overwrites Aspire-injected variables (OTEL endpoints, PORT, etc.).
var labEnvFile = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "..", ".env"));
var dotEnvVars = LoadDotEnv(labEnvFile);

Action<EnvironmentCallbackContext> injectServiceEnv = ctx =>
{
    // Step 1: .env as baseline — skip anything Aspire already set.
    foreach (var (key, value) in dotEnvVars)
        if (!ctx.EnvironmentVariables.ContainsKey(key))
            ctx.EnvironmentVariables[key] = value;

    // Step 2: AppHost config (user secrets, appsettings.Development.json) overrides .env
    //         but does NOT overwrite vars already managed by Aspire (OTEL, PORT, …).
    // GetChildren() returns only top-level entries; nested sections have null Value and are skipped.
    foreach (var section in builder.Configuration.GetChildren())
    {
        if (section.Value is null) continue;
        // Override if the key came from .env (allow user-secrets to win) OR is not set at all.
        if (dotEnvVars.ContainsKey(section.Key) || !ctx.EnvironmentVariables.ContainsKey(section.Key))
            ctx.EnvironmentVariables[section.Key] = section.Value;
    }
};

// ── tts-server ────────────────────────────────────────────────────────────────
// Requires GPU/CUDA (pytorch/pytorch base image). In local dev without GPU,
// set CONTENT_FACTORY_MODE=lab on agent-podcaster to use Azure OpenAI TTS fallback.
var ttsServer = builder.AddContainer("tts-server", "tts-server")
    .WithDockerfile("../../tts-server")
    .WithContainerName("tts-server")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHttpEndpoint(port: 8004, name: "http-tts-server", isProxied: false);

// ── agent-research ────────────────────────────────────────────────────────────
// Python FastAPI on port 8001. Requires a virtual environment:
//   cd Lab/src/agent-research && python -m venv .venv && .venv/Scripts/pip install -e .
var researchAgent = builder.AddPythonApp(
        "agent-research",
        "../../agent-research",
        "run.py")
    .WithHttpEndpoint(port: 8001, name: "http-research-agent", env: "PORT", isProxied: false)
    .WithEnvironment(injectServiceEnv)
    .WithOtlpExporter();

// ── agent-podcaster ────────────────────────────────────────────────────────────
// Python FastAPI on port 8003. Requires a virtual environment:
//   cd Lab/src/agent-podcaster && python -m venv .venv && .venv/Scripts/pip install -e .
var podcasterAgent = builder.AddPythonApp(
        "agent-podcaster",
        "../../agent-podcaster",
        "run.py")
    .WithHttpEndpoint(port: 8003, name: "http-podcaster-agent", env: "PORT", isProxied: false)
    .WithEnvironment("TTS_SERVER_URL", ttsServer.GetEndpoint("http-tts-server"))
    .WithEnvironment(injectServiceEnv)
    .WithOtlpExporter();

// ── agent-creator (.NET) ──────────────────────────────────────────────────────
var daprComponentsDir = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "..", "..", "dapr", "components"));

builder.AddProject<Projects.AgentCreator>("agent-creator")
    .WithHttpEndpoint(port: 8002, name: "http-agent-creator")
    .WithEnvironment(injectServiceEnv)
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "agent-creator",
        AppPort = 8002,
        ResourcesPaths = [daprComponentsDir]
    });

// ── agent-evaluator (.NET) ────────────────────────────────────────────────────
builder.AddProject<Projects.AgentEvaluator>("agent-evaluator")
    .WithHttpEndpoint(port: 8005, name: "http-agent-evaluator")
    .WithEnvironment(injectServiceEnv)
    .WithDaprSidecar(new DaprSidecarOptions
    {
        AppId = "agent-evaluator",
        AppPort = 8005,
        ResourcesPaths = [daprComponentsDir]
    });

// ── dev-ui ────────────────────────────────────────────────────────────────────
// Static nginx site on port 8080. Browser-side JS calls agents at
// localhost:8001 / 8002 / 8003 directly, so no env override is needed.
builder.AddContainer("dev-ui", "dev-ui")
    .WithDockerfile("../../dev-ui")
    .WithContainerName("dev-ui")
    .WithHttpEndpoint(port: 8080, name: "http-ui", isProxied: false);

builder.Build().Run();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Parses a .env file into a dictionary. Handles quoted values, the 'export' prefix,
/// and strips surrounding quotes. Lines starting with # and blank lines are skipped.
/// </summary>
static Dictionary<string, string> LoadDotEnv(string path)
{
    if (!File.Exists(path)) return [];

    var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(path))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

        // Handle optional 'export KEY=value' prefix used in some .env files.
        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["export ".Length..].TrimStart();

        var idx = trimmed.IndexOf('=');
        if (idx <= 0) continue;

        var key = trimmed[..idx].Trim();
        var raw = trimmed[(idx + 1)..].Trim();
        var value = raw is ['"', .., '"'] or ['\'', .., '\''] ? raw[1..^1] : raw;

        if (!string.IsNullOrEmpty(key))
            vars[key] = value;
    }
    return vars;
}

