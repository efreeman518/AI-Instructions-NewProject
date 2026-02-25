# Key Vault

## Prerequisites

- [package-dependencies.md](package-dependencies.md)
- [configuration.md](configuration.md)
- [identity-management.md](identity-management.md)
- [bootstrapper.md](bootstrapper.md)

## Purpose

Use Key Vault for:

1. startup configuration secrets,
2. runtime secret/key/certificate operations,
3. cryptographic operations where private keys remain in Key Vault.

## Non-Negotiables

1. Use managed identity/`DefaultAzureCredential` in hosted environments.
2. Keep Key Vault access behind project-specific abstractions.
3. Never log secret values or return them in raw API payloads.
4. Use RBAC least-privilege roles for secrets/keys/certs.
5. Keep soft delete and purge protection enabled.

---

## Core Manager Contract

`IKeyVaultManager` is the runtime abstraction for secret/key/certificate lifecycle actions.

```csharp
public interface IKeyVaultManager
{
    Task<string?> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default);
    Task<string?> SaveSecretAsync(string name, string? value = null, CancellationToken cancellationToken = default);
    Task<DeletedSecret> StartDeleteSecretAsync(string name, CancellationToken cancellationToken = default);

    Task<JsonWebKey?> GetKeyAsync(string name, string? version = null, CancellationToken cancellationToken = default);
    Task<JsonWebKey?> CreateKeyAsync(string name, KeyType keyType, CreateKeyOptions? options = null, CancellationToken cancellationToken = default);
    Task<KeyRotationPolicy> UpdateKeyRotationPolicyAsync(string name, KeyRotationPolicy policy, CancellationToken cancellationToken = default);
    Task<JsonWebKey?> RotateKeyAsync(string name, CancellationToken cancellationToken = default);
    Task<JsonWebKey?> DeleteKeyAsync(string name, CancellationToken cancellationToken = default);

    Task<byte[]?> GetCertAsync(string certificateName, CancellationToken cancellationToken = default);
    Task<byte[]?> ImportCertAsync(ImportCertificateOptions importCertificateOptions, CancellationToken cancellationToken = default);
}
```

### Project Wrapper Pattern

```csharp
public interface I{Project}KeyVaultManager : IKeyVaultManager { }

public class {Project}KeyVaultManager : KeyVaultManagerBase, I{Project}KeyVaultManager
{
    public {Project}KeyVaultManager(
        ILogger<{Project}KeyVaultManager> logger,
        IOptions<{Project}KeyVaultManagerSettings> settings,
        IAzureClientFactory<SecretClient> secretClientFactory,
        IAzureClientFactory<KeyClient> keyClientFactory,
        IAzureClientFactory<CertificateClient> certClientFactory)
        : base(logger, settings, secretClientFactory, keyClientFactory, certClientFactory) { }
}

public class {Project}KeyVaultManagerSettings : KeyVaultManagerSettingsBase { }
```

`KeyVaultManagerSettingsBase` requires `KeyVaultClientName`.

---

## Crypto Utility Contract

```csharp
public interface IKeyVaultCryptoUtility
{
    Task<byte[]> EncryptAsync(string plaintext);
    Task<string> DecryptAsync(byte[] ciphertext);
}
```

Use `CryptographyClient` (typically RSA-OAEP). This keeps private key operations in Key Vault.

---

## Configuration

`appsettings.json`

```json
{
  "KeyVault": {
    "VaultUri": ""
  },
  "{Project}KeyVaultManagerSettings": {
    "KeyVaultClientName": "{Project}KVClient"
  }
}
```

`appsettings.Development.json`

```json
{
  "KeyVault": {
    "VaultUri": "https://{project}-kv-dev.vault.azure.net/"
  }
}
```

---

## DI Registration (Bootstrapper)

```csharp
private static void AddKeyVaultServices(IServiceCollection services, IConfiguration config)
{
    var vaultUri = new Uri(config["KeyVault:VaultUri"]!);

    services.AddAzureClients(builder =>
    {
        builder.AddSecretClient(vaultUri).WithName("{Project}KVClient");
        builder.AddKeyClient(vaultUri).WithName("{Project}KVClient");
        builder.AddCertificateClient(vaultUri).WithName("{Project}KVClient");
        builder.UseCredential(new DefaultAzureCredential());
    });

    services.Configure<{Project}KeyVaultManagerSettings>(
        config.GetSection("{Project}KeyVaultManagerSettings"));

    services.AddScoped<I{Project}KeyVaultManager, {Project}KeyVaultManager>();
}
```

Crypto utility registration pattern:

```csharp
services.AddSingleton(sp =>
{
    var keyClient = new KeyClient(new Uri(config["KeyVault:VaultUri"]!), new DefaultAzureCredential());
    return keyClient.GetCryptographyClient(config["KeyVault:EncryptionKeyName"]!);
});
services.AddScoped<IKeyVaultCryptoUtility, KeyVaultCryptoUtility>();
```

---

## Usage Patterns

- **Startup configuration secrets:** Key Vault configuration provider.
- **Runtime secret management:** `I{Project}KeyVaultManager`.
- **Field-level encryption:** `IKeyVaultCryptoUtility`.
- **Key lifecycle:** create + policy + rotate through manager abstraction.
- **Certificates:** retrieve/import via manager abstraction.

Cache hot secrets appropriately; avoid per-request Key Vault round-trips unless required.

---

## Security Rules

1. `DefaultAzureCredential` + managed identity for production.
2. Minimal RBAC scope:
   - Secrets User/Officer only when needed,
   - Crypto User only for crypto operations,
   - Certificates User only for cert retrieval.
3. Keep diagnostic logs metadata-only (operation names, key names, status).
4. Isolate dev/prod vault URIs by environment.

---

## Verification

- [ ] project manager derives from `KeyVaultManagerBase`
- [ ] settings derive from `KeyVaultManagerSettingsBase`
- [ ] named `SecretClient`/`KeyClient`/`CertificateClient` are registered
- [ ] authentication uses `DefaultAzureCredential`
- [ ] vault URI and client name come from configuration
- [ ] crypto utility is bound to a specific key
- [ ] secrets are not logged or exposed in API output
- [ ] cross-check with [identity-management.md](identity-management.md), [iac.md](iac.md), and [configuration.md](configuration.md)